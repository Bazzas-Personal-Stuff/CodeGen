using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MY_PACKAGE_NAMESPACE {
    public class CodeGenWizard : ScriptableWizard {
        public const string PackageName = "MY_PACKAGE_URI";

// =====================================================================================================================
#region Variables
        // Defaults
        private string templatesDirectory = $"Packages/{PackageName}/CodeGen/Editor/Code Templates";
        public string outputDirectory = $"Assets/Generated/{PackageName}";
        public List<CodeGenMacroDefinition> macros;
#endregion      
// =====================================================================================================================
#region Regex
        private static readonly Regex s_MacroRegex = new(@"(?<!\\)\$[A-Z_]+[A-Z_.]*\$");
        //                                                        ^^^^^  ^ ^^^^   ^^^^^   ^
        //                       Negative lookbehind for escape char     |  |       |     |
        //                                        Don't start with . ----+--+       |+----+
        //                                                               |          ||       
        //                                                               $MAC_RO.CASE$
#endregion
// =====================================================================================================================
#region Ctors
        [MenuItem("Tools/MY_WIZARD_MENU_ITEM")]
        static void CreateWizard() {
            DisplayWizard<CodeGenWizard>("Create New GameVariable Type", "Generate", "Get Macros");
        }
        
        private void OnEnable() {
            LoadConfig();
        }

        private void OnDisable() {
            SaveConfig();
            AssetDatabase.Refresh();
        }
#endregion
// =====================================================================================================================
#region CodeGen Config
        private void LoadConfig() {
            CodeGenConfig cfg = CodeGenConfig.instance;
            if (cfg == null) {
                Debug.LogWarning("Could not find CodeGen config ScriptableObject.");
                return;
            }

            if (cfg.templatesDirectory.Length > 0) {
                templatesDirectory = cfg.templatesDirectory;
            }
            if (cfg.outputDirectory.Length > 0) {
                outputDirectory = cfg.outputDirectory;
            }
            
            macros = cfg.macros;
        }
        
        private void SaveConfig() {
            CodeGenConfig cfg = CodeGenConfig.instance;
            if (cfg == null) {
                Debug.LogWarning("Could not find CodeGen config ScriptableObject.");
                return;
            }

            if (templatesDirectory.Length > 0) {
                cfg.templatesDirectory = templatesDirectory;
            }
            if (outputDirectory.Length > 0) {
                cfg.outputDirectory = outputDirectory;
            }
            
            cfg.macros = macros;
            cfg.SaveConfig();
        }
#endregion
// =====================================================================================================================
#region Wizard Buttons 
        private void OnWizardCreate() {
            // Sanitize path inputs
            if (SanitizePath(templatesDirectory) == false) {
                Debug.LogError($"CodeGen template directory is not in the project directory: {Path.GetFullPath(templatesDirectory)}");
                return;
            }
            if (SanitizePath(outputDirectory, "Assets") == false) {
                Debug.LogError($"CodeGen output directory is not in the Assets folder: {Path.GetFullPath(outputDirectory)}");
                return;
            }

            // Check templatesDir exists
            if (Directory.Exists(templatesDirectory) == false) {
                Debug.LogError($"CodeGen template directory not found: {Path.GetFullPath(templatesDirectory)}.");
                return;
            }
            
            // Check write access to outputDir
            CreateDirectory(outputDirectory);
            if (Directory.Exists(outputDirectory) == false) {
                Debug.LogError($"Can't write to Codegen output directory: {Path.GetFullPath(outputDirectory)}");
                return;
            }

            Dictionary<string, HashSet<string>> requestedMacros = GetRequestedMacros(templatesDirectory);
            UpdateMacros(requestedMacros);

            Dictionary<string, string> macroReplacements = GetMacroReplacements(requestedMacros, macros);

            List<string> successfulWrites = new(); // Keep track of written files in case one fails
            bool result = true;
            try {
                foreach (string templatePath in GetTemplatePaths(templatesDirectory)) {
                    FileStream fs = File.OpenRead(templatePath);
                    StreamReader fsReader = new(fs);
                    string firstLine = fsReader.ReadLine();
                    string templateBody = fsReader.ReadToEnd();
                    fsReader.Close();

                    if (firstLine == null || firstLine.StartsWith("###") == false) {
                        Debug.LogError(
                            $"CodeGenWizard error parsing template {templatePath}: Incorrect syntax or empty file");
                        result = false;
                        break;
                    }


                    // Parse output file path
                    string outRelPathReplaced = ReplaceMacros(firstLine.Substring(3), macroReplacements);
                    string outputPath = Path.Combine(Path.GetRelativePath(".", outputDirectory),
                        Path.GetRelativePath(".", outRelPathReplaced));

                    if (SanitizePath(outputPath, "Assets") == false) {
                        Debug.LogError($"Output path for template {templatePath} is invalid: {outputPath}");
                        result = false;
                        break;
                    }
                    
                    // Parse template body
                    if (templateBody.Length <= 0) {
                        Debug.LogError($"CodeGenWizard error parsing template {templatePath}: Empty template body");
                        result = false;
                        break;
                    }
                    string templateBodyReplaced = ReplaceMacros(templateBody, macroReplacements);
                    string outputDestDir = Path.GetDirectoryName(outputPath);
                    if (outputDestDir == null) {
                        Debug.LogError($"Failure writing to file {outputPath}");
                        result = false;
                        break;
                    }
                    
                    // TODO: Record Undo 
                    Directory.CreateDirectory(outputDestDir);
                    File.WriteAllText(outputPath, templateBodyReplaced);
                    successfulWrites.Add(outputPath);
                }

            }
            catch (Exception e) {
                result = false;
                Debug.LogError(e);
            }

            if (result == false) {
                // delete files that were successfully written
                if (successfulWrites.Count > 0) {
                    // TODO: Perform Undo
                    string filesList = string.Join('\n', successfulWrites);
                    Debug.LogWarning($"CodeGen failed, but some files were successfully written. You may want to remove these manually, or overwrite them.\n{filesList}" );
                }
                else {
                    Debug.LogWarning($"CodeGen failed, no files were written.");
                }
                return;
            }
            
            // Select the new files in the Project panel
            if (successfulWrites.Count > 0) {
                UnityEngine.Object[] writtenObjs = new UnityEngine.Object[successfulWrites.Count];
                for (int i = 0; i < writtenObjs.Length; i++) {
                    UnityEngine.Object writtenObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(successfulWrites[i]);
                    writtenObjs[i] = writtenObj;
                    EditorGUIUtility.PingObject(writtenObj);
                }
                
                string filesList = string.Join('\n', successfulWrites);
                Debug.Log($"CodeGen: Files were successfully generated. If any errors follow this message, please check that the generated code is valid:\n{filesList}", writtenObjs[^1]);

                Selection.objects = writtenObjs;
                
            }
            else {
                Debug.Log("CodeGen: No files were created");
            }
        }

        
        private void OnWizardOtherButton() {
            if (SanitizePath(templatesDirectory) == false) {
                Debug.LogError($"CodeGen template directory is not in the project directory: {Path.GetFullPath(templatesDirectory)}");
                return;
            }
            
            UpdateMacros(GetRequestedMacros(templatesDirectory));
        }
#endregion
// =====================================================================================================================
#region File Parsing

        private void UpdateMacros(Dictionary<string, HashSet<string>> requestedMacros) {
            // Remove macros that aren't requested
            macros.RemoveAll(macro => requestedMacros.ContainsKey(macro.macroName) == false);

            foreach (string macroIdentifier in requestedMacros.Keys) {
                if (macros.Exists(macro => macroIdentifier == macro.macroName)) {
                    continue;
                }

                CodeGenMacroDefinition newEntry = new() {
                    macroName = macroIdentifier
                };
                macros.Add(newEntry);
            }
            
        }

        private static Dictionary<string, HashSet<string>> GetRequestedMacros(string directory) {
            Dictionary<string, HashSet<string>> requestedMacros = new();
            
            string[] templatePaths = GetTemplatePaths(directory);
            foreach (string path in templatePaths) {
                // Load  templates into memory
                string text = File.ReadAllText(path);
                
                // Regex macros
                MatchCollection templateMacros = s_MacroRegex.Matches(text);
                foreach (Match templateMacro in templateMacros) {
                    string macroVal = templateMacro.Value;
                    int substrLength = macroVal.Length - 2; // remove $$
                    int dotPosition = macroVal.IndexOf('.');
                    if (dotPosition != -1) {
                        // There is a modifier, substring to dotPos instead,
                        // then substring from dotPos to end for the modifier
                        substrLength = dotPosition - 1;
                    }
                    
                    string macroIdentifier = macroVal.Substring(1, substrLength);
                    if (requestedMacros.ContainsKey(macroIdentifier) == false) {
                        requestedMacros.Add(macroIdentifier, new HashSet<string>());
                    }

                    if (dotPosition != -1) {
                        string modifier = macroVal.Substring(dotPosition + 1, macroVal.Length - dotPosition - 2);
                        requestedMacros[macroIdentifier].Add(modifier);
                    }
                }
            }
            return requestedMacros;
        }
        
        private static Dictionary<string, string> GetMacroReplacements(Dictionary<string, HashSet<string>> requestedMacros, List<CodeGenMacroDefinition> macroDefinitions) {
            Dictionary<string, string> macroReplacements = new();
            foreach (KeyValuePair<string, HashSet<string>> kvp in requestedMacros) {
                string baseReplacement = macroDefinitions.Find(macroDef => kvp.Key == macroDef.macroName).replaceWith;
                if (baseReplacement.Length <= 0) {
                    continue;
                }
                
                macroReplacements.Add($"${kvp.Key}$", baseReplacement);
                // Modified variants
                foreach (string modifier in kvp.Value) {
                    if (ProcessMacroModifier(baseReplacement, modifier, out string transformedReplacement)) {
                        macroReplacements.Add($"${kvp.Key}.{modifier}$", transformedReplacement);
                    }
                }
            }

            return macroReplacements;
        }
        
        private static bool ProcessMacroModifier(string macroReplacement, string macroModifier, out string transformedReplacement) {
            bool result = true;
            
            // TODO: more advanced case parsing
            switch (macroModifier) {
                case "UPPER":
                    transformedReplacement = macroReplacement.ToUpper();
                    break;
                case "LOWER":
                    transformedReplacement = macroReplacement.ToLower();
                    break;
                case "PASCAL":
                    transformedReplacement = char.ToUpper(macroReplacement[0]) + macroReplacement.Substring(1);
                    break;
                case "CAMEL":
                    transformedReplacement = char.ToLower(macroReplacement[0]) + macroReplacement.Substring(1);
                    break;
                default:
                    transformedReplacement = string.Empty;
                    result = false;
                    break;
            }
            
            return result;
        }

        private string ReplaceMacros(string template, Dictionary<string, string> macroReplacements) {
            string outputText = template;
            foreach (KeyValuePair<string, string> kvp in macroReplacements) {
                outputText = outputText.Replace(kvp.Key, kvp.Value);
            }

            return outputText;
        }
#endregion
// =====================================================================================================================
#region File System
        private static bool CreateDirectory(string localPath) {
            try {
                Directory.CreateDirectory(localPath);
            }
            catch (Exception e) {
                Debug.LogError(e.Message);
                return false;
            }
            return true;
        }

        private static bool SanitizePath(string localPath, string requiredLocalPath = ".") {
            string projectDir = Path.GetFullPath(requiredLocalPath);
            string fullPath = Path.GetFullPath(localPath);
            return fullPath.Contains(projectDir);
        }

        private static string[] GetTemplatePaths(string directory) {
            return Directory.GetFiles(directory, "**.txt");
        }
#endregion
// =====================================================================================================================
    }

    
}