using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MY_PACKAGE_NAMESPACE {
    public class CodeGenWizard : ScriptableWizard {
        public const string WizardHelp = "MY_WIZARD_HELP_DIALOGUE";
        
        public const string PackageName = "MY_PACKAGE_URI";
        public const string WizardTitle = "MY_WIZARD_WINDOW_TITLE";
        public const string WizardMenuItemPath = "Tools/MY_WIZARD_MENU_ITEM";

// =====================================================================================================================
#region Variables
        public CodeGenHelpPanel helpPanel = new(WizardHelp);
        
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
        [MenuItem(WizardMenuItemPath)]
        static void CreateWizard() {
            DisplayWizard<CodeGenWizard>(WizardTitle, "Generate", "Get Macros");
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
            UpdateSerializedMacros(GetRequestedMacrosFromTemplates(templatesDirectory));
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

        // Return -1 if batch requested but is invalid,
        // 0 if no batch requested
        int GetBatchCount() {
            
            int batchCount = 0;
            foreach (CodeGenMacroDefinition macroDef in macros) {
                if (macroDef.useSingleReplacement) {
                    continue;
                }

                if (batchCount == 0) {
                    if(macroDef.replaceWithBatch.Count != 0){
                        batchCount = macroDef.replaceWithBatch.Count;
                        continue;
                    }
                }

                if (batchCount == macroDef.replaceWithBatch.Count) continue;
                
                Debug.LogError($"CodeGen: Batch generation invalid. Please make sure all batch macros have the same number of entries. Problem macro: {macroDef.macroName}"); 
                return 0;
            }

            return batchCount;
        }

        Dictionary<string, string> GetBatchReplacements(int index) {
            Dictionary<string, string> replacements = new();
            foreach (CodeGenMacroDefinition macroDef in macros) {
                if (macroDef.useSingleReplacement) {
                    replacements.Add(macroDef.macroName, macroDef.replaceWith);
                }
                else {
                    replacements.Add(macroDef.macroName, macroDef.replaceWithBatch[index]);
                }
            }

            return replacements;
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

            Dictionary<string, HashSet<string>> requestedMacros = GetRequestedMacrosFromTemplates(templatesDirectory);
            UpdateSerializedMacros(requestedMacros);

            // get max number of macro definitions
            int batchCount = GetBatchCount();
            if (batchCount == -1) return;

            for (int batchIndex = -1; batchIndex < batchCount - 1; ) {
                batchIndex++;
                Dictionary<string, string> batchDefs = GetBatchReplacements(batchIndex);
                Dictionary<string, string> macroReplacements = GetMacroReplacements(requestedMacros, batchDefs);

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
            
            // make sure all batch definitions have the same number of entries
            // loop that many times
            // populate replacement dict every time
            
        }

        
        private void OnWizardOtherButton() {
            if (SanitizePath(templatesDirectory) == false) {
                Debug.LogError($"CodeGen template directory is not in the project directory: {Path.GetFullPath(templatesDirectory)}");
                return;
            }
            
            UpdateSerializedMacros(GetRequestedMacrosFromTemplates(templatesDirectory));
        }
#endregion
// =====================================================================================================================
#region File Parsing

        /// <summary>
        /// Ensure all requested macros are displayed in the UI, and remove any macros that aren't requested.
        /// </summary>
        /// <param name="requestedMacros">Dictionary of all macros and modifiers present in the template files.</param>
        private void UpdateSerializedMacros(Dictionary<string, HashSet<string>> requestedMacros) {
            // Remove macros that aren't requested
            macros.RemoveAll(macro => requestedMacros.ContainsKey(macro.macroName) == false);

            // Add macros that are requested but don't exist
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

        /// <summary>
        /// Parse template files to find macro identifiers that need to be defined.
        /// </summary>
        /// <param name="directory">Directory containing the template files to parse.</param>
        /// <returns>Dictionary of Macro identifiers, and any modifiers that need to be calculated.</returns>
        private static Dictionary<string, HashSet<string>> GetRequestedMacrosFromTemplates(string directory) {
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
        
        /// <summary>
        /// Get the macro definitions for all macros, including modified variants.
        /// </summary>
        /// <param name="requestedMacros">Dictionary of all macro identifiers, and each requested modifier that needs to be calculated.</param>
        /// <param name="macroDefinitions">Dictionary of all macro identifiers, and the user-provided definitions.</param>
        /// <returns>Dictionary with definitions for every requested $MACRO.FORMAT$ combination.</returns>
        private static Dictionary<string, string> GetMacroReplacements(Dictionary<string, HashSet<string>> requestedMacros, Dictionary<string, string> macroDefinitions) {
            Dictionary<string, string> macroReplacements = new();
            foreach (KeyValuePair<string, HashSet<string>> kvp in requestedMacros) {
                string baseReplacement = macroDefinitions[kvp.Key];
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
        
        /// <summary>
        /// Transform a user-defined macro replacement into the format of the requested modifier.
        /// </summary>
        /// <param name="macroReplacement"></param>
        /// <param name="macroModifier"></param>
        /// <param name="transformedReplacement">Macro replacement in the specified format.</param>
        /// <returns><see langword="true"/> if transformation was successful, <see langword="false"/> otherwise.</returns>
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
                case "NAME":
                    // same as pascal, but strip all non-alphanumeric characters
                    transformedReplacement = char.ToUpper(macroReplacement[0]) + macroReplacement.Substring(1);
                    char[] replacementCharArr = transformedReplacement.ToCharArray();
                    transformedReplacement = new string(Array.FindAll(replacementCharArr, char.IsLetterOrDigit));
                    break;
                default:
                    transformedReplacement = string.Empty;
                    result = false;
                    break;
            }
            
            return result;
        }

        /// <summary>
        /// Parse a template file, replacing all $MACROS$ with their definitions.
        /// </summary>
        /// <param name="template">string from template file.</param>
        /// <param name="macroReplacements">Dictionary with definitions for every requested $MACRO.FORMAT$ combination.</param>
        /// <returns>The template string with all macros substituted.</returns>
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