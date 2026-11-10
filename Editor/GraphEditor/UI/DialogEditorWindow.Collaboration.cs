#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public partial class DialogEditorWindowED
    {
        #region FIELDS

        private const int COLLABORATION_BUTTON_WIDTH = 110;
        private static readonly GUIContent _changesContent = new GUIContent("Changes", "Open or close the changes panel. Review unsaved differences, then save or discard selected areas.");
        private string _viewedDialog;
        private string _viewedScope;
        private DialogDatabase _viewedDatabase;

        #endregion FIELDS

        #region COLLABORATION VIEW

        public override void SaveChanges()
        {
            if (database != null && isEditingDialog && !SaveDialogEditorSession("Window close - Save dialog")) return;

            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            if (database != null && isEditingDialog && !DiscardUnsavedDialogDatabaseChanges()) return;

            base.DiscardChanges();
        }

        private void RefreshViewedDialog()
        {
            if (database != _viewedDatabase || !isEditingDialog || selectedDialogName != _viewedDialog) ReleaseViewedDialog();
            if (database == null || !isEditingDialog || string.IsNullOrEmpty(selectedDialogName)) return;

            _viewedDatabase = database;
            _viewedDialog = selectedDialogName;
            _viewedScope = database.Dialogs.Find(dialog => dialog != null && dialog.dialogName == selectedDialogName)?.NodeIdentityKey;
            EndsSessionED session = EndsSessionED.ForDatabase(database);

            if (_viewedScope != null && EndsSessionED.ExclusiveEditing && !session.EditorOwnsDialog(selectedDialogName) && session.DialogOwner(selectedDialogName) == null) session.ChangeLease("acquire", new[] { selectedDialogName }, false, "Editing in Unity");
        }

        private void ReleaseViewedDialog()
        {
            if (_viewedDatabase != null && _viewedScope != null) EndsSessionED.ForDatabase(_viewedDatabase).ReleaseEditorDialog(_viewedScope);

            _viewedDatabase = null;
            _viewedDialog = null;
            _viewedScope = null;
        }

        private void DrawDialogWorkspaceTools()
        {
            if (database == null) return;

            GUILayout.FlexibleSpace();
            bool wasEnabled = GUI.enabled;
            GUI.enabled = true;

            try
            {
                if (GUILayout.Button(_changesContent, EditorStyles.toolbarButton, GUILayout.Width(COLLABORATION_BUTTON_WIDTH)))
                {
                    _showChanges = !_showChanges;
                    _changesPointerActive = false;
                    _changesKeyboardFocus = false;
                    GUI.FocusControl(null);
                    Repaint();
                    GUIUtility.ExitGUI();
                }
            }
            finally
            {
                GUI.enabled = wasEnabled;
            }
        }

        private void DrawCollaborationStatus(EndsSessionED session, DialogEntry dialog, bool readOnly)
        {
            if (readOnly)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label("Read only - " + session.DialogOwner(dialog.dialogName) + " is editing this dialog.");

                if (GUILayout.Button("Take control", GUILayout.Width(COLLABORATION_BUTTON_WIDTH))) session.ChangeLease("acquire", new[] { dialog.dialogName }, false, "Taken over in Unity", takeover: true);
                if (GUILayout.Button("Back to list", GUILayout.Width(COLLABORATION_BUTTON_WIDTH))) TryExitDialogEditorToList();

                EditorGUILayout.EndHorizontal();
            }
        }

        #endregion COLLABORATION VIEW

    }
}
#endif
