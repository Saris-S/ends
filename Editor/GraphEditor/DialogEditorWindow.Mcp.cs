#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public partial class DialogEditorWindowED
    {
        internal static DialogDatabase ActiveDatabase()
        {
            DialogEditorWindowED focused = EditorWindow.focusedWindow as DialogEditorWindowED;

            if (focused != null && focused.database != null) return focused.database;

            DialogEditorWindowED[] windows = Resources.FindObjectsOfTypeAll<DialogEditorWindowED>();

            for (int i = 0; i < windows.Length; i++)
            {
                DialogEditorWindowED window = windows[i];

                if (window == null) continue;
                if (window.database == null) continue;

                return window.database;
            }

            return LoadAssetFromPrefs<DialogDatabase>(PREFS_DIALOG_DATABASE_GUID_KEY, "DialogDatabase");
        }

        internal static DialogEditorWindowED WindowForDatabaseSlot()
        {
            DialogEditorWindowED focused = EditorWindow.focusedWindow as DialogEditorWindowED;

            if (focused != null && focused.database != null) return focused;

            DialogEditorWindowED[] windows = Resources.FindObjectsOfTypeAll<DialogEditorWindowED>();
            DialogEditorWindowED empty = focused;

            for (int i = 0; i < windows.Length; i++)
            {
                DialogEditorWindowED window = windows[i];

                if (window == null) continue;
                if (window.database != null) return window;
                if (empty == null) empty = window;
            }

            return empty;
        }

        internal bool TrySwitchDialogDatabase(DialogDatabase newDatabase, out string error)
        {
            error = null;

            if (EndsSessionED.SourceAsset(database) == EndsSessionED.SourceAsset(newDatabase)) return true;

            if (isEditingDialog)
            {
                error = DIALOG_DATABASE_SWITCH_LOCKED_TOOLTIP;
                Repaint();

                return false;
            }

            ReleaseViewedDialog();
            database = newDatabase;

            if (database != null) database = EndsSessionED.ForDatabase(database).WorkingDatabase;

            SaveAssetGuidToPrefs(PREFS_DIALOG_DATABASE_GUID_KEY, database);
            ResetDialogEditorSessionState();

            if (database != null) EnsureDialogDatabaseData(true);

            RefreshSavedDialogReferenceIndex();
            InvalidateReferenceIndex();
            EndsAiHighlightED.Bind(database);
            Repaint();

            return true;
        }

    }
}
#endif
