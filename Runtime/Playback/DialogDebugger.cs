using System;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>Optional dev module: formats Runner diagnostics for console and/or text bar. Subscribes to Runner. Runner does not reference this class.</summary>
    public class DialogDebugger : MonoBehaviour
    {
        #region Constants

        private const string NO_MATCHING_DIALOG_MESSAGE_FORMAT = "No matching dialog for \"{0}\".";

        #endregion Constants

        [Tooltip("Write formatted diagnostics to the Unity Console.")][SerializeField] private bool _logToConsole = true;
        [Tooltip("Show formatted diagnostics on the View text bar (StartFailed before dialog. Other kinds during dialog via diagnostic line).")][SerializeField] private bool _showOnTextBar = true;

        private bool _started = false;
        private DialogRunner _runner;

        private void Start()
        {
            _runner = DialogRunner.Instance;

            if (_runner == null)
            {
                Debug.LogWarning("DialogDebugger: DialogRunner.Instance is missing - diagnostics disabled.", this);
                return;
            }

            UnsubscribeRunner();
            SubscribeRunner();
            _started = true;
        }

        private void OnEnable()
        {
            if (!_started) return;

            UnsubscribeRunner();
            SubscribeRunner();
        }

        private void OnDisable()
        {
            if (!_started) return;

            UnsubscribeRunner();
        }

        private void SubscribeRunner()
        {
            _runner.DiagnosticReported += HandleDiagnosticReported;
        }

        private void UnsubscribeRunner()
        {
            _runner.DiagnosticReported -= HandleDiagnosticReported;
        }

        private void HandleDiagnosticReported(DialogDiagnosticReport report)
        {
            if (report == null) return;

            string message = FormatMessage(report);

            if (string.IsNullOrEmpty(message)) return;
            if (_logToConsole) Debug.LogWarning(message, this);
            if (!_showOnTextBar) return;

            DialogWindow view = DialogWindow.Instance;

            if (view == null) return;

            if (report.Kind == DialogDiagnosticKind.StartFailed)
            {
                if (_runner.IsTalking) return;

                view.ShowTextBarMessage(message);

                return;
            }

            if (!_runner.IsTalking) return;

            view.ShowDiagnosticLineDuringDialog(message);
        }

        private static string FormatMessage(DialogDiagnosticReport report)
        {
            string context = report.Context ?? "";

            if (report.Kind == DialogDiagnosticKind.StartFailed)
            {
                if (string.IsNullOrEmpty(context)) context = "?";

                return string.Format(NO_MATCHING_DIALOG_MESSAGE_FORMAT, context);
            }

            if (report.Kind == DialogDiagnosticKind.MissingNode) return $"Node with ID {context} not found!";
            if (report.Kind == DialogDiagnosticKind.MissingLineData) return $"Line data is null for node {context}";
            if (report.Kind == DialogDiagnosticKind.EmptyChoices) return "No available choices for choice node!";
            if (report.Kind == DialogDiagnosticKind.TransitionLimitExceeded) return $"DialogRunner: automatic transition limit exceeded, dialog cancelled. Check automatic loops. {context}";
            if (report.Kind == DialogDiagnosticKind.InvalidRedirect)
            {
                if (context == "invalid_node") return "DialogRunner: invalid redirect node.";
                if (context == "empty_data") return "DialogRunner: redirect node data is empty.";
                if (context == "empty_target_name") return "DialogRunner: redirect target dialog name is empty.";
                if (context.StartsWith("missing_start:")) return $"DialogRunner: start node not found in '{context.Substring("missing_start:".Length)}'.";
                if (context.StartsWith("missing_start_data:")) return $"DialogRunner: start node data is missing in '{context.Substring("missing_start_data:".Length)}'.";
                if (context.StartsWith("no_start_link:")) return $"DialogRunner: start node has no outgoing link in '{context.Substring("no_start_link:".Length)}'.";
                if (context.StartsWith("removed:")) return $"DialogRunner: target dialog was removed ({context.Substring("removed:".Length)}).";

                return "DialogRunner: invalid redirect.";
            }

            if (report.Kind == DialogDiagnosticKind.MissingReference)
            {
                if (context.StartsWith("consequence:")) return $"ApplyChoiceConsequences: variable was removed ({context.Substring("consequence:".Length)}).";

                return $"DialogRunner: variable was removed ({context}).";
            }

            if (report.Kind == DialogDiagnosticKind.InvalidCondition)
            {
                if (context == "empty") return "DialogRunner: empty condition.";
                if (context == "empty_variable") return "DialogRunner: empty variable name in condition.";
                if (context == "empty_choice") return "DialogRunner: empty choice condition.";
                if (context == "empty_choice_variable") return "DialogRunner: empty variable name in choice condition.";
                if (context.StartsWith("unknown_type:")) return $"DialogRunner: unknown condition type {context.Substring("unknown_type:".Length)}.";

                return "DialogRunner: invalid condition.";
            }

            return "";
        }
    }
}