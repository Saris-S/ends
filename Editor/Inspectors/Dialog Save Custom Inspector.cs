using System.IO;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    [CustomEditor(typeof(DialogSave))]
    public class DialogSaveCustomInspector : UnityEditor.Editor
    {
        private const int SECTION_PADDING = 5;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(SECTION_PADDING);
            EditorGUILayout.LabelField("Variables", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Dialog variables use DialogSave → save.json.", MessageType.None);

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);

            if (GUILayout.Button("Save")) G.SaveGame();
            if (GUILayout.Button("Load")) G.LoadGame();

            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Open (save.json)")) OpenSaveJson(G.GetSavePath());

            EditorGUILayout.EndHorizontal();

            if (!Application.isPlaying) EditorGUILayout.HelpBox("Save/Load is available only in Play Mode. Open works anytime.", MessageType.Info);
        }

        private static void OpenSaveJson(string savePath)
        {
            string folderPath = Path.GetDirectoryName(savePath);

            if (File.Exists(savePath))
            {
                EditorUtility.RevealInFinder(savePath);

                return;
            }

            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                EditorUtility.RevealInFinder(folderPath);
                Debug.Log($"save.json not found yet. Opened folder: {folderPath}");

                return;
            }

            Debug.LogWarning($"Save path does not exist yet: {savePath}");
        }
    }
}
