using System;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>Save module: G variables and save.json from Runner database schema.</summary>
    public sealed class DialogSave : MonoBehaviour
    {
        #region Developer API

            // Optional persistence for G variables. Runner does not reference this class.

            #region Save - variables file

                /// <summary>Full path to default save.json.</summary>
                public string SavePath => G.GetSavePath();

                /// <summary>Set load-on-start and auto-save flags. Load-on-start is read in Start only. Auto-save subscription updates immediately when already started.</summary>
                public void Configure(bool loadSavedVariablesOnStart, bool autoSaveOnDialogEnded = false) => ConfigureImpl(loadSavedVariablesOnStart, autoSaveOnDialogEnded);

                /// <summary>Reset G from DialogDatabase variable defaults.</summary>
                public void InitializeVariables() => InitializeVariablesImpl();

                /// <summary>Load G from disk; missing file leaves G unchanged.</summary>
                public void Load(string filePath = null) => G.LoadGame(filePath);

                /// <summary>Save current G to disk.</summary>
                public void Save(string filePath = null, bool logResult = true) => G.SaveGame(filePath, logResult);

            #endregion Save - variables file

        #endregion Developer API

        [Tooltip("After Initialize, load save.json from persistentDataPath if the file exists.")][SerializeField] private bool _loadSavedVariablesOnStart = true;
        [Tooltip("When enabled, saves G variables whenever DialogRunner fires DialogEnded.")][SerializeField] private bool _autoSaveOnDialogEnded;

        private bool _started;
        private DialogRunner _runner;

        private void Start()
        {
            _runner = DialogRunner.Instance;

            if (_runner == null) throw new InvalidOperationException("DialogSave: DialogRunner.Instance is missing - place DialogRunner in the scene.");

            DialogDatabase database = _runner.DialogDatabase;

            if (database == null) throw new InvalidOperationException("DialogSave: DialogRunner has no DialogDatabase assigned.");

            G.EnsureInitialized(database);

            if (_loadSavedVariablesOnStart) Load();
            if (_autoSaveOnDialogEnded)
            {
                UnsubscribeRunner();
                SubscribeRunner();
            }

            _started = true;
            ApplyAutoSaveSubscription();
        }

        private void OnEnable()
        {
            if (!_started) return;

            ApplyAutoSaveSubscription();
        }

        private void OnDisable()
        {
            if (!_started) return;

            UnsubscribeRunner();
        }

        private void ApplyAutoSaveSubscription()
        {
            UnsubscribeRunner();

            if (_autoSaveOnDialogEnded) SubscribeRunner();
        }

        private void SubscribeRunner()
        {
            _runner.DialogEnded += HandleDialogEnded;
        }

        private void UnsubscribeRunner()
        {
            if (_runner == null) return;

            _runner.DialogEnded -= HandleDialogEnded;
        }

        private void HandleDialogEnded(DialogSessionInfo info) => Save();

        private void ConfigureImpl(bool loadSavedVariablesOnStart, bool autoSaveOnDialogEnded)
        {
            _loadSavedVariablesOnStart = loadSavedVariablesOnStart;
            _autoSaveOnDialogEnded = autoSaveOnDialogEnded;

            if (!_started) return;

            ApplyAutoSaveSubscription();
        }

        private void InitializeVariablesImpl()
        {
            DialogDatabase database = _runner.DialogDatabase;

            if (database == null) throw new InvalidOperationException("DialogSave: DialogRunner has no DialogDatabase assigned.");

            G.InitializeFromDefinitions(database);
        }
    }
}
