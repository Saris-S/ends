using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary> Runtime storage for dialog system variables and save.json. </summary>
    public static class G
    {
        private const int CURRENT_SAVE_VERSION = 1;

        private static readonly Dictionary<string, bool> boolVariables = new Dictionary<string, bool>();
        private static readonly Dictionary<string, int> intVariables = new Dictionary<string, int>();
        private static readonly Dictionary<string, float> floatVariables = new Dictionary<string, float>();
        private static readonly Dictionary<string, string> stringVariables = new Dictionary<string, string>();

        /// <summary> The database whose definitions initialized the current runtime store. </summary>
        public static DialogDatabase DefinitionSource { get; private set; }

        /// <summary> Outcome of an explicit variables file operation. Missing files leave current values unchanged. </summary>
        public sealed class FileResult
        {
            public string Path { get; internal set; }
            public bool Success { get; internal set; }
            public bool Missing { get; internal set; }
            public int Version { get; internal set; }
            public string Error { get; internal set; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            DefinitionSource = null;
            boolVariables.Clear();
            intVariables.Clear();
            floatVariables.Clear();
            stringVariables.Clear();
        }

        /// <summary> Initialize defaults once for this database without resetting values already initialized by the runner. </summary>
        public static void EnsureInitialized(DialogDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (DefinitionSource == database) return;

            InitializeFromDefinitions(database);
        }

        public static void SetBool(string name, bool value)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Variable name cannot be empty.", nameof(name));

            boolVariables[name] = value;
        }

        public static void SetInt(string name, int value)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Variable name cannot be empty.", nameof(name));

            intVariables[name] = value;
        }

        public static void SetFloat(string name, float value)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Variable name cannot be empty.", nameof(name));

            floatVariables[name] = value;
        }

        public static void SetString(string name, string value)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Variable name cannot be empty.", nameof(name));

            stringVariables[name] = value;
        }

        /// <summary> Validate definitions and replace current values with database defaults. </summary>
        public static void InitializeFromDefinitions(DialogDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            IReadOnlyList<VariableDefinition> definitions = database.Variables;

            if (definitions == null) throw new InvalidOperationException("DialogDatabase.Variables is null.");

            foreach (VariableDefinition definition in definitions)
            {
                if (definition == null || string.IsNullOrEmpty(definition.variableName)) throw new InvalidOperationException("DialogDatabase has a null variable definition or empty variableName.");
                if (!Enum.IsDefined(typeof(ConditionVariableType), definition.variableType)) throw new InvalidOperationException("Variable type is not supported.");
            }

            boolVariables.Clear();
            intVariables.Clear();
            floatVariables.Clear();
            stringVariables.Clear();

            foreach (VariableDefinition def in definitions)
            {
                if (def.variableType == ConditionVariableType.Bool) boolVariables[def.variableName] = def.boolDefault;
                else if (def.variableType == ConditionVariableType.Int) intVariables[def.variableName] = def.intDefault;
                else if (def.variableType == ConditionVariableType.Float) floatVariables[def.variableName] = def.floatDefault;
                else if (def.variableType == ConditionVariableType.String) stringVariables[def.variableName] = def.stringDefault ?? "";
            }

            DefinitionSource = database;
        }

        public static string GetSavePath() => Path.Combine(Application.persistentDataPath, "save.json");

        /// <summary> Load known variables and log the outcome; use TryLoadGame when the caller needs a result. </summary>
        public static void LoadGame(string filePath = null)
        {
            FileResult result = TryLoadGame(filePath);

            if (!result.Success) Debug.LogError($"Load error: {result.Error}. Keeping current G values.");
            else if (result.Missing) Debug.Log($"Save file not found ({result.Path}). Using default values from SO.");
            else Debug.Log($"Game loaded from {result.Path} (save version {result.Version}).");
        }

        /// <summary> Load known variables only. Invalid files leave the entire runtime store unchanged. </summary>
        public static FileResult TryLoadGame(string filePath = null)
        {
            FileResult result = new FileResult { Path = string.IsNullOrEmpty(filePath) ? GetSavePath() : filePath };

            try
            {
                if (!File.Exists(result.Path))
                {
                    result.Success = true;
                    result.Missing = true;

                    return result;
                }

                string json = File.ReadAllText(result.Path);
                var data = JsonUtility.FromJson<SaveData>(json);

                if (data == null) throw new InvalidOperationException($"Save file is empty or invalid JSON: {result.Path}");

                int loadedVersion = data.saveVersion <= 0 ? 1 : data.saveVersion;

                if (loadedVersion > CURRENT_SAVE_VERSION) Debug.LogWarning($"G: save.json version {loadedVersion} is newer than supported {CURRENT_SAVE_VERSION}. Loading known fields only.");

                ValidateSaveData(data);
                ApplyLoadedSaveData(data);
                result.Version = loadedVersion;
                result.Success = true;

            }
            catch (Exception e)
            {
                result.Error = e.Message;
            }

            return result;
        }

        private static void ValidateSaveData(SaveData data)
        {
            if (data.boolVars != null && data.boolVars.Any(entry => entry == null)) throw new InvalidOperationException("Save file contains a null Bool entry.");
            if (data.intVars != null && data.intVars.Any(entry => entry == null)) throw new InvalidOperationException("Save file contains a null Int entry.");
            if (data.floatVars != null && data.floatVars.Any(entry => entry == null || float.IsNaN(entry.v) || float.IsInfinity(entry.v))) throw new InvalidOperationException("Save file contains an invalid Float entry.");
            if (data.stringVars != null && data.stringVars.Any(entry => entry == null)) throw new InvalidOperationException("Save file contains a null String entry.");
        }

        private static void ApplyLoadedSaveData(SaveData data)
        {
            if (data.boolVars != null)
            {
                foreach (SaveData.BoolEntry e in data.boolVars)
                {
                    if (string.IsNullOrEmpty(e.k)) continue;
                    if (!boolVariables.ContainsKey(e.k)) continue;

                    boolVariables[e.k] = e.v;
                }
            }

            if (data.intVars != null)
            {
                foreach (SaveData.IntEntry e in data.intVars)
                {
                    if (string.IsNullOrEmpty(e.k)) continue;
                    if (!intVariables.ContainsKey(e.k)) continue;

                    intVariables[e.k] = e.v;
                }
            }

            if (data.floatVars != null)
            {
                foreach (SaveData.FloatEntry e in data.floatVars)
                {
                    if (string.IsNullOrEmpty(e.k)) continue;
                    if (!floatVariables.ContainsKey(e.k)) continue;

                    floatVariables[e.k] = e.v;
                }
            }

            if (data.stringVars != null)
            {
                foreach (SaveData.StringEntry e in data.stringVars)
                {
                    if (string.IsNullOrEmpty(e.k)) continue;
                    if (!stringVariables.ContainsKey(e.k)) continue;

                    stringVariables[e.k] = e.v ?? "";
                }
            }
        }

        /// <summary> Save current variables and log failures; use TrySaveGame when the caller needs a result. </summary>
        public static void SaveGame(string filePath = null, bool logResult = true)
        {
            FileResult result = TrySaveGame(filePath);

            if (!result.Success) Debug.LogError($"Save error: {result.Error}");
            else if (logResult) Debug.Log($"Saving variables: {result.Path}");
        }

        /// <summary> Write current variables atomically and return failures to the caller. </summary>
        public static FileResult TrySaveGame(string filePath = null)
        {
            FileResult result = new FileResult { Path = string.IsNullOrEmpty(filePath) ? GetSavePath() : filePath };

            try
            {
                var data = new SaveData
                {
                    saveVersion = CURRENT_SAVE_VERSION,
                    boolVars = boolVariables.Select(kv => new SaveData.BoolEntry { k = kv.Key, v = kv.Value }).ToList(),
                    intVars = intVariables.Select(kv => new SaveData.IntEntry { k = kv.Key, v = kv.Value }).ToList(),
                    floatVars = floatVariables.Select(kv => new SaveData.FloatEntry { k = kv.Key, v = kv.Value }).ToList(),
                    stringVars = stringVariables.Select(kv => new SaveData.StringEntry { k = kv.Key, v = kv.Value ?? "" }).ToList()
                };
                ValidateSaveData(data);
                string json = JsonUtility.ToJson(data, true);
                WriteAllTextAtomic(result.Path, json);
                result.Success = true;
                result.Version = CURRENT_SAVE_VERSION;
            }
            catch (Exception e)
            {
                result.Error = e.Message;
            }

            return result;
        }

        private static void WriteAllTextAtomic(string filePath, string contents)
        {
            string directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, contents);

            if (File.Exists(filePath)) File.Replace(tempPath, filePath, null);
            else File.Move(tempPath, filePath);
        }

        public static void RenameVariable(string oldName, string newName, ConditionVariableType type)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName) || oldName == newName) return;

            switch (type)
            {
                case ConditionVariableType.Bool:
                    if (boolVariables.TryGetValue(oldName, out bool boolValue))
                    {
                        boolVariables[newName] = boolValue;
                        boolVariables.Remove(oldName);
                    }

                    break;
                case ConditionVariableType.Int:
                    if (intVariables.TryGetValue(oldName, out int intValue))
                    {
                        intVariables[newName] = intValue;
                        intVariables.Remove(oldName);
                    }

                    break;
                case ConditionVariableType.Float:
                    if (floatVariables.TryGetValue(oldName, out float floatValue))
                    {
                        floatVariables[newName] = floatValue;
                        floatVariables.Remove(oldName);
                    }

                    break;
                case ConditionVariableType.String:
                    if (stringVariables.TryGetValue(oldName, out string stringValue))
                    {
                        stringVariables[newName] = stringValue;
                        stringVariables.Remove(oldName);
                    }

                    break;
            }
        }

        public static void RemoveVariable(string name, ConditionVariableType type)
        {
            if (string.IsNullOrEmpty(name)) return;

            switch (type)
            {
                case ConditionVariableType.Bool:
                    boolVariables.Remove(name);
                    break;
                case ConditionVariableType.Int:
                    intVariables.Remove(name);
                    break;
                case ConditionVariableType.Float:
                    floatVariables.Remove(name);
                    break;
                case ConditionVariableType.String:
                    stringVariables.Remove(name);
                    break;
            }
        }

        public static void SetVariableFromDefinition(VariableDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.variableName)) return;

            if (def.variableType == ConditionVariableType.Bool) SetBool(def.variableName, def.boolDefault);
            else if (def.variableType == ConditionVariableType.Int) SetInt(def.variableName, def.intDefault);
            else if (def.variableType == ConditionVariableType.Float) SetFloat(def.variableName, def.floatDefault);
            else if (def.variableType == ConditionVariableType.String) SetString(def.variableName, def.stringDefault ?? "");
        }

        [Serializable]
        private class SaveData
        {
            [Serializable] public class BoolEntry { public string k; public bool v; }
            [Serializable] public class IntEntry { public string k; public int v; }
            [Serializable] public class FloatEntry { public string k; public float v; }
            [Serializable] public class StringEntry { public string k; public string v; }

            public int saveVersion;
            public List<BoolEntry> boolVars;
            public List<IntEntry> intVars;
            public List<FloatEntry> floatVars;
            public List<StringEntry> stringVars;
        }

        public static bool GetBool(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Variable name cannot be empty.", nameof(name));

            return boolVariables.TryGetValue(name, out bool v) && v;
        }

        public static int GetInt(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Variable name cannot be empty.", nameof(name));

            return intVariables.TryGetValue(name, out int v) ? v : 0;
        }

        public static float GetFloat(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Variable name cannot be empty.", nameof(name));

            return floatVariables.TryGetValue(name, out float v) ? v : 0f;
        }

        public static string GetString(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Variable name cannot be empty.", nameof(name));

            return stringVariables.TryGetValue(name, out string v) ? v : "";
        }

        public static string[] GetBoolVariableNames() => boolVariables.Keys.ToArray();
        public static string[] GetIntVariableNames() => intVariables.Keys.ToArray();
        public static string[] GetFloatVariableNames() => floatVariables.Keys.ToArray();
        public static string[] GetStringVariableNames() => stringVariables.Keys.ToArray();
    }
}
