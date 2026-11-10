using System;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>Starts dialog for initiator via DialogRunner.</summary>
    public class DialogActivator : MonoBehaviour
    {
        public const string INITIATOR_UNSET = "Unset";
        [Tooltip("Dialog category (initiator) used to pick a dialog from the database.")][SerializeField] private string _initiator = INITIATOR_UNSET;
        [Tooltip("Log dialog selection, condition checks, and all G variables when conversation starts.")][SerializeField] private bool _logDialogSelection;

        private DialogRunner _runner;

        public string Initiator => _initiator;

        private void Start()
        {
            _runner = DialogRunner.Instance;

            if (_runner == null) throw new InvalidOperationException($"[{name}] DialogActivator: DialogRunner.Instance is missing - place DialogRunner in the scene.");
        }

        /// <summary>Starts dialog for this activator's initiator category.</summary>
        public bool TryStartDialog()
        {
            if (_runner == null) throw new InvalidOperationException($"[{name}] DialogActivator: DialogRunner is not ready yet.");
            if (IsInitiatorUnset(_initiator)) throw new InvalidOperationException($"[{name}] DialogActivator: initiator is not selected.");
            if (_runner.IsTalking) throw new InvalidOperationException($"[{name}] DialogActivator: a dialog is already running.");

            return _runner.TryStartDialogForInitiator(_initiator, _logDialogSelection);
        }

        public static bool IsInitiatorUnset(string initiator)
        {
            if (string.IsNullOrEmpty(initiator)) return true;
            if (initiator == INITIATOR_UNSET) return true;

            return false;
        }
    }
}
