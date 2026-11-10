#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsLegacyNameColorMigrationED
    {
        private const string SAVE_KEY_PREFIX = "ENDS.LegacyNameColor.Pending.";
        private static readonly FieldInfo LegacyField = typeof(CharacterNameColor).GetField("_legacyColorHex", BindingFlags.Instance | BindingFlags.NonPublic);

        #region LEGACY CONVERSION

        /// <summary>ENDS. Detects legacy rows without constructing a database snapshot for modern catalogs.</summary>
        internal static bool RequiresMigration(DialogDatabase database)
        {
            foreach (CharacterNameColor entry in database.CharacterNameColors ?? new List<CharacterNameColor>())
            {
                if (entry != null && (string)LegacyField.GetValue(entry) != CharacterNameColor.LEGACY_COLOR_MIGRATED) return true;
            }

            return false;
        }

        internal static int MigrateDatabase(DialogDatabase database)
        {
            if (database?.CharacterNameColors == null) return 0;

            Dictionary<CharacterNameColor, Color> plan = new Dictionary<CharacterNameColor, Color>();

            for (int i = 0; i < database.CharacterNameColors.Count; i++)
            {
                CharacterNameColor entry = database.CharacterNameColors[i];

                if (entry == null) continue;

                string legacy = (string)LegacyField.GetValue(entry);

                if (legacy == CharacterNameColor.LEGACY_COLOR_MIGRATED) continue;

                Color color = Color.white;

                if (!string.IsNullOrEmpty(legacy) && !ColorUtility.TryParseHtmlString(legacy, out color)) throw new InvalidOperationException("Legacy color cannot be parsed at CharacterNameColors[" + i + "] (" + entry.Name + "): " + legacy + ". Set a valid color before saving or building.");

                plan[entry] = color;
            }

            foreach (KeyValuePair<CharacterNameColor, Color> item in plan)
            {
                item.Key.Color = item.Value;
                ClearLegacyColor(item.Key);
            }

            return plan.Count;
        }

        internal static void ClearLegacyColor(CharacterNameColor entry)
        {
            LegacyField.SetValue(entry, CharacterNameColor.LEGACY_COLOR_MIGRATED);
        }

        internal static string NormalizeSnapshot(string snapshot, bool migrateColors = true)
        {
            if (snapshot.IndexOf("\"ColorHex\"", StringComparison.Ordinal) < 0 && snapshot.IndexOf("\"_legacyColorHex\"", StringComparison.Ordinal) < 0) return snapshot;

            EndsMcpJsonED root = EndsMcpJsonED.Parse(snapshot);
            EndsMcpJsonED fields = root.Get("MonoBehaviour") ?? root;
            EndsMcpJsonED names = fields.Get("_characterNameColors") ?? fields.Get("CharacterNameColors");

            if (names?.Items == null) return snapshot;

            if (fields.Get("_characterNameColors") == null)
            {
                fields.Set("_characterNameColors", names);
                fields.Remove("CharacterNameColors");
            }

            bool requiresConversion = false;

            foreach (EndsMcpJsonED entry in names.Items)
            {
                if (entry?.Type != EndsMcpJsonED.Kind.Object) continue;

                EndsMcpJsonED legacy = entry.Get("_legacyColorHex");

                if (legacy != null && (legacy.Type != EndsMcpJsonED.Kind.String || legacy.Text != CharacterNameColor.LEGACY_COLOR_MIGRATED)) requiresConversion = true;
                if (entry.Get("ColorHex") == null) continue;

                entry.Set("_legacyColorHex", entry.Get("ColorHex"));
                entry.Remove("ColorHex");
                requiresConversion = true;
            }

            if (!requiresConversion) return snapshot;

            DialogDatabase copy = ScriptableObject.CreateInstance<DialogDatabase>();

            try
            {
                if (fields != root) EditorJsonUtility.FromJsonOverwrite(root.Write(), copy);
                else JsonUtility.FromJsonOverwrite(root.Write(), copy);

                try
                {
                    if (migrateColors) MigrateDatabase(copy);
                }
                catch (InvalidOperationException)
                {
                    // Keep the original legacy value available for an explicit repair.
                }

                return EditorJsonUtility.ToJson(copy);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        #endregion LEGACY CONVERSION

        #region PERSISTENCE AND BUILD

        internal static void RequireConverted(DialogDatabase database)
        {
            if (database.CharacterNameColors == null) return;

            for (int i = 0; i < database.CharacterNameColors.Count; i++)
            {
                CharacterNameColor entry = database.CharacterNameColors[i];

                if (entry != null && (string)LegacyField.GetValue(entry) != CharacterNameColor.LEGACY_COLOR_MIGRATED) throw new InvalidOperationException("Repair the legacy color at CharacterNameColors[" + i + "] in ENDS Names before saving or building.");
            }
        }

        internal static void MarkNeedsSave(DialogDatabase database)
        {
            string key = GetSaveKey(database);

            if (key != null) SessionState.SetBool(key, true);
        }

        internal static bool NeedsSave(DialogDatabase database)
        {
            string key = GetSaveKey(database);

            return key != null && SessionState.GetBool(key, false);
        }

        internal static void ConfirmSaved(DialogDatabase database)
        {
            string key = GetSaveKey(database);

            if (key != null) SessionState.EraseBool(key);
        }

        private static string GetSaveKey(DialogDatabase database)
        {
            string path = EndsSessionED.AssetPath(database);

            if (string.IsNullOrEmpty(path)) return null;

            return SAVE_KEY_PREFIX + AssetDatabase.AssetPathToGUID(path);
        }

        internal sealed class EndsLegacyNameColorBuildED : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            /// <summary> Prevents shipping databases whose legacy colors were not converted and saved. </summary>
            public void OnPreprocessBuild(BuildReport report)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:DialogDatabase"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    DialogDatabase database = AssetDatabase.LoadAssetAtPath<DialogDatabase>(path);
                    if (NeedsSave(database)) throw new BuildFailedException("Save the migrated ENDS database before building: " + path);

                    try
                    {
                        RequireConverted(database);
                    }
                    catch (InvalidOperationException exception)
                    {
                        throw new BuildFailedException(path + ": " + exception.Message);
                    }
                }
            }
        }

        #endregion PERSISTENCE AND BUILD
    }
}
#endif