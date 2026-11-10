using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>
    /// Optional settings component (not one of the five runtime modules).
    /// Holds current dialog language / locale. Language persistence is PlayerPrefs only - separate from DialogSave (save.json variables).
    /// </summary>
    public class DialogPreferences : MonoBehaviour
    {
        public const string LANGUAGE_PLAYER_PREFS_KEY = "Language";
        public const string DEFAULT_LANGUAGE_CODE = "en";

        public static DialogPreferences Instance { get; private set; }
        public static event Action<string> LanguageChanged;

        [Tooltip("If there is no translation for Current Language, this code is used.")][SerializeField] private string _fallbackLanguageCode = DEFAULT_LANGUAGE_CODE;
        [Tooltip("Language list from DialogDatabase is pulled on RegisterDialogDatabase when empty here.")][SerializeField] private List<string> _availableLanguages = new List<string>();
        [SerializeField] private string _currentLanguage = DEFAULT_LANGUAGE_CODE;

        private readonly List<DialogDatabase> _registeredDatabases = new List<DialogDatabase>();

        public string CurrentLanguage => _currentLanguage;
        public string FallbackLanguageCode => _fallbackLanguageCode;
        public IReadOnlyList<string> AvailableLanguages => _availableLanguages;
        public bool HasSavedLanguage() => PlayerPrefs.HasKey(LANGUAGE_PLAYER_PREFS_KEY);

        public static string GetFallbackLanguageCode()
        {
            if (Instance != null && !string.IsNullOrEmpty(Instance._fallbackLanguageCode)) return Instance.FallbackLanguageCode;

            return DEFAULT_LANGUAGE_CODE;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;
            LoadLanguage(logResult: false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RegisterDialogDatabase(DialogDatabase dialogDatabase)
        {
            if (dialogDatabase == null) throw new ArgumentNullException(nameof(dialogDatabase));
            if (_registeredDatabases.Contains(dialogDatabase)) return;

            _registeredDatabases.Add(dialogDatabase);

            if (_availableLanguages.Count == 0 && dialogDatabase.AvailableLanguages != null && dialogDatabase.AvailableLanguages.Count > 0) _availableLanguages = new List<string>(dialogDatabase.AvailableLanguages);

            ApplyLanguageToDatabase(dialogDatabase);
        }

        public void UnregisterDialogDatabase(DialogDatabase dialogDatabase)
        {
            if (dialogDatabase == null) throw new ArgumentNullException(nameof(dialogDatabase));

            _registeredDatabases.Remove(dialogDatabase);
        }

        /// <summary>
        /// Sets the runtime language and applies it to registered databases. Does not write PlayerPrefs - call SaveLanguage to persist.
        /// </summary>
        public bool SetLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode)) return false;

            string resolvedLanguage = ResolveLanguageCode(languageCode);

            if (_currentLanguage == resolvedLanguage)
            {
                ApplyLanguageToAllDatabases();

                return true;
            }

            _currentLanguage = resolvedLanguage;
            ApplyLanguageToAllDatabases();
            LanguageChanged?.Invoke(_currentLanguage);

            return true;
        }

        /// <summary>
        /// Writes Current Language to PlayerPrefs. Separate from DialogSave / G / save.json.
        /// </summary>
        public void SaveLanguage(bool logResult = true)
        {
            PlayerPrefs.SetString(LANGUAGE_PLAYER_PREFS_KEY, _currentLanguage);
            PlayerPrefs.Save();

            if (logResult) Debug.Log($"DialogPreferences: language saved to PlayerPrefs ('{LANGUAGE_PLAYER_PREFS_KEY}' = '{_currentLanguage}').");
        }

        /// <summary>
        /// Reads language from PlayerPrefs into Current Language (or fallback if missing). Separate from DialogSave / G / save.json.
        /// </summary>
        public void LoadLanguage(bool logResult = true)
        {
            string savedLanguage = PlayerPrefs.GetString(LANGUAGE_PLAYER_PREFS_KEY, GetFallbackLanguageCode());
            SetLanguage(savedLanguage);

            if (logResult) Debug.Log($"DialogPreferences: language loaded from PlayerPrefs ('{LANGUAGE_PLAYER_PREFS_KEY}' → '{_currentLanguage}').");
        }

        private string ResolveLanguageCode(string languageCode)
        {
            if (_availableLanguages == null || _availableLanguages.Count == 0) return languageCode;
            if (_availableLanguages.Contains(languageCode)) return languageCode;

            string fallbackLanguageCode = GetFallbackLanguageCode();

            if (_availableLanguages.Contains(fallbackLanguageCode)) return fallbackLanguageCode;

            return _availableLanguages[0];
        }

        private void ApplyLanguageToAllDatabases()
        {
            for (int i = 0; i < _registeredDatabases.Count; i++)
            {
                ApplyLanguageToDatabase(_registeredDatabases[i]);
            }
        }

        private void ApplyLanguageToDatabase(DialogDatabase dialogDatabase)
        {
            dialogDatabase.CurrentLanguage = _currentLanguage;
        }
    }
}
