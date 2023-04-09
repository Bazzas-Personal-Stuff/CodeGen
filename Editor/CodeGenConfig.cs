using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MY_PACKAGE_NAMESPACE {
    [FilePath("Assets/Settings/BazzaGibbs/CodeGen/Editor/" + CodeGenWizard.PackageName + ".singleton", FilePathAttribute.Location.ProjectFolder)]
    public class CodeGenConfig : ScriptableSingleton<CodeGenConfig> {
        public string templatesDirectory = "";
        public string outputDirectory = "";
        public List<CodeGenMacroDefinition> macros = new();

        public void SaveConfig() {
            Save(true);
        }
    }

    [Serializable]
    public class CodeGenMacroDefinition {
        [Tooltip("Not including dollar signs $$")]
        public string macroName = "";
        public bool useSingleReplacement = true;
        public string replaceWith = "";
        public List<string> replaceWithBatch = new();
    }
    

    [CustomPropertyDrawer(typeof(CodeGenMacroDefinition))]
    public class CodeGenMacroDefinitionDrawer : PropertyDrawer {

        private readonly string[] m_PopupOptions = {
            "Single Macro Definition",
            "Batch Macro Definition"
        };
        private GUIStyle m_PopupStyle;
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (m_PopupStyle == null) {
                m_PopupStyle = new GUIStyle(GUI.skin.GetStyle("PaneOptions"));
                m_PopupStyle.imagePosition = ImagePosition.ImageOnly;
            }

            label = EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, label);
            
            SerializedProperty useSingleReplacement = property.FindPropertyRelative("useSingleReplacement");
            SerializedProperty replaceWithBatchProp = property.FindPropertyRelative("replaceWithBatch");
            SerializedProperty replaceWithProp = property.FindPropertyRelative("replaceWith");
            
            int indentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            Rect buttonRect = new(position);
            buttonRect.yMin += m_PopupStyle.margin.top;
            buttonRect.width = m_PopupStyle.fixedWidth + m_PopupStyle.margin.right;
            position.xMin = buttonRect.xMax;

            int result = EditorGUI.Popup(buttonRect, useSingleReplacement.boolValue ? 0 : 1, m_PopupOptions,
                m_PopupStyle);
            useSingleReplacement.boolValue = result == 0;

            EditorGUI.BeginDisabledGroup(false);
            SerializedProperty prop = useSingleReplacement.boolValue ? replaceWithProp : replaceWithBatchProp;
            EditorGUI.PropertyField(position, prop, GUIContent.none);
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.indentLevel = indentLevel;
            EditorGUI.EndProperty();
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            bool isUsingSingleReplacement = property.FindPropertyRelative("useSingleReplacement").boolValue;
            if (isUsingSingleReplacement) {
                return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("replaceWith"));
            }
            return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("replaceWithBatch"));
        }
    }

    [Serializable]
    public class CodeGenHelpPanel {
        public bool enabled = true;
        public string message;
        public MessageType messageType;
        

        public CodeGenHelpPanel(string message, MessageType messageType = MessageType.Info) {
            this.message = message;
            this.messageType = messageType;
        }
    }

    [CustomPropertyDrawer(typeof(CodeGenHelpPanel))]
    public class CodeGenHelpDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            SerializedProperty enabled = property.FindPropertyRelative("enabled");
            if (enabled.boolValue == false) return;
            
            SerializedProperty helpString = property.FindPropertyRelative("message");
            SerializedProperty messageType = property.FindPropertyRelative("messageType");
            EditorGUILayout.HelpBox(helpString.stringValue, (MessageType) messageType.enumValueIndex);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return 0;
        }
    }

}