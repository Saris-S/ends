#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsPopupLayoutED
    {
        internal const int CATEGORY_WIDTH = 340;
        internal const int CATEGORY_HEIGHT = 400;
        internal const int BUTTON_ADD_WIDTH = 80;
        internal const int ROW_ACTION_BUTTON_WIDTH = 64;
        internal const int ROW_BUTTON_HEIGHT = 20;
        internal const int HEADER_HEIGHT = 24;
        internal const int CLOSE_BUTTON_SIZE = HEADER_HEIGHT;
        internal const int CLOSE_BUTTON_FONT_SIZE = 13;
        internal const int HEADER_TITLE_FONT_SIZE = 12;
        internal const int CONTENT_TOP_PADDING = 5;
        internal const int EDITOR_SPACE_MEDIUM = 10;
        internal const int CONTENT_SIDE_PADDING = 5;
        internal const int HEADER_SEPARATOR_HEIGHT = 1;
        internal static readonly Color DELETE_BUTTON_COLOR = new Color(1f, 0.7f, 0.7f);
        private const int CLOSE_BUTTON_BORDER = 2;
        private static readonly GUIContent _closeContent = new GUIContent("×", "Close");
        private static GUIStyle _titleStyle;
        private static GUIStyle _closeStyle;
        private static GUISkin _headerSkin;
        private static GUIStyle _headerLabelSource;
        private static GUIStyle _headerButtonSource;
        private static bool _headerProSkin;

        internal static void BeginContentPadding()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(CONTENT_SIDE_PADDING);
            EditorGUILayout.BeginVertical();
        }

        internal static void EndContentPadding()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(CONTENT_SIDE_PADDING);
            EditorGUILayout.EndHorizontal();
        }

        internal static void DrawTitleHeader(string title, Action onClose)
        {
            Rect headerRect = GUILayoutUtility.GetRect(0, HEADER_HEIGHT, GUILayout.ExpandWidth(true));

            Color headerBg;
            Color separatorColor;

            if (EditorGUIUtility.isProSkin)
            {
                headerBg = new Color(0.22f, 0.22f, 0.22f, 1f);
                separatorColor = new Color(0f, 0f, 0f, 0.45f);
            }
            else
            {
                headerBg = new Color(0.76f, 0.76f, 0.76f, 1f);
                separatorColor = new Color(0f, 0f, 0f, 0.2f);
            }

            EditorGUI.DrawRect(headerRect, headerBg);
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.yMax - HEADER_SEPARATOR_HEIGHT, headerRect.width, HEADER_SEPARATOR_HEIGHT), separatorColor);

            float closeSize = CLOSE_BUTTON_SIZE;
            Rect closeRect = new Rect(headerRect.xMax - closeSize, headerRect.y, closeSize, closeSize);
            Rect titleRect = new Rect(headerRect.x + closeSize, headerRect.y, headerRect.width - closeSize * 2, headerRect.height);

            if (_titleStyle == null || _closeStyle == null || _headerSkin != GUI.skin || _headerLabelSource != EditorStyles.boldLabel || _headerButtonSource != GUI.skin.button || _headerProSkin != EditorGUIUtility.isProSkin)
            {
                _titleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = HEADER_TITLE_FONT_SIZE, padding = new RectOffset(0, 0, 0, 0) };
                _closeStyle = new GUIStyle(GUI.skin.button) { fontSize = CLOSE_BUTTON_FONT_SIZE, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0), border = new RectOffset(CLOSE_BUTTON_BORDER, CLOSE_BUTTON_BORDER, CLOSE_BUTTON_BORDER, CLOSE_BUTTON_BORDER) };
                _headerSkin = GUI.skin;
                _headerLabelSource = EditorStyles.boldLabel;
                _headerButtonSource = GUI.skin.button;
                _headerProSkin = EditorGUIUtility.isProSkin;
            }

            GUI.Label(titleRect, title, _titleStyle);

            if (GUI.Button(closeRect, _closeContent, _closeStyle)) onClose?.Invoke();
        }
    }

    public abstract class EndsManagedListPopupED : PopupWindowContent
    {
        protected Vector2 scrollPos;
        protected int editingIndex = -1;
        protected string draftValue = "";
        protected string draftOriginalValue = "";
        protected string newItemDraft = "";
        protected readonly Action<string> persistChanges;
        protected readonly DialogDatabase database;
        protected readonly List<string> items;
        private int _draftRevision;

        protected EndsManagedListPopupED(DialogDatabase database, List<string> items, Action<string> persistChanges)
        {
            this.database = database;
            this.items = items ?? new List<string>();
            this.persistChanges = persistChanges;
        }

        protected abstract string PopupTitle { get; }
        protected abstract string NewItemLabel { get; }
        protected abstract string ExistingItemsLabel { get; }
        protected abstract string UndoAddLabel { get; }
        protected abstract string UndoRenameLabel { get; }
        protected abstract string UndoDeleteLabel { get; }
        protected abstract string RenameDialogTitle { get; }
        protected abstract string DeleteDialogTitle { get; }
        protected abstract string PersistLogItemKind { get; }

        protected abstract string OperationPrefix { get; }
        protected abstract string BuildRenameConfirmMessage(string oldName, string newName);
        protected abstract string BuildDeleteConfirmMessage(string name);

        protected virtual bool CanMutateItem(string item) => true;

        public override void OnOpen()
        {
            editorWindow.titleContent = new GUIContent(PopupTitle);
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(EndsPopupLayoutED.CATEGORY_WIDTH, EndsPopupLayoutED.CATEGORY_HEIGHT);
        }

        public override void OnGUI(Rect rect)
        {
            if (editingIndex < 0) RefreshItems();

            EndsPopupLayoutED.DrawTitleHeader(PopupTitle, () => TryClosePopup());
            EditorGUILayout.Space(EndsPopupLayoutED.CONTENT_TOP_PADDING);
            EndsPopupLayoutED.BeginContentPadding();

            DrawAddSection();
            
            EditorGUILayout.Space(EndsPopupLayoutED.EDITOR_SPACE_MEDIUM);
            GUILayout.Label(ExistingItemsLabel, EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            for (int i = 0; i < items.Count; i++)
            {
                DrawManagedRow(i);
            }

            EditorGUILayout.EndScrollView();
            DrawAdditionalSections();
            EndsPopupLayoutED.EndContentPadding();
        }

        protected virtual void DrawAdditionalSections()
        {
        }

        protected void RefreshItems()
        {
            List<string> source = database.Categories;

            if (OperationPrefix == "language") source = database.AvailableLanguages;

            items.Clear();

            if (source != null) items.AddRange(source);
        }

        protected bool ApplyManagedOperation(string operation, string name, string newName = null, int? expectedRevision = null)
        {
            EndsMcpJsonED command = EndsDialogGraphContractED.Command(OperationPrefix + "." + operation);
            command.Set("name", EndsMcpJsonED.Str(name));

            if (newName != null) command.Set("newName", EndsMcpJsonED.Str(newName));

            EndsSessionED.Result result = EndsDialogGraphContractED.Execute(database, command, expectedRevision);

            if (result.Success) return true;

            EditorUtility.DisplayDialog(PopupTitle, result.Error, "OK");

            return false;
        }

        private void DrawAddSection()
        {
            GUILayout.Label(NewItemLabel, EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            newItemDraft = EditorGUILayout.TextField(newItemDraft ?? "", GUILayout.ExpandWidth(true), GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT));
            GUI.enabled = !string.IsNullOrWhiteSpace(newItemDraft) && !items.Contains(newItemDraft.Trim());

            if (GUILayout.Button("Add", GUILayout.Width(EndsPopupLayoutED.BUTTON_ADD_WIDTH), GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT)))
            {
                if (!ApplyManagedOperation("add", newItemDraft.Trim())) return;

                RefreshItems();
                newItemDraft = "";
                persistChanges?.Invoke("Add " + PersistLogItemKind);
                GUI.FocusControl(null);
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        protected virtual string HighlightPrefix
        {
            get { return null; }
        }

        private void DrawManagedRow(int index)
        {
            string item = items[index];
            bool isEditing = editingIndex == index;
            bool canMutate = CanMutateItem(item);
            Color rowColor = GUI.backgroundColor;
            string highlightKey = null;

            if (HighlightPrefix != null) highlightKey = HighlightPrefix + item;
            Rect managedRow = EditorGUILayout.BeginHorizontal(GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT));
            DrawRowTextField(index, item, isEditing);

            if (EndsAiHighlightED.IsMarked(highlightKey)) GUI.backgroundColor = EndsAiHighlightED.Tint(highlightKey);

            if (isEditing)
            {
                DrawRowActionButtonsPair(
                    () => { if (TryApplyDraft()) CancelDraft(); },
                    "Save",
                    CancelDraft,
                    "Cancel");
            }
            else if (canMutate)
            {
                DrawRowActionButtonsPair(
                    () => BeginDraft(index, item),
                    "Rename",
                    () => TryRemoveAt(index),
                    "Delete",
                    secondIsDanger: true);
            }
            else DrawRowActionButtonsPlaceholder();

            EditorGUILayout.EndHorizontal();
            EndsAiHighlightED.ReleaseIfAltHovered(highlightKey, managedRow);
            GUI.backgroundColor = rowColor;
        }

        private void DrawRowTextField(int index, string displayValue, bool isEditing)
        {
            string controlName;

            if (isEditing) controlName = $"DialogEditorPopupEdit_{GetType().Name}_{index}";
            else controlName = $"DialogEditorPopupRead_{GetType().Name}_{index}";

            GUI.SetNextControlName(controlName);

            if (isEditing)
            {
                draftValue = EditorGUILayout.TextField(draftValue ?? "", GUILayout.ExpandWidth(true), GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT));
                return;
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(displayValue ?? "", GUILayout.ExpandWidth(true), GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT));
            EditorGUI.EndDisabledGroup();
        }

        private void DrawRowActionButtonsPair(Action firstAction, string firstLabel, Action secondAction, string secondLabel, bool secondIsDanger = false)
        {
            if (GUILayout.Button(firstLabel, GUILayout.Width(EndsPopupLayoutED.ROW_ACTION_BUTTON_WIDTH), GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT))) firstAction?.Invoke();
            if (secondIsDanger) GUI.backgroundColor = EndsPopupLayoutED.DELETE_BUTTON_COLOR;
            if (GUILayout.Button(secondLabel, GUILayout.Width(EndsPopupLayoutED.ROW_ACTION_BUTTON_WIDTH), GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT))) secondAction?.Invoke();
            if (secondIsDanger) GUI.backgroundColor = Color.white;
        }

        private void DrawRowActionButtonsPlaceholder()
        {
            GUILayout.Space(EndsPopupLayoutED.ROW_ACTION_BUTTON_WIDTH * 2);
        }

        private bool HasUnsavedDraft => editingIndex >= 0;

        private bool TryClosePopup()
        {
            if (!HasUnsavedDraft)
            {
                editorWindow.Close();

                return true;
            }

            int choice = EditorUtility.DisplayDialogComplex("Unsaved changes", "You have an unfinished edit. Save it before closing?", "Save", "Stay", "Discard");

            if (choice == 1) return false;
            if (choice == 0 && !TryApplyDraft()) return false;

            CancelDraft();
            editorWindow.Close();
            return true;
        }

        private void BeginDraft(int index, string value)
        {
            _draftRevision = EndsSessionED.ForDatabase(database).ObservedRevision;
            editingIndex = index;
            draftOriginalValue = value ?? "";
            draftValue = draftOriginalValue;
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);
        }

        private void CancelDraft()
        {
            draftValue = draftOriginalValue;
            draftOriginalValue = "";
            editingIndex = -1;
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);

            if (editorWindow != null) editorWindow.Repaint();
        }

        private bool TryApplyDraft()
        {
            if (editingIndex < 0 || editingIndex >= items.Count) return false;

            string oldName = items[editingIndex];
            string newName = (draftValue ?? "").Trim();

            if (string.IsNullOrWhiteSpace(newName)) return false;
            if (newName == oldName) return true;

            if (items.Contains(newName))
            {
                EditorUtility.DisplayDialog(RenameDialogTitle, $"\"{newName}\" already exists.", "OK");
                return false;
            }

            if (!EditorUtility.DisplayDialog(RenameDialogTitle, BuildRenameConfirmMessage(oldName, newName), "Rename", "Cancel")) return false;

            if (!ApplyManagedOperation("rename", oldName, newName, _draftRevision)) return false;

            RefreshItems();
            persistChanges?.Invoke("Rename " + PersistLogItemKind);

            return true;
        }

        private void TryRemoveAt(int index)
        {
            if (index < 0 || index >= items.Count) return;

            string name = items[index];

            if (!CanMutateItem(name)) return;
            if (!EditorUtility.DisplayDialog(DeleteDialogTitle, BuildDeleteConfirmMessage(name), "Delete", "Cancel")) return;

            if (!ApplyManagedOperation("remove", name)) return;

            RefreshItems();

            if (editingIndex == index) CancelDraft();
            else if (editingIndex > index) editingIndex--;

            persistChanges?.Invoke("Delete " + PersistLogItemKind);
        }
    }

    public class DialogCategoryPopup : EndsManagedListPopupED
    {
        public DialogCategoryPopup(DialogDatabase database, Action<string> persistChanges): base(database, database?.Categories != null ? new List<string>(database.Categories) : new List<string>(), persistChanges){}

        protected override string PopupTitle => "Category management";
        protected override string NewItemLabel => "New category";
        protected override string ExistingItemsLabel => "Existing categories";
        protected override string UndoAddLabel => "Add Category";
        protected override string UndoRenameLabel => "Rename Category";
        protected override string UndoDeleteLabel => "Delete Category";
        protected override string RenameDialogTitle => "Rename category";
        protected override string HighlightPrefix
        {
            get { return "category/"; }
        }

        protected override string DeleteDialogTitle => "Delete category";
        protected override string PersistLogItemKind => "Category";

        protected override bool CanMutateItem(string item)
        {
            return item != EndsDatabaseMaintenanceED.DefaultCategoryName;
        }

        protected override string OperationPrefix => "category";

        protected override string BuildRenameConfirmMessage(string oldName, string newName)
        {
            int dialogCount = EndsDatabaseMaintenanceED.CountDialogsInCategory(database, oldName);

            if (dialogCount > 0) return $"Category \"{oldName}\" has {dialogCount} dialog(s). They will be moved to \"{newName}\".";

            return $"Rename category \"{oldName}\" to \"{newName}\"?";
        }

        protected override string BuildDeleteConfirmMessage(string name)
        {
            int dialogCount = EndsDatabaseMaintenanceED.CountDialogsInCategory(database, name);

            if (dialogCount > 0) return $"Category \"{name}\" has {dialogCount} dialog(s). They will be moved to \"{EndsDatabaseMaintenanceED.DefaultCategoryName}\".";

            return $"Delete category \"{name}\"?";
        }




    }

    public class LanguageManagementPopup : EndsManagedListPopupED
    {
        public LanguageManagementPopup(DialogDatabase database, Action<string> persistChanges) : base(database, database?.AvailableLanguages != null ? new List<string>(database.AvailableLanguages) : new List<string>(), persistChanges){}

        protected override string PopupTitle => "Language management";
        protected override string NewItemLabel => "New language";
        protected override string ExistingItemsLabel => "Existing languages";
        protected override string UndoAddLabel => "Add Language";
        protected override string UndoRenameLabel => "Rename Language";
        protected override string UndoDeleteLabel => "Delete Language";
        protected override string RenameDialogTitle => "Rename language";
        protected override string HighlightPrefix
        {
            get { return "language/"; }
        }

        protected override string DeleteDialogTitle => "Delete language";
        protected override string PersistLogItemKind => "Language";

        protected override string OperationPrefix => "language";

        protected override string BuildRenameConfirmMessage(string oldName, string newName)
        {
            int usageCount = EndsDatabaseMaintenanceED.CountLanguageUsages(database, oldName);

            if (usageCount > 0) return $"Language \"{oldName}\" is used in {usageCount} text entry(ies). They will be moved to \"{newName}\".";
            return $"Rename language \"{oldName}\" to \"{newName}\"?";
        }

        protected override string BuildDeleteConfirmMessage(string name)
        {
            int usageCount = EndsDatabaseMaintenanceED.CountLanguageUsages(database, name);

            if (usageCount > 0) return $"Language \"{name}\" is used in {usageCount} text entry(ies). They will be removed.";
            return $"Delete language \"{name}\"?";
        }





        public override Vector2 GetWindowSize()
        {
            int orphanCount = EndsDatabaseMaintenanceED.GetOrphanLanguageUsages(database).Count;
            float height = EndsPopupLayoutED.CATEGORY_HEIGHT;

            if (orphanCount > 0) height += ORPHAN_SECTION_BASE_HEIGHT + orphanCount * ORPHAN_ROW_HEIGHT;

            return new Vector2(EndsPopupLayoutED.CATEGORY_WIDTH, height);
        }

        protected override void DrawAdditionalSections()
        {
            List<EndsDatabaseMaintenanceED.OrphanLanguageUsage> orphans = EndsDatabaseMaintenanceED.GetOrphanLanguageUsages(database);

            if (orphans.Count == 0) return;

            EditorGUILayout.Space(EndsPopupLayoutED.EDITOR_SPACE_MEDIUM);
            GUILayout.Label("Texts without language slot", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("These language codes exist in dialog or name-color texts but not in Available languages.", MessageType.Info);

            for (int i = 0; i < orphans.Count; i++)
            {
                EndsDatabaseMaintenanceED.OrphanLanguageUsage orphan = orphans[i];
                EditorGUILayout.BeginHorizontal(GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT));
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField( $"{orphan.LanguageCode} ({orphan.UsageCount} entries)", GUILayout.ExpandWidth(true), GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT));
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Add", GUILayout.Width(EndsPopupLayoutED.ROW_ACTION_BUTTON_WIDTH), GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT))) TryAddOrphanLanguageToList(orphan.LanguageCode);

                GUI.backgroundColor = EndsPopupLayoutED.DELETE_BUTTON_COLOR;

                if (GUILayout.Button("Delete texts", GUILayout.Width(ORPHAN_DELETE_BUTTON_WIDTH), GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT))) TryRemoveOrphanLanguageTexts(orphan.LanguageCode);

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        private const int ORPHAN_SECTION_BASE_HEIGHT = 56;
        private const int ORPHAN_ROW_HEIGHT = 24;
        private const int ORPHAN_DELETE_BUTTON_WIDTH = 88;

        private void TryAddOrphanLanguageToList(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode) || items.Contains(languageCode)) return;

            if (!ApplyManagedOperation("add", languageCode)) return;

            RefreshItems();
            persistChanges?.Invoke("Add orphan language to Available languages");
        }

        private void TryRemoveOrphanLanguageTexts(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode)) return;

            int usageCount = EndsDatabaseMaintenanceED.CountLanguageUsages(database, languageCode);
            string message = $"Delete {usageCount} text entry(ies) for language \"{languageCode}\"? Available languages list will not change.";

            if (!EditorUtility.DisplayDialog("Delete orphan texts", message, "Delete", "Cancel")) return;

            if (!ApplyManagedOperation("deleteTexts", languageCode)) return;
            persistChanges?.Invoke("Delete orphan language texts");
        }
    }

    internal enum DialogLocalizationFileFormat
    {
        Csv = 0
    }

    public class DialogLocalizationImportExportPopup : PopupWindowContent
    {
        private const int POPUP_WIDTH = 340;
        private const int POPUP_HEIGHT = 145;
        private const int FORMAT_LABEL_WIDTH = 50;
        private const int ACTION_BUTTON_HEIGHT = 24;

        private static readonly string[] FormatLabels = { "CSV" };

        private readonly DialogDatabase database;
        private readonly Action onImportApplied;
        private DialogLocalizationFileFormat selectedFormat = DialogLocalizationFileFormat.Csv;

        public DialogLocalizationImportExportPopup(DialogDatabase database, Action onImportApplied)
        {
            this.database = database;

            if (database != null) EndsSessionED.ForDatabase(database);

            this.onImportApplied = onImportApplied;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(POPUP_WIDTH, POPUP_HEIGHT);
        }

        public override void OnOpen()
        {
            editorWindow.titleContent = new GUIContent("Import / Export");
        }

        public override void OnGUI(Rect rect)
        {
            EndsPopupLayoutED.DrawTitleHeader("Import / Export", () => editorWindow.Close());
            EditorGUILayout.Space(EndsPopupLayoutED.CONTENT_TOP_PADDING);
            EndsPopupLayoutED.BeginContentPadding();

            if (database == null)
            {
                EditorGUILayout.HelpBox("No Dialog Database assigned.", MessageType.Warning);
                EndsPopupLayoutED.EndContentPadding();

                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Format:", GUILayout.Width(FORMAT_LABEL_WIDTH), GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT));

            selectedFormat = (DialogLocalizationFileFormat)EditorGUILayout.Popup((int)selectedFormat, FormatLabels, GUILayout.Height(EndsPopupLayoutED.ROW_BUTTON_HEIGHT));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(EndsPopupLayoutED.EDITOR_SPACE_MEDIUM);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Export", GUILayout.ExpandWidth(true), GUILayout.Height(ACTION_BUTTON_HEIGHT))) ExportSelectedFormat();
            if (GUILayout.Button("Import", GUILayout.ExpandWidth(true), GUILayout.Height(ACTION_BUTTON_HEIGHT))) ImportSelectedFormat();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(EndsPopupLayoutED.CONTENT_TOP_PADDING);
            EditorGUILayout.HelpBox("Exports Line and Choice texts. Keep the Key column unchanged. Keys use stable node and option IDs, options can be reordered safely.", MessageType.Info);
            EndsPopupLayoutED.EndContentPadding();
        }

        private void ExportSelectedFormat()
        {
            if (selectedFormat != DialogLocalizationFileFormat.Csv)
            {
                EditorUtility.DisplayDialog("Export", "Selected format is not implemented yet.", "OK");

                return;
            }

            string defaultName = database.name + "_localization.csv";
            string path = EditorUtility.SaveFilePanel("Export localization CSV", Application.dataPath, defaultName, "csv");

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                EndsDialogLocalizationCsvED.WriteCsvFile(database, path);
                EditorUtility.DisplayDialog("Export", "Exported:\n" + path, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Export", "Export failed:\n" + ex.Message, "OK");
            }
        }

        private void ImportSelectedFormat()
        {
            if (selectedFormat != DialogLocalizationFileFormat.Csv)
            {
                EditorUtility.DisplayDialog("Import", "Selected format is not implemented yet.", "OK");

                return;
            }

            string path = EditorUtility.OpenFilePanel("Import localization CSV", Application.dataPath, "csv");
            
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string csv = File.ReadAllText(path, Encoding.UTF8);
                int revision = EndsSessionED.ForDatabase(database).ObservedRevision;
                bool addLanguages = false;
                List<EndsDialogLocalizationCsvED.CsvMissingLanguageColumn> missingLanguages = EndsDialogLocalizationCsvED.GetMissingLanguageColumns(csv, database);

                if (missingLanguages.Count > 0)
                {
                    string prompt = EndsDialogLocalizationCsvED.BuildMissingLanguagesImportPromptMessage(missingLanguages);
                    int choice = EditorUtility.DisplayDialogComplex("Import localization", prompt, "Add", "Import only", "Cancel");

                    if (choice == 2 || choice == -1) return;
                    addLanguages = choice == 0;
                }

                EndsMcpJsonED commands = EndsMcpJsonED.Arr();
                EndsMcpJsonED command = EndsDialogGraphContractED.Command("localization.csv");
                command.Set("csv", EndsMcpJsonED.Str(csv));
                command.Set("addLanguages", EndsMcpJsonED.Boolean(addLanguages));
                commands.Add(command);
                EndsSessionED.Result result = EndsDialogGraphContractED.ExecuteBatch(database, commands, revision, false, out EndsMcpJsonED response);

                if (!result.Success)
                {
                    EditorUtility.DisplayDialog("Import", result.Error, "OK");

                    return;
                }

                onImportApplied?.Invoke();
                EditorUtility.DisplayDialog("Import", response.Get("results").Items[0].Get("message").Text, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Import", "Import failed:\n" + ex.Message, "OK");
            }
        }
    }

    public class DialogEditorSettingsPopup : PopupWindowContent
    {
        private const int POPUP_WIDTH = 320;
        private const int LABEL_WIDTH = 140;
        private const int ROW_HEIGHT = 20;

        private static readonly GUIContent _highlightAiContent = new GUIContent("Highlight AI edits:", "Yellow marks are unsaved AI edits. Green marks are saved AI edits. Hold Alt over green marks to clear them. Yellow marks remain until saving or discarding their changes.");

        private readonly DialogDatabase database;
        private readonly Action<string> onDatabaseChanged;
        private readonly Action onEditorPreferencesChanged;
        private float _popupHeight = EndsPopupLayoutED.HEADER_HEIGHT + EndsPopupLayoutED.CONTENT_TOP_PADDING + ROW_HEIGHT * 4 + EndsPopupLayoutED.CONTENT_SIDE_PADDING;

        public DialogEditorSettingsPopup(DialogDatabase database, Action<string> onDatabaseChanged, Action onEditorPreferencesChanged)
        {
            this.database = database;
            this.onDatabaseChanged = onDatabaseChanged;
            this.onEditorPreferencesChanged = onEditorPreferencesChanged;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(POPUP_WIDTH, _popupHeight);
        }

        public override void OnOpen()
        {
            editorWindow.titleContent = new GUIContent("Settings");
            EndsMcpHostED.SettingsChanged += OnMcpSettingsChanged;
        }

        public override void OnClose()
        {
            EndsMcpHostED.SettingsChanged -= OnMcpSettingsChanged;
        }

        private void OnMcpSettingsChanged()
        {
            if (editorWindow != null) editorWindow.Repaint();
        }

        public override void OnGUI(Rect rect)
        {
            EndsPopupLayoutED.DrawTitleHeader("Settings", () => editorWindow.Close());
            EditorGUILayout.Space(EndsPopupLayoutED.CONTENT_TOP_PADDING);
            EndsPopupLayoutED.BeginContentPadding();

            if (database == null) EditorGUILayout.HelpBox("No Dialog Database assigned.", MessageType.Warning);
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Include voice clips:", GUILayout.Width(LABEL_WIDTH), GUILayout.Height(ROW_HEIGHT));
                bool voiceEnabled = database.VoiceClipsEnabled;
                bool newVoiceEnabled = EditorGUILayout.Toggle(voiceEnabled, GUILayout.Height(ROW_HEIGHT));
                EditorGUILayout.EndHorizontal();

                if (newVoiceEnabled != voiceEnabled)
                {
                    if (newVoiceEnabled)
                    {
                        if (!SetVoiceAvailability(true)) return;
                        onDatabaseChanged?.Invoke("Enable voice clips");
                    }
                    else
                    {
                        int clipCount = CountAssignedVoiceClips();
                        string message = "Voice clip slots will be hidden.\n\n" + clipCount + " assigned audio clip(s) in dialogs will be cleared.\n\nContinue?";

                        if (EditorUtility.DisplayDialog("Disable Voice Clips", message, "Clear and disable", "Cancel"))
                        {
                            if (!SetVoiceAvailability(false)) return;
                            onDatabaseChanged?.Invoke("Disable voice clips");
                        }
                    }
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Save logs in Console:", GUILayout.Width(LABEL_WIDTH), GUILayout.Height(ROW_HEIGHT));
                bool saveLogsEnabled = EditorPrefs.GetBool(DialogEditorWindowED.PREFS_DIALOG_EDITOR_SAVE_LOGS_KEY, true);
                bool newSaveLogsEnabled = EditorGUILayout.Toggle(saveLogsEnabled, GUILayout.Height(ROW_HEIGHT));
                EditorGUILayout.EndHorizontal();

                if (newSaveLogsEnabled != saveLogsEnabled)
                {
                    EditorPrefs.SetBool(DialogEditorWindowED.PREFS_DIALOG_EDITOR_SAVE_LOGS_KEY, newSaveLogsEnabled);
                    onEditorPreferencesChanged?.Invoke();
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(_highlightAiContent, GUILayout.Width(LABEL_WIDTH), GUILayout.Height(ROW_HEIGHT));
                bool highlightAiEdits = EndsAiHighlightED.Enabled;
                bool newHighlightAiEdits = EditorGUILayout.Toggle(highlightAiEdits, GUILayout.Height(ROW_HEIGHT));
                EditorGUILayout.EndHorizontal();

                if (newHighlightAiEdits != highlightAiEdits) EndsAiHighlightED.SetEnabled(newHighlightAiEdits);

            }

            EndsMcpHostED.DrawInSettings();
            EndsPopupLayoutED.EndContentPadding();

            if (Event.current.type == EventType.Repaint) _popupHeight = Mathf.Ceil(GUILayoutUtility.GetLastRect().yMax + EndsPopupLayoutED.CONTENT_SIDE_PADDING);
            if (editorWindow == null) return;

            Vector2 size = GetWindowSize();
            Rect position = editorWindow.position;

            if (position.width == size.x && position.height == size.y) return;

            position.width = size.x;
            position.height = size.y;
            editorWindow.position = position;
        }

        private int CountAssignedVoiceClips()
        {
            int count = 0;

            if (database?.Dialogs == null) return count;

            for (int dialogIndex = 0; dialogIndex < database.Dialogs.Count; dialogIndex++)
            {
                DialogEntry dialog = database.Dialogs[dialogIndex];

                if (dialog?.nodes == null) continue;

                for (int nodeIndex = 0; nodeIndex < dialog.nodes.Count; nodeIndex++)
                {
                    DialogNode node = dialog.nodes[nodeIndex];

                    if (node?.NodeType != NodeType.Line || node.LineData?.DialogsText == null) continue;

                    for (int textIndex = 0; textIndex < node.LineData.DialogsText.Count; textIndex++)
                    {
                        LineText text = node.LineData.DialogsText[textIndex];

                        if (text != null && text.VoiceClip != null) count++;
                    }
                }
            }

            return count;
        }

        private bool SetVoiceAvailability(bool enabled)
        {
            EndsMcpJsonED command = EndsDialogGraphContractED.Command("database.voice");
            command.Set("enabled", EndsMcpJsonED.Boolean(enabled));
            command.Set("clearClips", EndsMcpJsonED.Boolean(!enabled));
            EndsSessionED.Result result = EndsDialogGraphContractED.Execute(database, command);

            if (result.Success) return true;

            EditorUtility.DisplayDialog("Voice clips", result.Error, "OK");

            return false;
        }
    }
}
#endif