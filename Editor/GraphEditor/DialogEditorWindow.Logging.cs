#if UNITY_EDITOR
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public partial class DialogEditorWindowED
    {
        private const string DIALOG_EDITOR_LOG_TITLE_COLOR = "#FFD166";
        private const string DIALOG_EDITOR_LOG_LABEL_COLOR = "#7EC8E3";
        private const string DIALOG_EDITOR_LOG_VALUE_COLOR = "#E8E8E8";
        private const string DIALOG_EDITOR_LOG_STATUS_YES_COLOR = "#6EE787";
        private const string DIALOG_EDITOR_LOG_STATUS_NO_COLOR = "#FF7B72";

        private static string GetDialogDatabaseAssetLabel(DialogDatabase dialogDatabase)
        {
            if (dialogDatabase == null) return "null";

            return dialogDatabase.name ?? "DialogDatabase";
        }

        private static string FormatDialogEditorLogTitle(string title)
        {
            return "<b><color=" + DIALOG_EDITOR_LOG_TITLE_COLOR + ">" + title + "</color></b>";
        }

        private static string FormatDialogEditorLogDetail(string label, string value)
        {
            return "<color=" + DIALOG_EDITOR_LOG_LABEL_COLOR + ">" + label + ":</color> " + "<color=" + DIALOG_EDITOR_LOG_VALUE_COLOR + ">" + value + "</color>";
        }

        private static string FormatDialogEditorLogDetailRich(string label, string richValue)
        {
            return "<color=" + DIALOG_EDITOR_LOG_LABEL_COLOR + ">" + label + ":</color> " + richValue;
        }

        private static string FormatDialogEditorLogStatus(string text, string colorHex, bool bold = true)
        {
            if (bold) return "<color=" + colorHex + "><b>" + text + "</b></color>";

            return "<color=" + colorHex + ">" + text + "</color>";
        }

        private static string FormatDialogEditorGraphSessionState(bool graphSessionOpen)
        {
            if (graphSessionOpen) return FormatDialogEditorLogStatus("Open", DIALOG_EDITOR_LOG_STATUS_YES_COLOR);

            return FormatDialogEditorLogStatus("Closed", DIALOG_EDITOR_LOG_STATUS_NO_COLOR);
        }

        private void LogDialogEditorSave(string logTrigger)
        {
            if (!_dialogEditorSaveLogsEnabled) return;

            string assetLabel = GetDialogDatabaseAssetLabel(database);
            string savedArea = "Entire database";

            if (isEditingDialog) savedArea = "Dialog: " + selectedDialogName;

            string message =
                FormatDialogEditorLogTitle("[Dialog Editor] Saved") + "\n\n" +
                FormatDialogEditorLogDetail("Asset", assetLabel) + "\n" +
                FormatDialogEditorLogDetail("Trigger", logTrigger) + "\n" +
                FormatDialogEditorLogDetail("Saved area", savedArea) + "\n" +
                FormatDialogEditorLogDetailRich("Graph session", FormatDialogEditorGraphSessionState(isEditingDialog)) + "\n";

            Debug.Log(message);
        }

        private void LogDialogEditorDiscard(string restoredDialogName)
        {
            if (!_dialogEditorSaveLogsEnabled) return;

            string assetLabel = GetDialogDatabaseAssetLabel(database);
            string dialogLabel = string.IsNullOrEmpty(restoredDialogName) ? "All dialogs" : restoredDialogName;

            string message =
                FormatDialogEditorLogTitle("[Dialog Editor] Discarded unsaved graph edits") + "\n\n" +
                FormatDialogEditorLogDetail("Asset", assetLabel) + "\n" +
                FormatDialogEditorLogDetail("Graph rollback", dialogLabel) + "\n" +
                FormatDialogEditorLogDetailRich("Graph session", FormatDialogEditorGraphSessionState(isEditingDialog)) + "\n";

            Debug.Log(message);
        }
    }
}
#endif
