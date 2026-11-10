#if UNITY_EDITOR
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal sealed class EndsSessionUndoED : ScriptableObject
    {
        #region FIELDS

        [SerializeField] private DialogDatabase _asset;
        [SerializeField] private DialogDatabase _working;
        [SerializeField] private string _scope;
        [SerializeField] private string _snapshot;

        internal DialogDatabase Asset => _asset;
        internal DialogDatabase Working => _working;
        internal string Scope => _scope;
        internal string Snapshot
        {
            get
            {
                if (string.IsNullOrEmpty(_snapshot)) return null;

                return _snapshot;
            }
            set => _snapshot = value;
        }

        #endregion FIELDS

        #region INITIALIZATION

        internal void Initialize(DialogDatabase asset, DialogDatabase working, string scope, string snapshot)
        {
            _asset = asset;
            _working = working;
            _scope = scope;
            _snapshot = snapshot;
            hideFlags = HideFlags.HideAndDontSave;
        }

        #endregion INITIALIZATION
    }
}
#endif
