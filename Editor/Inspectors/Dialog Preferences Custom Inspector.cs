using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    [CustomEditor(typeof(DialogPreferences))]
    public class DialogPreferencesCustomInspector : UnityEditor.Editor
    {
        private const int SECTION_PADDING = 5;

        private const string PROP_CURRENT_LANGUAGE = "_currentLanguage";
        private const string PROP_FALLBACK_LANGUAGE = "_fallbackLanguageCode";
        private const string PROP_AVAILABLE_LANGUAGES = "_availableLanguages";

        private List<string> _languageOptions;

        private void OnEnable()
        {
            RefreshLanguageOptions();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, PROP_CURRENT_LANGUAGE, PROP_FALLBACK_LANGUAGE, PROP_AVAILABLE_LANGUAGES);
            RefreshLanguageOptions();
            DialogPreferences preferences = (DialogPreferences)target;

            DrawLanguagePopup("Current Language", preferences.CurrentLanguage, PROP_CURRENT_LANGUAGE, languageCode =>
            {
                if (Application.isPlaying) preferences.SetLanguage(languageCode);
            });

            DrawLanguagePopup("Fallback Language", preferences.FallbackLanguageCode, PROP_FALLBACK_LANGUAGE, null);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(SECTION_PADDING);
            EditorGUILayout.LabelField("Language PlayerPrefs", EditorStyles.boldLabel);
            string savedLanguage;

            if (preferences.HasSavedLanguage()) savedLanguage = PlayerPrefs.GetString(DialogPreferences.LANGUAGE_PLAYER_PREFS_KEY);
            else savedLanguage = "(none)";

            EditorGUILayout.HelpBox($"Language is stored in PlayerPrefs only. Current saved Language: {savedLanguage}", MessageType.None);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save language"))
            {
                if (Application.isPlaying) preferences.SaveLanguage();
                else
                {
                    string language = serializedObject.FindProperty(PROP_CURRENT_LANGUAGE).stringValue;
                    PlayerPrefs.SetString(DialogPreferences.LANGUAGE_PLAYER_PREFS_KEY, language);
                    PlayerPrefs.Save();
                    Debug.Log($"DialogPreferences: language saved to PlayerPrefs ('{DialogPreferences.LANGUAGE_PLAYER_PREFS_KEY}' = '{language}').");
                }
            }

            if (GUILayout.Button("Load language"))
            {
                if (Application.isPlaying) preferences.LoadLanguage();
                else
                {
                    string language = PlayerPrefs.GetString(DialogPreferences.LANGUAGE_PLAYER_PREFS_KEY, DialogPreferences.DEFAULT_LANGUAGE_CODE);
                    serializedObject.FindProperty(PROP_CURRENT_LANGUAGE).stringValue = language;
                    serializedObject.ApplyModifiedProperties();
                    Debug.Log($"DialogPreferences: language loaded from PlayerPrefs ('{DialogPreferences.LANGUAGE_PLAYER_PREFS_KEY}' → '{language}').");
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLanguagePopup(string label, string currentLanguage, string serializedPropertyName, System.Action<string> onPlayModeChanged)
        {
            if (_languageOptions == null || _languageOptions.Count == 0)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Popup(label, 0, new[] { "No languages" });
                EditorGUI.EndDisabledGroup();

                return;
            }

            int selectedIndex = _languageOptions.IndexOf(currentLanguage);

            if (selectedIndex < 0) selectedIndex = 0;

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, _languageOptions.ToArray());

            if (!EditorGUI.EndChangeCheck() || newIndex < 0 || newIndex >= _languageOptions.Count) return;

            string newLanguage = _languageOptions[newIndex];

            if (Application.isPlaying && onPlayModeChanged != null) onPlayModeChanged(newLanguage);
            else serializedObject.FindProperty(serializedPropertyName).stringValue = newLanguage;
        }

        private void RefreshLanguageOptions()
        {
            _languageOptions = new List<string>();
            DialogRunner runner = Object.FindAnyObjectByType<DialogRunner>();

            if (runner != null && runner.DialogDatabase != null && runner.DialogDatabase.AvailableLanguages != null) _languageOptions.AddRange(runner.DialogDatabase.AvailableLanguages.Where(l => !string.IsNullOrEmpty(l)));

            if (_languageOptions.Count == 0)
            {
                string[] guids = AssetDatabase.FindAssets("t:DialogDatabase");

                if (guids.Length > 0)
                {
                    DialogDatabase database = AssetDatabase.LoadAssetAtPath<DialogDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));

                    if (database?.AvailableLanguages != null) _languageOptions.AddRange(database.AvailableLanguages.Where(l => !string.IsNullOrEmpty(l)));
                }
            }
        }
    }
}
