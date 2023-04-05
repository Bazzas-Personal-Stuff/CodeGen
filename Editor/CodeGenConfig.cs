using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MY_PACKAGE_NAMESPACE {
    // [FilePath("Assets/Settings/BazzaGibbs/CodeGen/Editor/" + CodeGenWizard.PackageName + ".singleton", FilePathAttribute.Location.ProjectFolder)]
    [FilePath("Packages/" + CodeGenWizard.PackageName + "/CodeGen/Editor/Settings/CodeGenConfigAsset.singleton", FilePathAttribute.Location.ProjectFolder)]
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
        public string macroName;
        public string replaceWith = "";
}
    

    [CustomPropertyDrawer(typeof(CodeGenMacroDefinition))]
    public class CodeGenMacroDefinitionDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);
            int indentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            
            SerializedProperty macroNameProp = property.FindPropertyRelative("macroName");
            SerializedProperty replaceWithProp = property.FindPropertyRelative("replaceWith");
           
            EditorGUI.BeginDisabledGroup(false);
            EditorGUI.PropertyField(position, replaceWithProp, new GUIContent(macroNameProp.stringValue));
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.indentLevel = indentLevel;
            EditorGUI.EndProperty();
        }
    }


}