using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;


namespace EngineArtist {


[Serializable]
public struct OutputSlot {
    public string name;
    public GameObject target;
    public string input;
    [SerializeReference] public IAttribute[] args;
}


[Serializable]
public struct OutputBind {
    public GameObject self;
    public string output;
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
                if (sign.outputs[i].name == name && sign.outputs[i].target != null) {
                    sign.outputs[i].target.ReceiveSignal(
                        sign.outputs[i].input,
                        sign.outputs[i].args
                    );
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
        return property.isExpanded ? 54f: 16f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);
        var name = property.FindPropertyRelative("name");
        var foldoutLabel = name.stringValue;
        if (foldoutLabel == null || foldoutLabel == "") foldoutLabel = "Unnamed output";
        property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, property.isExpanded ? 19f: position.width, 16f), property.isExpanded, property.isExpanded ? "": foldoutLabel, true, EditorStyles.foldout);
        if (property.isExpanded) {
            var target = property.FindPropertyRelative("target");
            var input = property.FindPropertyRelative("input");
            name.stringValue = EditorGUI.TextField(new Rect(position.x, position.y, position.width, 16f), name.stringValue);
            EditorGUI.PropertyField(new Rect(position.x, position.y + 16f, position.width, 16f), target);
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
                    input.stringValue = inputNames[EditorGUI.Popup(new Rect(position.x, position.y + 32f, position.width, 16f), "Target input", sel, inputNames)];
                }
                else {
                    input.stringValue = "";
                }
            }
        }
        EditorGUI.EndProperty();
    }
}


[CustomPropertyDrawer(typeof(OutputBind))]
public class OutputBindDrawer: PropertyDrawer {
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