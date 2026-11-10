using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    [CustomEditor(typeof(DialogActivator))]
    public class DialogActivatorCustomInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty initiatorProperty = serializedObject.FindProperty("_initiator");
            DialogDatabase database = ResolveDialogDatabaseForEditor();

            if (initiatorProperty != null && DrawInitiatorPopup(initiatorProperty, database)) serializedObject.ApplyModifiedProperties();

            DrawPropertiesExcluding(serializedObject, "_initiator", "m_Script");
            serializedObject.ApplyModifiedProperties();

            bool initiatorSelected = initiatorProperty != null && !DialogActivator.IsInitiatorUnset(initiatorProperty.stringValue);
            bool canStartDialog = Application.isPlaying && initiatorSelected;

            EditorGUI.BeginDisabledGroup(!canStartDialog);

            if (GUILayout.Button("Start dialog"))
            {
                DialogActivator activator = (DialogActivator)target;
                activator.TryStartDialog();
            }

            EditorGUI.EndDisabledGroup();

            if (!Application.isPlaying) EditorGUILayout.HelpBox("Start dialog is available only in Play Mode.", MessageType.Info);
            else if (!initiatorSelected) EditorGUILayout.HelpBox("Select an initiator to start a dialog.", MessageType.Info);
        }

        private static DialogDatabase ResolveDialogDatabaseForEditor()
        {
            DialogRunner runner = FindAnyObjectByType<DialogRunner>();

            if (runner != null && runner.DialogDatabase != null) return runner.DialogDatabase;

            string[] guids = AssetDatabase.FindAssets("t:DialogDatabase");

            if (guids.Length == 0) return null;

            return AssetDatabase.LoadAssetAtPath<DialogDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static List<string> GetInitiatorOptions(DialogDatabase database)
        {
            List<string> options = new List<string>();

            if (database?.Categories != null && database.Categories.Count > 0) options.AddRange(database.Categories);

            if (options.Count == 0 && database?.Dialogs != null)
            {
                options = database.Dialogs
                    .Where(d => d != null && !string.IsNullOrEmpty(d.initiator))
                    .Select(d => d.initiator)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList();
            }

            return options;
        }

        private static bool DrawInitiatorPopup(SerializedProperty initiatorProperty, DialogDatabase database)
        {
            if (initiatorProperty == null) return false;

            if (database == null)
            {
                EditorGUILayout.HelpBox("Dialog Database not found. Assign one on DialogRunner or create a Dialog Database asset.", MessageType.Warning);
                return false;
            }

            List<string> initiatorOptions = GetInitiatorOptions(database);

            if (initiatorOptions.Count == 0)
            {
                EditorGUILayout.HelpBox("No initiator categories in Dialog Database. Add categories or dialogs in the Dialog Editor.", MessageType.Warning);
                return false;
            }

            List<string> displayOptions = new List<string> { "(not selected)" };
            displayOptions.AddRange(initiatorOptions);

            string current = initiatorProperty.stringValue ?? "";
            bool isUnset = DialogActivator.IsInitiatorUnset(current);

            int selectedIndex;
            if (isUnset) selectedIndex = 0;
            else selectedIndex = initiatorOptions.IndexOf(current) + 1;

            if (selectedIndex < 0) selectedIndex = 0;

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Initiator", selectedIndex, displayOptions.ToArray());

            if (!EditorGUI.EndChangeCheck()) return false;
            if (newIndex < 0 || newIndex >= displayOptions.Count) return false;

            if (newIndex == 0) initiatorProperty.stringValue = DialogActivator.INITIATOR_UNSET;
            else initiatorProperty.stringValue = initiatorOptions[newIndex - 1];

            return true;
        }
    }
}
