#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public partial class DialogEditorWindowED
    {
        #region FIELDS

        private const int GRAPH_CONTROL_PADDING = 2;
        private const int GRAPH_TEXT_PADDING = 5;
        private const int NODE_TEXT_MIN_LINES = 5;
        private const int REDIRECT_HINT_HORIZONTAL_PADDING = 4;
        private const int CONDITIONS_WINDOW_BORDER = 10;
        private const int CONDITIONS_BOX_BORDER = 5;
        private const string DATABASE_SLOT_TOOLTIP = "Select the database containing dialogs, character sprites, speaker names, and variable definitions.";
        private const string NEW_ASSET_TOOLTIP = "Create a new database asset and assign it to this editor.";

        private static readonly GUIContent[] _mainTabs = { new GUIContent("💬  Text", "Edit dialog graphs, translations, and speaker names."),
            new GUIContent("🎭  Character Sprites", "Manage characters and their emotion sprites for line animations."), new GUIContent("🗝️  Variables", "Manage variable definitions, defaults, and current runtime values.") };
        private static readonly GUIContent[] _dialogTabs = { new GUIContent("💬  Dialogs", "Browse dialogs by category and open their graphs."), new GUIContent("🎨  Names", "Manage reusable speaker names and their display colors.") };
        private static readonly GUIContent _settingsContent = new GUIContent("⚙", "Configure voice clips, save logs, AI edit highlighting, and legacy tools.");
        private static readonly GUIContent _addDialogContent = new GUIContent("+ Add new dialog", "Choose a category and create a dialog with a Start node.");
        private static readonly GUIContent _manageCategoriesContent = new GUIContent("Manage categories", "Add, rename, or remove categories used to group and select dialogs.");
        private static readonly GUIContent _manageLanguagesContent = new GUIContent("Manage languages", "Manage language codes used by dialog translations.");
        private static readonly GUIContent _importExportContent = new GUIContent("Import / Export", "Import or export dialog translations as CSV.");
        private static readonly GUIContent _checkAllConditionsContent = new GUIContent("Check conditions", "Check every category for overlapping playback conditions and dialog selection conflicts.");
        private static readonly GUIContent _unconditionalDialogContent = new GUIContent("📋", "No playback conditions. This dialog can be selected as a fallback in its category.");
        private static readonly GUIContent _conditionalDialogContent = new GUIContent("🗝️", "Playback conditions are set. The dialog is eligible for category selection when its conditions match.");
        private static readonly GUIContent _lockedDialogContent = new GUIContent("🔒", "Redirect-only dialog. Category selection skips it, a Redirect node can open it directly.");
        private static readonly GUIContent _dialogMenuContent = new GUIContent("⋮", "Duplicate this dialog, move it to another category, or delete it.");
        private static readonly GUIContent _searchContent = new GUIContent("Search:", "Filter the list by name. Search ignores letter case.");
        private static readonly GUIContent _clearSearchContent = new GUIContent("×", "Clear the search filter and show the full list.");
        private static readonly GUIContent _backContent = new GUIContent("← Back to list", "Return to the dialog list");
        private static readonly GUIContent _showConditionsContent = new GUIContent("Show conditions", "Open or close the playback conditions window");
        private static readonly GUIContent _checkIntegrityContent = new GUIContent("Check integrity", "Find and fix broken links to non-existent nodes");
        private static readonly GUIContent _checkConditionsContent = new GUIContent("Check conditions", "Find overlaps of start conditions in the current category");
        private static readonly GUIContent _goToStartContent = new GUIContent("Go to start", "Center view on the start node");
        private static readonly GUIContent _saveContent = new GUIContent("Save", "Save all changes to the dialog and databases");
        private static readonly GUIContent _addConditionContent = new GUIContent("+ Add condition🗝️", "Add a new condition for the dialog");
        private static readonly GUIContent _instantLineContent = new GUIContent("⚡", "Instant line: text and sprites are displayed without waiting for a click");
        private static readonly GUIContent _deleteChoiceContent = new GUIContent("×", "Delete option");
        private static readonly GUIContent _playVoiceContent = new GUIContent("▶", "Play / Pause preview");
        private static readonly GUIContent _pauseVoiceContent = new GUIContent("||", "Play / Pause preview");
        private static readonly GUIContent _autoSaveContent = new GUIContent("Auto", "Automatically save the open dialog at the selected interval in minutes.");
        private static readonly GUIContent _addLineContent = new GUIContent("+ Add line", "Add a spoken line with translations, optional voice audio, and character actions.");
        private static readonly GUIContent _addChoiceNodeContent = new GUIContent("+ Add choice", "Add a menu of player choices. Each option can have conditions, consequences, and its own outgoing link.");
        private static readonly GUIContent _addConditionNodeContent = new GUIContent("+ Add condition", "Add a branch that checks variable conditions and follows its True or False output.");
        private static readonly GUIContent _addRedirectContent = new GUIContent("+ Add Redirect", "Continue in another dialog. The target must have no playback conditions or be redirect-only.");
        private static readonly GUIContent _deleteNodeContent = new GUIContent("×", "Delete this node from the dialog graph.");
        private static readonly GUIContent _duplicateNodeContent = new GUIContent("✂", "Duplicate this node and its content.");
        private static readonly GUIContent _addActionContent = new GUIContent("+ Sprite", "Add a character action to this line, such as showing, hiding, or changing a sprite.");
        private static readonly GUIContent _addChoiceOptionContent = new GUIContent("+ Option", "Add another option with its own text and outgoing link.");
        private static readonly GUIContent _addNodeConditionContent = new GUIContent("+ Condition", "Add a variable condition to this branching node.");
        private static readonly GUIContent _addChoiceConditionContent = new GUIContent("+ Condition", "Add a variable condition controlling whether this option is available.");
        private static readonly GUIContent _addConsequenceContent = new GUIContent("+ Consequence", "Add a variable change applied when the player selects this option.");
        private static readonly GUIContent _andLogicContent = new GUIContent("&&", "AND mode: all conditions must match. Click to switch to OR.");
        private static readonly GUIContent _orLogicContent = new GUIContent("||", "OR mode: at least one condition must match. Click to switch to AND.");
        private static readonly GUIContent _closeConditionsContent = new GUIContent("→", "Close the playback conditions panel.");
        private static readonly GUIContent _addCharacterContent = new GUIContent("+ Add character", "Create a character group with an initial emotion sprite slot.");
        private static readonly GUIContent _addSpriteContent = new GUIContent("+ Add sprite", "Add an emotion sprite slot to this character.");
        private static readonly GUIContent _deleteCharacterContent = new GUIContent("×", "Remove this character group. Dialog actions that use it become missing references.");
        private static readonly GUIContent _deleteSpriteContent = new GUIContent("Delete", "Remove this emotion sprite. Dialog actions that use it become missing references.");
        private static readonly GUIContent _addNameColorContent = new GUIContent("+ Add name color", "Create a reusable speaker name and display color for lines.");
        private static readonly GUIContent _deleteNameColorContent = new GUIContent("×", "Remove this speaker name and color. Lines that use it become missing references.");
        private static readonly GUIContent _loadCurrentContent = new GUIContent("Load Current from save.json", "Load saved variable values into the current runtime state.");
        private static readonly GUIContent _saveCurrentContent = new GUIContent("Save Current to save.json", "Write current runtime variable values to save.json.");
        private static readonly GUIContent _openSaveContent = new GUIContent("Open save.json", "Open the variable save file in its default application.");
        private static readonly GUIContent _createVariableContent = new GUIContent("+ Create variable", "Add a variable definition with the selected name, type, and default value.");
        private static readonly GUIContent _deleteVariableContent = new GUIContent("×", "Remove this variable definition. Conditions and consequences that use it become missing references.");
        private static readonly GUIContent _defaultValueContent = new GUIContent("Default value", "Initial value stored in the database, used when a variable has no current or saved value.");
        private static readonly GUIContent _currentValueContent = new GUIContent("Current value in G", "Value currently held in runtime memory. Use Save Current to save.json to persist it.");
        private static readonly GUIContent _editTemplateContent = new GUIContent("Template", "Edit the variable name, type, and default value in the database.");
        private static readonly GUIContent _saveTemplateContent = new GUIContent("Save", "Apply the edited variable definition to the database.");
        private static readonly GUIContent _editCurrentContent = new GUIContent("Current", "Edit the current runtime value. Use Save Current to save.json to persist it.");
        private static readonly GUIContent _finishCurrentContent = new GUIContent("Save", "Finish editing the value in runtime memory. Use Save Current to save.json to persist it.");

        [NonSerialized] private GraphStyles _graphStyles;
        [NonSerialized] private GraphStyles _unitGraphStyles;
        [NonSerialized] private WindowStyles _windowStyles;
        [NonSerialized] private UiCatalog _uiCatalog;
        private readonly Dictionary<string, CategoryIssues> _categoryIssues = new Dictionary<string, CategoryIssues>(StringComparer.Ordinal);

        private sealed class CategoryIssues
        {
            internal DialogDatabase Database;
            internal int Revision;
            internal List<EndsDialogSelectionDiagnosticsED.ValidationIssue> Issues;
        }
        [NonSerialized] private GUISkin _toolbarSkin;
        [NonSerialized] private GUISkin _listStyleSkin;
        [NonSerialized] private bool _listStyleProSkin;

        private sealed class GraphStyles
        {
            internal readonly GUISkin Skin;
            internal readonly bool ProSkin;
            internal readonly float Zoom;
            internal readonly GUIStyle Label;
            internal readonly GUIStyle CenterLabel;
            internal readonly GUIStyle StartLabel;
            internal readonly GUIStyle TextField;
            internal readonly GUIStyle Popup;
            internal readonly GUIStyle TextArea;
            internal readonly float TextAreaWidth;
            internal readonly NodeTextMetrics TextMetrics;
            internal readonly GUIStyle Button;
            internal readonly GUIStyle SpeakerLabel;
            internal readonly GUIStyle LogicLabel;
            internal readonly GUIStyle TimeLabel;
            internal readonly GUIStyle RedirectHint;
            internal readonly GUIStyle MissingWarning;
            internal readonly GUIStyle MissingDefaultWarning;
            internal readonly GUIStyle BoolToggle;

            internal GraphStyles(float zoom)
            {
                Skin = GUI.skin;
                ProSkin = EditorGUIUtility.isProSkin;
                Zoom = zoom;
                int fontSize = (int)(BASE_FONT_SIZE * zoom);
                int controlPadding = (int)(GRAPH_CONTROL_PADDING * zoom);
                int textPadding = (int)(GRAPH_TEXT_PADDING * zoom);
                int hintPadding = (int)(REDIRECT_HINT_HORIZONTAL_PADDING * zoom);
                Label = new GUIStyle(Skin.label) { fontSize = fontSize, padding = new RectOffset(0, 0, 0, 0), alignment = TextAnchor.MiddleLeft };
                CenterLabel = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
                StartLabel = new GUIStyle(Skin.label) { fontSize = fontSize, normal = { textColor = START_LABEL_COLOR }, padding = new RectOffset(0, 0, 0, 0) };
                TextField = new GUIStyle(Skin.textField) { fontSize = fontSize, padding = new RectOffset(controlPadding, controlPadding, controlPadding, controlPadding) };
                Popup = new GUIStyle(EditorStyles.popup) { fontSize = fontSize, fixedHeight = FIELD_ROW_HEIGHT * zoom, padding = new RectOffset(textPadding, textPadding, controlPadding, controlPadding) };
                TextArea = new GUIStyle(Skin.textArea) { fontSize = fontSize, padding = new RectOffset(textPadding, textPadding, controlPadding, controlPadding) };
                TextAreaWidth = BASE_NODE_WIDTH * zoom - Mathf.Max(NODE_LEFT_RIGHT_PADDING * zoom, Skin.window.overflow.left) - Mathf.Max(NODE_LEFT_RIGHT_PADDING * zoom, Skin.window.overflow.right);
                TextMetrics = new NodeTextMetrics(TextArea, TextAreaWidth, zoom);
                Button = new GUIStyle(Skin.button) { fontSize = fontSize, padding = new RectOffset(controlPadding, controlPadding, controlPadding, controlPadding) };
                SpeakerLabel = new GUIStyle(Label);
                LogicLabel = new GUIStyle(Label) { alignment = TextAnchor.MiddleRight };
                TimeLabel = new GUIStyle(Label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
                RedirectHint = new GUIStyle(EditorStyles.helpBox) { fontSize = Mathf.Max(1, Mathf.RoundToInt(BASE_FONT_SIZE * zoom)), wordWrap = true,
                    alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip, padding = new RectOffset(hintPadding, hintPadding, controlPadding, controlPadding) };
                MissingWarning = new GUIStyle(Label) { wordWrap = true, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
                MissingDefaultWarning = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { wordWrap = true, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
                MissingDefaultWarning.fontSize = Mathf.Max(1, Mathf.RoundToInt(MissingDefaultWarning.fontSize * zoom));
                BoolToggle = new GUIStyle(Skin.button) { fontSize = Mathf.Max(1, (int)(BASE_FONT_SIZE * zoom)), alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0) };
            }
        }

        private sealed class WindowStyles
        {
            private const int HINT_FONT_SIZE = 13;
            private const int HINT_HORIZONTAL_PADDING = 8;
            private const int HINT_VERTICAL_PADDING = 4;

            internal readonly GUISkin Skin;
            internal readonly bool ProSkin;
            internal readonly GUIStyle ConditionsBackground;
            internal readonly GUIStyle ConditionBox;
            internal readonly GUIStyle ScaleLabel;
            internal readonly GUIStyle PortHint;
            internal readonly GUIStyle CopyHint;
            internal readonly GUIStyle NameLabel;
            internal readonly GUIStyle EmptySprite;
            internal readonly GUIStyle EmptyNode;

            internal WindowStyles()
            {
                Skin = GUI.skin;
                ProSkin = EditorGUIUtility.isProSkin;
                ConditionsBackground = new GUIStyle(EditorStyles.helpBox) { border = new RectOffset(CONDITIONS_WINDOW_BORDER, CONDITIONS_WINDOW_BORDER, CONDITIONS_WINDOW_BORDER, CONDITIONS_WINDOW_BORDER) };
                ConditionBox = new GUIStyle(Skin.box) { border = new RectOffset(CONDITIONS_BOX_BORDER, CONDITIONS_BOX_BORDER, CONDITIONS_BOX_BORDER, CONDITIONS_BOX_BORDER) };
                ScaleLabel = new GUIStyle(Skin.label) { fontSize = BASE_FONT_SIZE, normal = { textColor = Color.white } };
                PortHint = new GUIStyle(Skin.box) { fontSize = HINT_FONT_SIZE, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(HINT_HORIZONTAL_PADDING, HINT_HORIZONTAL_PADDING, HINT_VERTICAL_PADDING, HINT_VERTICAL_PADDING), normal = { textColor = HINT_TEXT_COLOR } };
                CopyHint = new GUIStyle(PortHint) { normal = { textColor = HINT_COPY_TEXT_COLOR } };
                NameLabel = new GUIStyle(EditorStyles.boldLabel);
                EmptySprite = new GUIStyle(Skin.box) { alignment = TextAnchor.MiddleCenter };
                EmptyNode = new GUIStyle();
            }
        }

        private sealed class UiCatalog : EndsAnimationDrawerED.ICatalog
        {
            internal readonly DialogDatabase Database;
            internal readonly int Revision;
            internal readonly string[] Categories;
            internal readonly string[] ActionCharacters;
            internal readonly string[] SpeakerNames;
            private readonly Dictionary<string, CharacterGroup> _characters = new Dictionary<string, CharacterGroup>();
            private readonly Dictionary<string, CharacterNameColor> _speakerColors = new Dictionary<string, CharacterNameColor>();
            private readonly Dictionary<CharacterGroup, string[]> _emotions = new Dictionary<CharacterGroup, string[]>();
            private readonly Dictionary<ConditionVariableType, string[]> _variableNames = new Dictionary<ConditionVariableType, string[]>();
            private readonly ConditionalWeakTable<IReadOnlyList<CharacterActionData>, ActionNames> _actionNames = new ConditionalWeakTable<IReadOnlyList<CharacterActionData>, ActionNames>();
            private readonly Dictionary<string, RedirectChoices> _redirectChoices = new Dictionary<string, RedirectChoices>();
            private readonly RedirectChoices _emptyRedirectChoices = new RedirectChoices(new List<DialogEntry>());
            private readonly Dictionary<string, List<DialogEntry>> _dialogGroups;
            private Dictionary<string, List<DialogEntry>> _filteredDialogs;
            private string _dialogFilter;
            private CultureInfo _filterCulture;
            private bool[] _visibleCharacters;
            private int _visibleCharacterCount;
            private string _characterFilter;
            private CultureInfo _characterFilterCulture;

            internal int VisibleCharacterCount => _visibleCharacterCount;

            /// <summary> ENDS. Reports whether the existing database offers character choices to animation drawers. </summary>
            public bool HasCharacters => Database.CharacterGroups != null && Database.CharacterGroups.Count > 0;

            internal UiCatalog(DialogDatabase database, int revision)
            {
                Database = database;
                Revision = revision;
                Categories = (database.Categories ?? new List<string>()).ToArray();
                List<string> actionCharacters = new List<string> { "None" };
                List<string> speakerNames = new List<string> { "None" };

                foreach (CharacterGroup group in database.CharacterGroups ?? new List<CharacterGroup>())
                {
                    if (group == null) continue;

                    actionCharacters.Add(group.characterName);

                    if (group.characterName != null && !_characters.ContainsKey(group.characterName)) _characters.Add(group.characterName, group);
                }

                foreach (CharacterNameColor entry in database.CharacterNameColors ?? new List<CharacterNameColor>())
                {
                    if (entry == null) continue;

                    speakerNames.Add(entry.Name);

                    if (entry.Name != null && !_speakerColors.ContainsKey(entry.Name)) _speakerColors.Add(entry.Name, entry);
                }

                ActionCharacters = actionCharacters.ToArray();
                SpeakerNames = speakerNames.ToArray();
                _dialogGroups = (database.Dialogs ?? new List<DialogEntry>())
                    .Where(dialog => dialog != null)
                    .GroupBy(dialog => dialog.initiator ?? DEFAULT_CATEGORY_NAME)
                    .OrderBy(group =>
                    {
                        int index = Array.IndexOf(Categories, group.Key);

                        if (index >= 0) return index;

                        return int.MaxValue;
                    })
                    .ToDictionary(group => group.Key, group => group.ToList());
            }

            internal string[] GetVariableNames(ConditionVariableType type)
            {
                if (_variableNames.TryGetValue(type, out string[] names)) return names;

                names = Database.GetVariableNamesByType(type);
                _variableNames.Add(type, names);

                return names;
            }

            /// <summary> ENDS. Resolves the cached character entry shared by graph controls and animation drawers. </summary>
            public CharacterGroup GetCharacter(string name)
            {
                if (name == null) return null;

                _characters.TryGetValue(name, out CharacterGroup group);

                return group;
            }

            internal CharacterNameColor GetSpeakerColor(string name)
            {
                if (name == null) return null;

                _speakerColors.TryGetValue(name, out CharacterNameColor entry);

                return entry;
            }

            /// <summary> ENDS. Reuses cached emotion names for the selected character instead of allocating choices during each repaint. </summary>
            public string[] GetEmotions(CharacterGroup group)
            {
                if (_emotions.TryGetValue(group, out string[] names)) return names;

                names = group.sprites
                    .Select(sprite => sprite.spriteName)
                    .ToArray();
                _emotions.Add(group, names);

                return names;
            }

            /// <summary> ENDS. Reuses row-specific character choices while excluding names assigned to other animations. </summary>
            public string[] GetActionCharacters(IReadOnlyList<CharacterActionData> actions, int index)
            {
                if (!_actionNames.TryGetValue(actions, out ActionNames names) || !names.Matches(actions))
                {
                    names = new ActionNames(actions, ActionCharacters);
                    _actionNames.Remove(actions);
                    _actionNames.Add(actions, names);
                }

                return names.Options[index];
            }

            internal RedirectChoices GetRedirectChoices(string category)
            {
                if (string.IsNullOrEmpty(category)) return _emptyRedirectChoices;
                if (_redirectChoices.TryGetValue(category, out RedirectChoices choices)) return choices;

                List<DialogEntry> targets = new List<DialogEntry>();

                foreach (DialogEntry entry in Database.Dialogs ?? new List<DialogEntry>())
                {
                    if (entry == null || string.IsNullOrEmpty(entry.dialogName) || entry.initiator != category || !DialogEntryUtility.IsRedirectTargetCandidate(entry)) continue;

                    targets.Add(entry);
                }

                targets.Sort((first, second) => string.Compare(first.dialogName, second.dialogName, StringComparison.Ordinal));
                choices = new RedirectChoices(targets);
                _redirectChoices.Add(category, choices);

                return choices;
            }

            internal bool[] GetVisibleCharacters(string filter)
            {
                if (_visibleCharacters != null && _characterFilter == filter && ReferenceEquals(_characterFilterCulture, CultureInfo.CurrentCulture)) return _visibleCharacters;

                _characterFilter = filter;
                _characterFilterCulture = CultureInfo.CurrentCulture;
                string loweredFilter = "";

                if (!string.IsNullOrEmpty(filter)) loweredFilter = filter.ToLower(_characterFilterCulture);

                _visibleCharacters = new bool[Database.CharacterGroups.Count];
                _visibleCharacterCount = 0;

                for (int i = 0; i < _visibleCharacters.Length; i++)
                {
                    CharacterGroup group = Database.CharacterGroups[i];

                    if (group == null) continue;
                    if (!string.IsNullOrEmpty(filter) && (group.characterName == null || !group.characterName.ToLower(_characterFilterCulture).Contains(loweredFilter))) continue;

                    _visibleCharacters[i] = true;
                    _visibleCharacterCount++;
                }

                return _visibleCharacters;
            }

            internal Dictionary<string, List<DialogEntry>> GetDialogGroups(string filter)
            {
                if (string.IsNullOrEmpty(filter)) return _dialogGroups;
                if (_filteredDialogs != null && _dialogFilter == filter && ReferenceEquals(_filterCulture, CultureInfo.CurrentCulture)) return _filteredDialogs;

                _dialogFilter = filter;
                _filterCulture = CultureInfo.CurrentCulture;
                string loweredFilter = filter.ToLower(_filterCulture);
                _filteredDialogs = new Dictionary<string, List<DialogEntry>>();

                foreach (KeyValuePair<string, List<DialogEntry>> group in _dialogGroups)
                {
                    List<DialogEntry> matches = group.Value
                        .Where(dialog => dialog.dialogName != null && dialog.dialogName.ToLower(_filterCulture).Contains(loweredFilter))
                        .ToList();

                    if (matches.Count > 0) _filteredDialogs.Add(group.Key, matches);
                }

                return _filteredDialogs;
            }
        }

        private sealed class RedirectChoices
        {
            internal readonly List<DialogEntry> Dialogs;
            internal readonly List<string> Names;
            internal readonly string[] Labels;

            internal RedirectChoices(List<DialogEntry> dialogs)
            {
                Dialogs = dialogs;
                Names = new List<string>(dialogs.Count + 1);

                if (dialogs.Count == 0)
                {
                    Labels = new[] { "(no redirect targets)" };

                    return;
                }

                Names.Add("");
                Labels = new string[dialogs.Count + 1];
                Labels[0] = "(not selected)";

                for (int i = 0; i < dialogs.Count; i++)
                {
                    Names.Add(dialogs[i].dialogName);
                    Labels[i + 1] = FormatRedirectTargetDialogLabel(dialogs[i]);
                }
            }
        }

        private sealed class ActionNames
        {
            internal readonly string[][] Options;
            private readonly string[] _names;
            private readonly DialogReferenceState[] _referenceStates;

            internal ActionNames(IReadOnlyList<CharacterActionData> actions, string[] characters)
            {
                _names = new string[actions.Count];
                _referenceStates = new DialogReferenceState[actions.Count];
                Options = new string[actions.Count][];

                for (int i = 0; i < actions.Count; i++)
                {
                    string selected = actions[i]?.CharacterName;
                    _names[i] = selected;
                    _referenceStates[i] = default;

                    if (actions[i] != null) _referenceStates[i] = actions[i].ReferenceState;

                    List<string> used = EndsDialogRulesED.CollectUsedActionCharacters(actions, i);
                    Options[i] = characters
                        .Where(name => name == "None" || name == selected || !used.Contains(name))
                        .ToArray();
                }
            }

            internal bool Matches(IReadOnlyList<CharacterActionData> actions)
            {
                if (_names.Length != actions.Count) return false;

                for (int i = 0; i < actions.Count; i++)
                {
                    if (_names[i] != actions[i]?.CharacterName) return false;
                    if (actions[i] != null && _referenceStates[i] != actions[i].ReferenceState) return false;
                }

                return true;
            }
        }

        #endregion FIELDS

        #region UI CACHES

        private GraphStyles GetGraphStyles(float zoom)
        {
            if (zoom == DEFAULT_ZOOM_SCALE)
            {
                if (_unitGraphStyles == null || _unitGraphStyles.Skin != GUI.skin || _unitGraphStyles.ProSkin != EditorGUIUtility.isProSkin) _unitGraphStyles = new GraphStyles(zoom);

                return _unitGraphStyles;
            }

            if (_graphStyles == null || _graphStyles.Zoom != zoom || _graphStyles.Skin != GUI.skin || _graphStyles.ProSkin != EditorGUIUtility.isProSkin) _graphStyles = new GraphStyles(zoom);

            return _graphStyles;
        }

        private WindowStyles GetWindowStyles()
        {
            if (_windowStyles == null || _windowStyles.Skin != GUI.skin || _windowStyles.ProSkin != EditorGUIUtility.isProSkin) _windowStyles = new WindowStyles();

            return _windowStyles;
        }

        private UiCatalog GetUiCatalog()
        {
            int revision = EndsSessionED.ForDatabase(database).CatalogRevision;

            if (_uiCatalog == null || _uiCatalog.Database != database || _uiCatalog.Revision != revision) _uiCatalog = new UiCatalog(database, revision);

            return _uiCatalog;
        }

        #endregion UI CACHES
    }
}
#endif