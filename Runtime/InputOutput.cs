using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;


namespace EngineArtist {


public static class InputOutputUtils {
    public static bool callbackRegistered = false;


    public static void UpdateCallback(SceneView view) {
        var gobj = Selection.activeGameObject;
        if (gobj != null) {
            var io = gobj.GetComponent<InputOutput>();
            if (io != null && io.outputs != null) {
                var from = gobj.transform.localPosition;
                Handles.color = Color.red;
                for (int i = 0; i < io.outputs.Count; ++i) {
                    if (io.outputs[i].targets != null) {
                        for (int j = 0; j < io.outputs[i].targets.Count; ++j) {
                            if (io.outputs[i].targets[j].target != null) {
                                Handles.DrawLine(from, io.outputs[i].targets[j].target.transform.localPosition);
                            }
                        }
                    }
                }
            }
        }
    }
}


[Serializable]
public struct OutputSlot {
    public string name;
    public List<OutputTarget> targets;
}


[Serializable]
public struct OutputTarget {
    public GameObject target;
    public string input;
    [SerializeReference] public IAttribute[] args;
}


public interface Command {
    void Execute();
}


[Serializable]
public class OutputTrigger: Command {
    public GameObject self;
    public string output;


    public void Execute() => self.SendSignal(output);
}


[Serializable]
public struct InputSlot {
    public string name;
    public InputBind inputBind;
}


[Serializable]
public class InputBind {
    public string componentName;
    public string methodName;
    //public string[] argTypeNames;

    private MethodInfo _methodInfo;
    private Component _component;

    public void Call(GameObject target, string signalName, IAttribute[] args) {
        if (methodName == null || methodName == "") {
            return;
        }
        if (_methodInfo != null) {
            _methodInfo.Invoke(_component, null);
            return;
        }
        _component = target.GetComponent(componentName);
        if (_component == null) {
            Debug.LogWarning("Cannot process signal '" + signalName + "': GameObject '" + target.name + "' doesn't have Component '" + componentName + "'");
            return;
        }
        var compType = _component.GetType();
        //Type[] argTypes = new Type[argTypeNames.Length];
        //for (int i = 0; i < argTypes.Length; ++i) {
        //    argTypes[i] = Type.GetType(argTypeNames[i], false, false);
        //}
        _methodInfo = compType.GetMethod(methodName, new Type[] {});
        if (_methodInfo == null) {
            Debug.LogWarning("Cannot process signal '" + signalName + "': GameObject '" + target.name + "' with Component '" + componentName + "' doesn't have method '" + methodName + "' with the required arguments");
            return;
        }
        _methodInfo.Invoke(_component, null);
    }
}


public class InputOutput: MonoBehaviour {
    public List<OutputSlot> outputs;
    public List<InputSlot> inputs;
}


public static class InputOutputExtensions {
    public static void SendSignal(this GameObject gobj, string name) {
        var sign = gobj.GetComponent<InputOutput>();
        if (sign != null) {
            for (int i = 0; i < sign.outputs.Count; ++i) {
                if (sign.outputs[i].name == name && sign.outputs[i].targets != null) {
                    for (int j = 0; j < sign.outputs[i].targets.Count; ++j) {
                        if (sign.outputs[i].targets[j].target != null) {
                            sign.outputs[i].targets[j].target.ReceiveSignal(
                                sign.outputs[i].targets[j].input,
                                sign.outputs[i].targets[j].args
                            );
                        }
                    }
                    return;
                }
            }
        }
    }

    public static void ReceiveSignal(this GameObject gobj, string name, IAttribute[] args) {
        var sign = gobj.GetComponent<InputOutput>();
        if (sign != null) {
            for (int i = 0; i < sign.inputs.Count; ++i) {
                if (sign.inputs[i].name == name) {
                    sign.inputs[i].inputBind.Call(gobj, name, args);
                    return;
                }
            }
        }
    }
}


[CustomPropertyDrawer(typeof(OutputSlot))]
public class OutputSlotDrawer: PropertyDrawer {
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        if (property.isExpanded) {
            var height = 40f;
            var targets = property.FindPropertyRelative("targets");
            var targetsCount = targets.arraySize;
            for (int i = 0; i < targetsCount; ++i) {
                height += EditorGUI.GetPropertyHeight(targets.GetArrayElementAtIndex(i)) + 12f;
            }
            return height;
        }
        return 16f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);
        var name = property.FindPropertyRelative("name");
        var foldoutLabel = name.stringValue;
        if (foldoutLabel == null || foldoutLabel == "") foldoutLabel = "Unnamed output";
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, property.isExpanded ? 19f: position.width, 16f), property.isExpanded, property.isExpanded ? "": foldoutLabel, true, EditorStyles.foldout);
        if (property.isExpanded) {
            var height = 24f;
            name.stringValue = EditorGUI.TextField(new Rect(position.x, position.y, position.width, 16f), name.stringValue);
            var targets = property.FindPropertyRelative("targets");
            var targetsCount = targets.arraySize;
            var removeIndex = -1;
            for (int i = 0; i < targetsCount; ++i) {
                var target = targets.GetArrayElementAtIndex(i);
                EditorGUI.PropertyField(new Rect(position.x + 4f, position.y + height, position.width - 72f, 16f), target);
                if (GUI.Button(new Rect(position.x + position.width - 68f, position.y + height, 64f, 16f), "Remove")) {
                    removeIndex = i;
                }
                var targetHeight = EditorGUI.GetPropertyHeight(target);
                EditorGUI.HelpBox(new Rect(position.x + 16f, position.y + height - 4f, position.width - 16f, targetHeight + 8f), "", MessageType.None);
                height += targetHeight + 12f;
            }
            if (removeIndex >= 0) {
                targets.DeleteArrayElementAtIndex(removeIndex);
            }
            else if (GUI.Button(new Rect(position.x + 16f, position.y + height - 4f, 80f, 16f), "Add target")) {
                targets.InsertArrayElementAtIndex(targetsCount);
            }
        }
        EditorGUI.EndProperty();
    }
}


[CustomPropertyDrawer(typeof(OutputTarget))]
public class OutputTargetDrawer: PropertyDrawer {
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        var target = property.FindPropertyRelative("target");
        var targetGobj = (GameObject)target.objectReferenceValue;
        if (targetGobj != null) {
            var sign = targetGobj.GetComponent<InputOutput>();
            if (sign != null && sign.inputs != null && sign.inputs.Count > 0) {
                return 34f;
            }
        }
        return 16f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        if (!InputOutputUtils.callbackRegistered) {
            InputOutputUtils.callbackRegistered = true;
            SceneView.duringSceneGui -= InputOutputUtils.UpdateCallback;
            SceneView.duringSceneGui += InputOutputUtils.UpdateCallback;
        }
        EditorGUI.BeginProperty(position, label, property);
        var target = property.FindPropertyRelative("target");
        var input = property.FindPropertyRelative("input");
        EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, 16f), target);
        var targetGobj = (GameObject)target.objectReferenceValue;
        if (targetGobj != null) {
            var sign = targetGobj.GetComponent<InputOutput>();
            if (sign != null && sign.inputs != null && sign.inputs.Count > 0) {
                string[] inputNames = new string[sign.inputs.Count];
                for (int i = 0; i < inputNames.Length; ++i) {
                    inputNames[i] = sign.inputs[i].name;
                }
                int sel = Array.FindIndex(inputNames, (string s) => s == input.stringValue);
                if (sel < 0) sel = 0;
                input.stringValue = inputNames[EditorGUI.Popup(new Rect(position.x, position.y + 18f, position.width, 16f), "Target input", sel, inputNames)];
            }
            else {
                input.stringValue = "";
            }
        }
        EditorGUI.EndProperty();
    }
}


[CustomPropertyDrawer(typeof(OutputTrigger))]
public class OutputTriggerDrawer: PropertyDrawer {
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        return 16f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);
        var self = (GameObject)Selection.activeObject;
        property.FindPropertyRelative("self").objectReferenceValue = self;
        var io = self.GetComponent<InputOutput>();
        if (io != null && io.outputs != null && io.outputs.Count > 0) {
            string[] outputNames = new string[io.outputs.Count];
            for (int i = 0; i < outputNames.Length; ++i) {
                outputNames[i] = io.outputs[i].name;
            }
            var output = property.FindPropertyRelative("output");
            var outputIndex = Array.IndexOf(outputNames, output.stringValue);
            if (outputIndex < 0) outputIndex = 0;
            output.stringValue = outputNames[EditorGUI.Popup(new Rect(position.x, position.y, position.width, position.height), label.text, outputIndex, outputNames)];
        }
        else {
            EditorGUI.LabelField(new Rect(position.x, position.y, position.width, position.height), label);
            var offset = position.width*.4f;
            EditorGUI.HelpBox(new Rect(position.x + offset, position.y, position.width - offset, position.height), " GameObject has no outputs!", MessageType.Warning);
        }
        EditorGUI.EndProperty();
    }
}


[CustomPropertyDrawer(typeof(InputSlot))]
public class InputSlotDrawer: PropertyDrawer {
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        return property.isExpanded ? 56f: 16f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);
        var name = property.FindPropertyRelative("name");
        var foldoutLabel = name.stringValue;
        if (foldoutLabel == null || foldoutLabel == "") foldoutLabel = "Unnamed input";
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, property.isExpanded ? 19f: position.width, 16f), property.isExpanded, property.isExpanded ? "": foldoutLabel, true, EditorStyles.foldout);
        if (property.isExpanded) {
            var inputBind = property.FindPropertyRelative("inputBind");
            var componentName = inputBind.FindPropertyRelative("componentName");
            var gobj = (GameObject)Selection.activeObject;
            var comps = gobj.GetComponents<Component>();
            string[] compNames = new string[comps.Length];
            for (int i = 0; i < comps.Length; ++i) {
                compNames[i] = comps[i].GetType().Name;
            }
            var compIndex = Array.IndexOf(compNames, componentName.stringValue);
            if (compIndex < 0) compIndex = 0;
            var methodName = inputBind.FindPropertyRelative("methodName");
            name.stringValue = EditorGUI.TextField(new Rect(position.x, position.y, position.width, 16f), name.stringValue);
            componentName.stringValue = compNames[EditorGUI.Popup(new Rect(position.x, position.y + 16f, position.width, 16f), "Component", compIndex, compNames)];
            var methods = gobj.GetComponent(componentName.stringValue).GetType().GetMethods(BindingFlags.Instance|BindingFlags.Public);
            List<string> methodNames = new List<string>();
            for (int i = 0; i < methods.Length; ++i) {
                if (methods[i].GetParameters().Length == 0) {
                    methodNames.Add(methods[i].Name);
                }
            }
            var methodIndex = methodNames.IndexOf(methodName.stringValue);
            if (methodIndex < 0) methodIndex = 0;
            methodName.stringValue = methodNames[EditorGUI.Popup(new Rect(position.x, position.y + 34f, position.width, 16f), "Method", methodIndex, methodNames.ToArray())];
        }
        EditorGUI.EndProperty();
    }
}


}