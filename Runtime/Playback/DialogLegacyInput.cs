using System;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>Default Input: Space / Enter / click call DialogRunner.Advance.</summary>
    public sealed class DialogLegacyInput : MonoBehaviour
    {
        #region Developer API

            /// <summary>Same as Space / Enter / click during dialog.</summary>
            public void RequestAdvance() => RequestAdvanceImpl();

        #endregion Developer API

        private DialogRunner _runner;

        private void Start()
        {
            _runner = DialogRunner.Instance;

            if (_runner == null) throw new InvalidOperationException("DialogLegacyInput: DialogRunner.Instance is missing - place DialogRunner in the scene.");
        }

        private void RequestAdvanceImpl()
        {
            if (!_runner.IsTalking) return;

            _runner.Advance();
        }

        private void Update()
        {
            if (_runner == null || !_runner.IsTalking) return;
            if (!(Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) return;

            _runner.Advance();
        }
    }
}