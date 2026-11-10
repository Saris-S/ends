#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Text.RegularExpressions;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{

    public partial class DialogEditorWindowED : EditorWindow
    {
        #region Constants

        // Colors
        private static readonly Color LINE_COLOR_1 = new Color(0.9f, 0.9f, 0.9f, 0.3f);
        private static readonly Color LINE_COLOR_2 = new Color(0.85f, 0.85f, 0.85f, 0.3f);
        private static readonly Color CATEGORY_BG = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        private static readonly Color ORANGE_TOOLBAR = new Color(0.96f, 0.6f, 0.3f);
        private static readonly Color TEXT_AREA_BG = new Color(1f, 1f, 0.95f);
        private static readonly Color GRID_BG = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        private static readonly Color GRID_LINE = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        private static readonly Color CONDITION_BOX_BG = new Color(0.9f, 0.9f, 0.9f);
        private static readonly Color DELETE_BUTTON_COLOR = new Color(1f, 0.7f, 0.7f);
        private static readonly Color INSTANT_LINE_COLOR = new Color(1f, 0.9f, 0.3f);
        private static readonly Color DEFAULT_LINE_COLOR = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color CONDITION_TRUE_COLOR = new Color(0.1f, 0.5f, 0.1f);
        private static readonly Color CONDITION_FALSE_COLOR = new Color(0.5f, 0.1f, 0.1f);
        private static readonly Color START_LABEL_COLOR = new Color(0.23f, 0.18f, 0.17f);
        private static readonly Color HINT_TEXT_COLOR = new Color(0.9f, 0.9f, 0.9f);
        private static readonly Color HINT_COPY_TEXT_COLOR = new Color(0.85f, 0.95f, 0.85f);

        // Graph canvas
        private const int GRID_TILE_SIZE = 20;
        private const float DEFAULT_ZOOM_SCALE = 1f;
        private const float MIN_NODE_HEIGHT = 40f;
        private const float ZOOM_SENSITIVITY = 0.05f;
        private const float ZOOM_MIN = 0.1f;
        private const float ZOOM_MAX = 2.95f;
        private const float ZOOM_EPSILON = 0.001f;
        private const int BEZIER_OFFSET = 50;
        private const float BEZIER_THICKNESS = 2f;
        private const int SELECTION_FRAME_PADDING = 10;
        private const float BOX_SELECT_MIN_DRAG = 4f;
        private const int DEFAULT_POSITION_XY = 100;
        private const int DUPLICATE_NODE_OFFSET = 50;

        // Shared node chrome
        private const int NODE_TOP_PADDING = 22;
        private const int NODE_LEFT_RIGHT_PADDING = 10;
        private const int NODE_BOTTOM_PADDING = 5;
        private const int NODE_PORT_SIZE = 40;
        private const int PORT_HALF_SIZE = NODE_PORT_SIZE / 2;
        private const int BASE_NODE_WIDTH = 450;
        private const int NODE_SPACE_OFFSET = 5;
        private const int NODE_TOOLBAR_HEIGHT = 22;
        private const int NODE_BUTTON_SIZE = 27;
        private const int HEADER_FONT_SIZE = 14;
        private const int BASE_FONT_SIZE = 12;
        private const int CATEGORY_FONT_SIZE = 12;
        private const int FIELD_ROW_HEIGHT = 20;
        private const int TITLE_LABEL_OFFSET = 25;
        private const int TITLE_LABEL_WIDTH_ZOOM = 200;
        private const int FOLDOUT_ARROW_WIDTH = 20;
        private const int ACTION_BUTTON_SIZE = 22;
        private const string MISSING_WARNING_EMOJI_PREFIX = "⚠ ";
        private const int HINT_EXTRA_WIDTH = 16;

        // Start node
        private const int START_NODE_WIDTH = 100;
        private const int START_NODE_HEIGHT = 45;
        private const int INPUT_OFFSET_START = 50;

        // Line node
        private const int INPUT_OFFSET_LINE = 200;
        private const int LINE_NODE_BASE_HEIGHT = 27;
        private const int LINE_NODE_HEADER_OFFSET = 40;
        private const int LINE_NODE_ACTIONS_FOLDOUT = 25;
        private const int LINE_NODE_ACTION_ITEM_HEIGHT = 60;
        private const int LINE_NODE_LANG_FOLDOUT = 25;
        private const int LINE_NODE_TEXT_AREA_HEIGHT = 110;
        private const int LINE_NODE_AUDIO_ROW_HEIGHT = 25;
        private const int LINE_NODE_TOOLBAR_RIGHT_BUTTONS_COUNT = 2;
        private const int LINE_NODE_INSTANT_BUTTON_SPACING_COUNT = 4;
        private const int SPRITE_BUTTON_WIDTH = 90;
        private const int LINE_TEXT_AREA_HEIGHT = 80;
        private const int LINE_CHARACTER_LABEL_WIDTH = 105;
        private const int LINE_CHARACTER_POPUP_WIDTH = 200;
        private const int LINE_CHARACTER_ROW_HEIGHT = 21;
        private const int LINE_SAYER_LABEL_WIDTH = 120;
        private const int LINE_SAYER_LABEL_SPACING = 8;
        private const int LINE_SAYER_FIELD_WIDTH = 150;
        private const int LINE_SAYER_FIELD_SPACING = 8;
        private const int LINE_VOICE_LABEL_WIDTH = 35;
        private const int LINE_VOICE_OBJECT_FIELD_WIDTH = 90;
        private const int LINE_VOICE_COMBINED_TIME_WIDTH = 94;
        private const int LINE_VOICE_PLAY_WIDTH = 22;
        private const int LINE_VOICE_FIELD_GAP = 4;
        private const int LINE_VOICE_OBJECT_TO_SCRUB_GAP = 8;
        private const int LINE_VOICE_SCRUBBER_MIN_WIDTH = 40;
        private const int LINE_VOICE_SCRUBBER_HEIGHT = 10;
        private const int SAYER_PREVIEW_X = LINE_SAYER_LABEL_WIDTH + LINE_SAYER_LABEL_SPACING + LINE_SAYER_FIELD_WIDTH + LINE_SAYER_FIELD_SPACING;
        private const int SAYER_PREVIEW_WIDTH = 100;
        private const int COLOR_PREVIEW_OFFSET = 315;
        private const int COLOR_PREVIEW_SIZE = 12;
        private const int ACTION_ROW_HEIGHT = 25;

        // Choice node
        private const int CHOICE_NODE_ENTER_SPACING = 22;
        private const int CHOICE_NODE_EXIT_SPACING = CHOICE_NODE_ENTER_SPACING + NODE_SPACE_OFFSET + PORT_HALF_SIZE;
        private const int CHOICE_HEADER_HEIGHT = 25;
        private const int CHOICE_LANG_FOLDOUT = 25;
        private const int CHOICE_TEXT_AREA_HEIGHT = 85;
        private const int CHOICE_EVENT_ITEM_HEIGHT = 27;
        private const int CHOICE_NODE_EMPTY_HEIGHT = 44;
        private const int CHOICE_ROW_LABEL_WIDTH = 110;
        private const int CHOICE_BUTTON_GAP = 5;
        private const int CHOICE_CLOSE_BUTTON_OFFSET = 35;
        private const int CHOICE_CONSEQUENCE_BUTTON_WIDTH = 95;
        private const int CHOICE_CONSEQUENCE_BUTTON_OFFSET = CHOICE_CLOSE_BUTTON_OFFSET + CHOICE_CONSEQUENCE_BUTTON_WIDTH + CHOICE_BUTTON_GAP;
        private const int CHOICE_CONDITION_BUTTON_WIDTH = 80;
        private const int CHOICE_CONDITION_BUTTON_OFFSET = CHOICE_CONSEQUENCE_BUTTON_OFFSET + CHOICE_CONDITION_BUTTON_WIDTH + CHOICE_BUTTON_GAP;
        private const int CHOICE_FOLDOUT_WIDTH = 180;
        private const int CHOICE_LOGIC_TOGGLE_SIZE = 20;
        private const int CHOICE_LOGIC_RIGHT_PADDING = 15;
        private const int CHOICE_LOGIC_LABEL_WIDTH = 140;
        private const int CHOICE_LOGIC_GAP = 5;
        private const int CHOICE_LOGIC_ROW_Y_OFFSET = 3;
        private const int CHOICE_ITEM_CLOSE_OFFSET = 25;
        private const float CHOICE_EVENT_FIELD_TOP_PADDING = 2.5f;
        private const int CHOICE_EVENT_TYPE_LABEL_WIDTH = 32;
        private const int CHOICE_EVENT_TYPE_POPUP_WIDTH = 45;
        private const int CHOICE_EVENT_NAME_LABEL_WIDTH = 38;
        private const int CHOICE_EVENT_VAR_COND_POPUP_WIDTH = 72;
        private const int CHOICE_EVENT_VAR_POPUP_WIDTH_CONS = 105;
        private const int CHOICE_EVENT_VAR_SPACING = 2;
        private const int CHOICE_EVENT_OP_POPUP_WIDTH = 135;
        private const int CHOICE_EVENT_OP_TO_VAL_GAP = 105;
        private const int CHOICE_EVENT_STANET_LABEL_WIDTH = 53;
        private const int CHOICE_EVENT_VALUE_FIELD_WIDTH = 70;
        private const int CHOICE_EVENT_ITEM_SPACING = 2;

        // Condition node + start-conditions panel
        private const int CONDITION_NODE_WIDTH = 380;
        private const int CONDITION_NODE_ENTER_SPACING = 22;
        private const int CONDITION_NODE_EXIT_SPACING = CONDITION_NODE_ENTER_SPACING + NODE_SPACE_OFFSET + PORT_HALF_SIZE;
        private const int CONDITION_NODE_CONDITION_HEIGHT = 130;
        private const int CONDITION_NODE_BASE_HEIGHT = 117;
        private const int CONDITION_NODE_FOLDOUT_OFFSET = 10;
        private const int CONDITION_ITEM_SPACING = 20;
        private const int CONDITIONS_BOX_PADDING = 2;
        private const int CONDITION_LABEL_X = 10;
        private const int CONDITION_TOP_PADDING = 5;
        private const int CONDITION_FIELD_Y_OFFSET = 30;
        private const int CONDITION_LABEL_WIDTH = 80;
        private const int CONDITION_FIELD_OFFSET = CONDITION_LABEL_X + CONDITION_LABEL_WIDTH;
        private const int CONDITION_FIELD_WIDTH_OFFSET = 100;
        private const int CONDITION_TITLE_LABEL_WIDTH = 100;
        private const int CONDITION_CLOSE_BUTTON_WIDTH = 25;
        private const int CONDITION_CLOSE_BUTTON_OFFSET = 35;
        private const int CONDITION_AND_SPACING = 20;
        private const int CONDITION_LOGIC_LABEL_X = 100;
        private const int CONDITION_LOGIC_LABEL_WIDTH = 140;
        private const int CONDITION_LOGIC_GAP = 5;
        private const int CONDITION_LOGIC_TOGGLE_X = CONDITION_LOGIC_LABEL_X + CONDITION_LOGIC_LABEL_WIDTH + CONDITION_LOGIC_GAP;
        private const int CONDITION_PORT_LABEL_OFFSET = 80;
        private const int CONDITION_PORT_LABEL_WIDTH = 40;
        private const int CONDITION_TRUE_PORT_Y = 15;
        private const int CONDITION_HEADER_OFFSET = 34;
        private const int CONDITION_TOGGLE_OFFSET = 35;

        // Redirect node
        private const int REDIRECT_NODE_ENTER_SPACING = 22;
        private const int REDIRECT_CONTENT_TOP_PADDING = 5;
        private const int REDIRECT_SECTION_GAP = 5;
        private const int REDIRECT_FIELD_GAP = 5;
        private const int REDIRECT_CONTENT_BOTTOM_PADDING = 3;
        private const int REDIRECT_HINT_LEFT_PADDING = 14;
        private const int REDIRECT_HINT_HEIGHT = 40;
        private const int REDIRECT_FIELD_ROW_HEIGHT = 22;
        private const int REDIRECT_CATEGORY_LABEL_WIDTH = 72;
        private const int REDIRECT_DIALOG_LABEL_WIDTH = 72;

        // Dialogs list tab
        private const int TOOLBAR_HEIGHT = 0;
        private const int ASSET_ACTION_ROW_HEIGHT = 30;
        private const int BUTTON_BACK_WIDTH = 130;
        private const int SEARCH_LABEL_WIDTH = 50;
        private const int CLEAR_BUTTON_WIDTH = 25;
        private const int DIALOG_ROW_HEIGHT = 26;
        private const int CATEGORY_HEADER_HEIGHT = 25;
        private const int DIALOG_ICON_SIZE = 20;
        private const int SPACE_AFTER_CATEGORY = 15;
        private const int INLINE_ASSET_LABEL_WIDTH = 100;
        private const int INLINE_ASSET_FIELD_WIDTH = 400;
        private const int INLINE_ASSET_SLOT_INDENT = 8;
        private const int INLINE_ASSET_NEW_BUTTON_WIDTH = 52;
        private const int INLINE_HEADER_SECTION_PADDING = 6;
        private const int INLINE_ASSET_CONTROL_HEIGHT = 18;
        private const int INLINE_ASSET_LOCKED_HINT_WIDTH = 14;
        private const string DIALOG_DATABASE_SWITCH_LOCKED_TOOLTIP = "Close the dialog graph with Back to list before switching Dialog Database.";

        // Graph editor header / toolbar
        private const int EDITOR_SPACE_SMALL = 5;
        private const int EDITOR_SPACE_MEDIUM = 10;
        private const int ADD_LINE_BUTTON_WIDTH = 130;
        private const int ADD_LINE_BUTTON_HEIGHT = 21;
        private const int LINES_HEADER_BUTTON_OFFSET = 2;

        // Validation
        private const int BROKEN_LINKS_MAX_SHOW = 15;
        private const int START_CONDITION_ISSUES_MAX_SHOW = 15;

        // Prefs / defaults / placeholders
        private const string PREFS_DIALOG_DATABASE_GUID_KEY = "DialogEditor.DialogDatabaseGuid";
        private const string PREFS_DIALOG_EDITOR_AUTO_SAVE_KEY = "DialogEditor.AutoSaveEnabled";
        private const string PREFS_DIALOG_EDITOR_AUTO_SAVE_MINUTES_KEY = "DialogEditor.AutoSaveMinutes";
        internal const string PREFS_DIALOG_EDITOR_SAVE_LOGS_KEY = "DialogEditor.SaveLogsEnabled";
        private const int SETTINGS_TOOLBAR_BUTTON_WIDTH = 28;
        private const int AUTO_SAVE_MIN_MINUTES = 1;
        private const int AUTO_SAVE_MAX_MINUTES = 10;
        private const int AUTO_SAVE_DEFAULT_MINUTES = 5;
        private const string NO_VARS_HINT = "-";
        private const string SELECT_VAR_PLACEHOLDER = "-";
        private const string NO_VARS_PLACEHOLDER = "-";
        private const string DIALOG_NAME_FIELD_CONTROL = "DialogEditorDialogNameField";
        private const string DEFAULT_DIALOG_BASE_NAME = "New Dialog";
        private const string DEFAULT_CATEGORY_NAME = "Unnamed";
        private static readonly int[] AUTO_SAVE_MINUTE_VALUES = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        private static readonly string[] AUTO_SAVE_MINUTE_LABELS = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };

        #endregion Constants


        #region Fields

        private DialogDatabase database;

        private Vector2 dialogListScrollPos;
        private Vector2 conditionsScrollPos;
        private Vector2 nameColorsScrollPos;

        private readonly DialogReferenceIndex _dialogReferenceIndex = new DialogReferenceIndex();
        private readonly DialogReferenceIndex _savedDialogReferenceIndex = new DialogReferenceIndex();
        private DialogDatabase _savedReferenceDatabase;
        private int _savedReferenceRevision = -1;
        private bool _dialogReferenceIndexStale = true;

        private bool _dialogEditorAutoSaveEnabled;
        private int _dialogEditorAutoSaveIntervalMinutes = AUTO_SAVE_DEFAULT_MINUTES;
        private double _lastDialogEditorAutoSaveTime = -1;
        private bool _dialogEditorSaveLogsEnabled = true;

        private string dialogSearchFilter = "";

        private List<string> dialogCategories
        {
            get
            {
                if (database == null || database.Categories == null) return new List<string>();
                return database.Categories;
            }
        }

        private List<string> availableLanguages => database.AvailableLanguages;

        private int currentMainTab = 0;
        private int dialogSubTab = 0;
        private string selectedDialogName = null;
        private string _pendingDialogNameEdit = "";
        private string _pendingDialogNameSelectedKey;
        private DialogEntry selectedDialog => _graphDraft ?? GetSelectedDialog();
        private bool showConditions = false;
        private bool isEditingDialog = false;
        private bool isDatabasesLoaded = false;

        private GUIStyle dialogButtonStyle;
        private GUIStyle categoryStyle;
        private Texture2D lineBgTexture;
        private Texture2D _startToolbarTexture;
        private GUIStyle _nodeToolbarStyle;
        private GUIStyle _startToolbarStyle;
        private GUIStyle _actionSliderStyle;
        private GUIStyle _actionSliderThumbStyle;
        private GUIStyle _actionNumberFieldStyle;
        private float _toolbarZoomScale;
        private bool _toolbarProSkin;
        private Color[] lineColors = new Color[] { LINE_COLOR_1, LINE_COLOR_2 };
        private bool stylesInitialized = false;

        private Vector2 editorOffset;
        private Vector2 dragStartPos;
        private bool isDragging;
        private Texture2D gridTexture;
        private float zoomScale = DEFAULT_ZOOM_SCALE;
        private Rect editorRect;

        private Texture2D nodeInactiveInputTexture;
        private Texture2D nodeInactiveOutputTexture;
        private Texture2D nodeActiveInputTexture;
        private Texture2D nodeActiveOutputTexture;

        private List<SelectedPort> selectedPorts = new List<SelectedPort>();

        private SelectedPort pendingPort = null;
        private bool isContextMenuOpen;
        private bool actionWasPerformed;

        private HashSet<DialogNode> selectedNodes = new HashSet<DialogNode>();
        private bool isBoxSelecting;
        private int pressedHeaderWindow = -1;
        private Vector2 boxSelectStart;
        private Vector2 boxSelectCurrent;
        private bool isDraggingSelection;
        private bool graphPortClickHandled;
        private int selectionDragControlId;
        private Vector2 selectionDragStartPos;
        private Dictionary<DialogNode, Vector2> selectionDragStartNodePositions;
        private bool _voiceScrubKeepPlaying;

        private Dictionary<string, bool> languageTextFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, bool> actionFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, bool> choiceLanguageFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, bool> variableChangeFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, bool> conditionFoldouts = new Dictionary<string, bool>();
        private Dictionary<string, bool> categoryFoldouts = new Dictionary<string, bool>();

        private static CharacterActionData copiedActionData = null;
        private static string copiedActionSourceNodeId = null;

        private enum ConditionRowShellLayout
        {
            DialogStartPanel,
            GraphConditionNode,
            ChoiceConditionRow
        }

        private struct ConditionRowShellParams
        {
            public ConditionRowShellLayout Layout;
            public int Index;
            public DialogReferenceState ReferenceState;
            public string VariableName;
            public string MissingReferenceLabel;
            public float ContentWidth;
            public float RowBaseY;
            public float ZoomScale;
            public Rect WarningBoundsRect;
            public GUIStyle LabelStyle;
            public GUIStyle ButtonStyle;
            public bool RequireMouseButton;
            public Event CurrentEvent;
            public string DeleteTooltip;
            public bool UseRedDeleteButton;
        }

        private class SelectedPort
        {
            public DialogNode Node { get; }
            public bool IsInput { get; }
            public int ChoiceIndex { get; }

            public SelectedPort(DialogNode node, bool isInput, int choiceIndex = -1)
            {
                Node = node;
                IsInput = isInput;
                ChoiceIndex = choiceIndex;
            }
        }

        private DialogEntry GetSelectedDialog()
        {
            if (string.IsNullOrEmpty(selectedDialogName) || database == null || database.Dialogs == null) return null;
            return database.Dialogs.FirstOrDefault(d => d != null && d.dialogName == selectedDialogName);
        }


        [MenuItem("Tools/Easy Node Dialog System")]
        public static void ShowWindow()
        {
            GetWindow<DialogEditorWindowED>("Dialog Editor");
        }

        #endregion Fields


        #region Loading

        private void OnEnable()//might need to be done via Start
        {
            titleContent = new GUIContent("Dialog Editor");
            EditorApplication.update += DelayedSave;
            EditorApplication.update += UpdateGraphPerformance;
            EndsSessionED.StateChanged += HandleSessionChanged;

            LoadDatabases();
            EndsAiHighlightED.Bind(database);
            wantsMouseMove = false;

            _dialogEditorAutoSaveEnabled = EditorPrefs.GetBool(PREFS_DIALOG_EDITOR_AUTO_SAVE_KEY, false);
            _dialogEditorAutoSaveIntervalMinutes = Mathf.Clamp(EditorPrefs.GetInt(PREFS_DIALOG_EDITOR_AUTO_SAVE_MINUTES_KEY, AUTO_SAVE_DEFAULT_MINUTES), AUTO_SAVE_MIN_MINUTES, AUTO_SAVE_MAX_MINUTES);
            ReloadDialogEditorPreferences();

            foreach (string category in dialogCategories)
            {
                if (!categoryFoldouts.ContainsKey(category)) categoryFoldouts[category] = true;
            }

            gridTexture = new Texture2D(GRID_TILE_SIZE, GRID_TILE_SIZE) { hideFlags = HideFlags.HideAndDontSave };
            Color backgroundColor = GRID_BG;
            Color lineColor = GRID_LINE;

            for (int x = 0; x < GRID_TILE_SIZE; x++)
            {
                for (int y = 0; y < GRID_TILE_SIZE; y++)
                {
                    if (x == 0 || y == 0) gridTexture.SetPixel(x, y, lineColor);        
                    else gridTexture.SetPixel(x, y, backgroundColor);         
                }
            }

            gridTexture.Apply();
            gridTexture.wrapMode = TextureWrapMode.Repeat;

            nodeInactiveInputTexture = Resources.Load<Texture2D>("Buttons/NodeInactiveInput");
            nodeInactiveOutputTexture = Resources.Load<Texture2D>("Buttons/NodeInactiveOutput");
            nodeActiveInputTexture = Resources.Load<Texture2D>("Buttons/NodeActiveInput");
            nodeActiveOutputTexture = Resources.Load<Texture2D>("Buttons/NodeActiveOutput");
    
            if (nodeInactiveInputTexture == null || nodeInactiveOutputTexture == null || nodeActiveInputTexture == null || nodeActiveOutputTexture == null)
            {
                Debug.LogError("One or more port textures not found in Resources!");
            }
        }

        private void OnDisable()
        {
            ReleaseViewedDialog();
            EditorApplication.update -= DelayedSave;
            EditorApplication.update -= UpdateGraphPerformance;
            EndsSessionED.StateChanged -= HandleSessionChanged;

            if (gridTexture != null) DestroyImmediate(gridTexture);
            if (lineBgTexture != null) DestroyImmediate(lineBgTexture);
            if (_startToolbarTexture != null) DestroyImmediate(_startToolbarTexture);

            gridTexture = null;
            lineBgTexture = null;
            _startToolbarTexture = null;
            dialogButtonStyle = null;
            categoryStyle = null;
            _nodeToolbarStyle = null;
            _startToolbarStyle = null;
            _actionSliderStyle = null;
            _actionSliderThumbStyle = null;
            _actionNumberFieldStyle = null;
            _graphStyles = null;
            _unitGraphStyles = null;
            _windowStyles = null;
            _uiCatalog = null;
            _toolbarSkin = null;
            _listStyleSkin = null;
            stylesInitialized = false;
            EndsEditorAudioPreviewED.ReleaseRepaintTarget(this);

            ClearDialogEditorSessionTracking();
        }

        private void ReloadDialogEditorPreferences()
        {
            _dialogEditorSaveLogsEnabled = EditorPrefs.GetBool(PREFS_DIALOG_EDITOR_SAVE_LOGS_KEY, true);
        }

        private void InvalidateReferenceIndex()
        {
            _dialogReferenceIndexStale = true;
        }

        private DialogReferenceIndex GetDialogReferenceIndex()
        {
            if (_dialogReferenceIndexStale)
            {
                _dialogReferenceIndex.Rebuild(database);
                _dialogReferenceIndexStale = false;
            }

            return _dialogReferenceIndex;
        }

        private DialogReferenceIndex GetLiveDialogReferenceIndex()
        {
            _dialogReferenceIndex.Rebuild(database);
            _dialogReferenceIndexStale = false;
            return _dialogReferenceIndex;
        }

        private void RefreshSavedDialogReferenceIndex()
        {
            if (database == null)
            {
                _savedReferenceDatabase = null;
                _savedReferenceRevision = -1;
                _savedDialogReferenceIndex.Rebuild(null);
                return;
            }

            EndsSessionED session = EndsSessionED.ForDatabase(database);

            if (_savedReferenceDatabase == database && _savedReferenceRevision == session.SavedRevision) return;

            DialogDatabase savedSnapshot = session.ReadSavedDatabase();
            try
            {
                _savedDialogReferenceIndex.Rebuild(savedSnapshot);
                _savedReferenceDatabase = database;
                _savedReferenceRevision = session.SavedRevision;
            }
            finally
            {
                DestroyImmediate(savedSnapshot);
            }
        }

        private string BuildRegistryDeleteUsageMessage(IReadOnlyList<DialogReferenceUsage> liveUsages, IReadOnlyList<DialogReferenceUsage> savedUsages, string footer)
        {
            return DialogReferenceIndex.BuildUsageSummary(liveUsages, savedUsages) + "\n\n" + footer;
        }

        private bool IsOrphanedVariableReference(string variableName, DialogReferenceState referenceState)
        {
            if (DialogReferenceUtility.IsMissing(referenceState)) return false;
            if (string.IsNullOrEmpty(variableName)) return false;
            if (database?.Variables == null) return true;

            return !database.Variables.Any(v => v != null && v.variableName == variableName);
        }

        private bool ShouldDrawMissingVariableSlot(DialogReferenceState referenceState, string variableName)
        {
            if (DialogReferenceUtility.IsMissing(referenceState)) return true;
            return IsOrphanedVariableReference(variableName, referenceState);
        }

        private static string GetMissingVariableDisplayLabel(DialogReferenceState referenceState, string variableName, string missingReferenceLabel)
        {
            if (DialogReferenceUtility.IsMissing(referenceState)) return DialogReferenceMessagesED.FormatMissingVariableLabel(missingReferenceLabel);
            return DialogReferenceMessagesED.FormatMissingVariableLabel(variableName);
        }


        /// <returns>true - row is only missing-slot, fields don't need to be drawn</returns>
        private bool DrawConditionRowShell(ConditionRowShellParams shellParams, List<int> removeIndices, out float fieldsStartY)
        {
            float zs = shellParams.ZoomScale;
            fieldsStartY = shellParams.RowBaseY;

            float headerY;
            if (shellParams.Layout == ConditionRowShellLayout.DialogStartPanel) headerY = shellParams.RowBaseY + CONDITION_TOP_PADDING;
            else if (shellParams.Layout == ConditionRowShellLayout.GraphConditionNode) headerY = shellParams.RowBaseY + 5 * zs;
            else headerY = shellParams.RowBaseY + CHOICE_EVENT_FIELD_TOP_PADDING * zs;

            bool showTitle = shellParams.Layout != ConditionRowShellLayout.ChoiceConditionRow;

            GUIStyle titleStyle;
            if (shellParams.LabelStyle != null) titleStyle = shellParams.LabelStyle;
            else titleStyle = EditorStyles.boldLabel;

            float deleteButtonX;
            float deleteButtonWidth;
            string deleteTooltip;

            if (shellParams.Layout == ConditionRowShellLayout.ChoiceConditionRow)
            {
                deleteButtonX = shellParams.ContentWidth - CHOICE_ITEM_CLOSE_OFFSET * zs;
                deleteButtonWidth = ACTION_BUTTON_SIZE * zs;
                if (string.IsNullOrEmpty(shellParams.DeleteTooltip)) deleteTooltip = "Delete condition";
                else deleteTooltip = shellParams.DeleteTooltip;
            }
            else
            {
                deleteButtonX = shellParams.ContentWidth - CONDITION_CLOSE_BUTTON_OFFSET * zs;
                deleteButtonWidth = CONDITION_CLOSE_BUTTON_WIDTH * zs;
                deleteTooltip = null;
            }

            Rect deleteRect = new Rect(deleteButtonX, headerY, deleteButtonWidth, FIELD_ROW_HEIGHT * zs);

            if (ShouldDrawMissingVariableSlot(shellParams.ReferenceState, shellParams.VariableName))
            {
                DrawMissingReferenceWarning(
                    shellParams.WarningBoundsRect,
                    GetMissingVariableDisplayLabel(shellParams.ReferenceState, shellParams.VariableName, shellParams.MissingReferenceLabel),
                    zs,
                    shellParams.LabelStyle);

                if (showTitle) GUI.Label(new Rect(CONDITION_LABEL_X * zs, headerY, CONDITION_TITLE_LABEL_WIDTH * zs, FIELD_ROW_HEIGHT * zs), $"Condition {shellParams.Index + 1}", titleStyle);

                if (DrawConditionRowDeleteButton(deleteRect, deleteTooltip, shellParams)) removeIndices.Add(shellParams.Index);

                return true;
            }

            if (showTitle) GUI.Label(new Rect(CONDITION_LABEL_X * zs, headerY, CONDITION_TITLE_LABEL_WIDTH * zs, FIELD_ROW_HEIGHT * zs), $"Condition {shellParams.Index + 1}", titleStyle);

            if (DrawConditionRowDeleteButton(deleteRect, deleteTooltip, shellParams)) removeIndices.Add(shellParams.Index);

            if (shellParams.Layout == ConditionRowShellLayout.GraphConditionNode) fieldsStartY = headerY + ACTION_ROW_HEIGHT * zs;
            else if (shellParams.Layout == ConditionRowShellLayout.DialogStartPanel) fieldsStartY = shellParams.RowBaseY + CONDITION_FIELD_Y_OFFSET;
            else fieldsStartY = headerY;

            return false;
        }

        private static bool DrawConditionRowDeleteButton(Rect deleteRect, string deleteTooltip, ConditionRowShellParams shellParams)
        {
            bool clicked;

            if (shellParams.Layout == ConditionRowShellLayout.ChoiceConditionRow)
            {
                GUIContent deleteContent = new GUIContent("×", deleteTooltip);
                if (shellParams.ButtonStyle != null) clicked = GUI.Button(deleteRect, deleteContent, shellParams.ButtonStyle);
                else clicked = GUI.Button(deleteRect, deleteContent);
            }
            else
            {
                if (shellParams.UseRedDeleteButton) GUI.backgroundColor = DELETE_BUTTON_COLOR;

                if (shellParams.ButtonStyle != null) clicked = GUI.Button(deleteRect, "×", shellParams.ButtonStyle);
                else clicked = GUI.Button(deleteRect, "×", EditorStyles.miniButton);

                if (shellParams.UseRedDeleteButton) GUI.backgroundColor = Color.white;
            }

            if (!clicked) return false;
            if (shellParams.RequireMouseButton && shellParams.CurrentEvent != null && shellParams.CurrentEvent.button != 0) return false;

            return true;
        }

        private bool SaveDialogGraphChanges(string logTrigger)
        {
            if (!FlushGraphEdits()) return false;

            InvalidateReferenceIndex();

            return SaveDialogDatabase(logTrigger);
        }

        private void DelayedSave()
        {
            TryAutoSaveDialogEditor();
        }

        private void TryAutoSaveDialogEditor()
        {
            if (_graphGestureActive || _graphDraft != null) return;

            if (!_dialogEditorAutoSaveEnabled || !isEditingDialog)
            {
                _lastDialogEditorAutoSaveTime = -1;
                return;
            }

            if (_lastDialogEditorAutoSaveTime < 0)
            {
                _lastDialogEditorAutoSaveTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (EditorApplication.timeSinceStartup - _lastDialogEditorAutoSaveTime >= _dialogEditorAutoSaveIntervalMinutes * 60.0) SaveDialogEditorSession("Auto-save timer");
        }


        private void RefreshDialogRegistry()
        {
            InvalidateReferenceIndex();
            RefreshManagedListFoldouts();
        }

        private void RefreshManagedListFoldouts()
        {
            foreach (string category in dialogCategories)
            {
                if (!categoryFoldouts.ContainsKey(category)) categoryFoldouts[category] = true;
            }

            foreach (string lang in availableLanguages)
            {
                if (!languageTextFoldouts.ContainsKey(lang)) languageTextFoldouts[lang] = true;
            }
        }

        private void CommitPendingDialogNameEditIfUnfocused()
        {
            if (!isEditingDialog || database == null) return;
            if (GUI.GetNameOfFocusedControl() == DIALOG_NAME_FIELD_CONTROL) return;

            CommitPendingDialogNameEdit();
        }

        private void LoadDatabases()
        {
            try
            {
                database = LoadAssetFromPrefs<DialogDatabase>(PREFS_DIALOG_DATABASE_GUID_KEY, "DialogDatabase");

                if (database != null) database = EndsSessionED.ForDatabase(database).WorkingDatabase;
                if (database != null) EnsureDialogDatabaseData(false);

                if (database != null && string.IsNullOrEmpty(EditorPrefs.GetString(PREFS_DIALOG_DATABASE_GUID_KEY, ""))) SaveAssetGuidToPrefs(PREFS_DIALOG_DATABASE_GUID_KEY, database);

                RefreshSavedDialogReferenceIndex();
                InvalidateReferenceIndex();
                isDatabasesLoaded = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load databases: {ex.Message}");
                database = null;
                isDatabasesLoaded = true;
            }
        }

        private static T LoadAssetFromPrefs<T>(string prefsKey, string resourcesFallbackName) where T : ScriptableObject
        {
            string guid = EditorPrefs.GetString(prefsKey, "");

            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(path))
                {
                    var fromGuid = AssetDatabase.LoadAssetAtPath<T>(path);

                    if (fromGuid != null) return fromGuid;
                }
            }

            return Resources.Load<T>(resourcesFallbackName);
        }

        private static void SaveAssetGuidToPrefs(string prefsKey, ScriptableObject asset)
        {
            if (asset is DialogDatabase dialogDatabase) asset = EndsSessionED.SourceAsset(dialogDatabase);

            if (asset == null)
            {
                EditorPrefs.DeleteKey(prefsKey);
                return;
            }

            string path = AssetDatabase.GetAssetPath(asset);

            if (string.IsNullOrEmpty(path)) return;

            EditorPrefs.SetString(prefsKey, AssetDatabase.AssetPathToGUID(path));
        }

        private void ResetDialogEditorSessionState()
        {
            ResetGraphPorts();
            _graphNodeHeights.Clear();
            _graphChoiceHeights.Clear();
            _graphLayoutNodes.Clear();
            isEditingDialog = false;
            selectedDialogName = null;
            _graphDraft = null;
            _graphView = null;
            _graphBefore = null;
            _graphViewRevision = -1;
            _graphGestureActive = false;
            _graphDataDirty = false;
            _graphEditRequested = false;
            _queuedEditorCommands.Clear();
            selectedNodes.Clear();
            pendingPort = null;
            selectedPorts.Clear();
            editorOffset = Vector2.zero;
            showConditions = false;
            ClearDialogEditorSessionTracking();
        }

        private void ClearDialogEditorSessionTracking()
        {
            _lastDialogEditorAutoSaveTime = -1;
        }

        private void OnDialogDatabaseChanged(DialogDatabase newDatabase)
        {
            if (TrySwitchDialogDatabase(newDatabase, out string error)) return;
            if (string.IsNullOrEmpty(error)) return;

            EditorUtility.DisplayDialog("Dialog graph open", error, "OK");
        }

        private void EnsureDialogDatabaseData(bool saveAfterMigration)
        {
            if (database == null) return;

            EndsSessionED.Result result = EndsDialogGraphContractED.Execute(database, EndsDialogGraphContractED.Command("database.initialize"));

            if (!result.Success) throw new InvalidOperationException(result.Error);

            RefreshManagedListFoldouts();

            if (saveAfterMigration && result.Changed) SaveDialogDatabase("Database initialization");
        }


        private DialogDatabase CreateNewDialogDatabaseAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject( "Create Dialog Database", "DialogDatabase", "asset", "Choose path for the new dialog database");
                
            if (string.IsNullOrEmpty(path)) return null;

            var asset = CreateInstance<DialogDatabase>();
            asset.Categories = new List<string> { DEFAULT_CATEGORY_NAME, "Main Story", "Side Quests", "NPC", "Tutorial" };
            asset.Dialogs = new List<DialogEntry>();
            asset.CharacterNameColors = new List<CharacterNameColor>();
            asset.CharacterGroups = new List<CharacterGroup>();
            asset.SetVariables(new List<VariableDefinition>());
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(asset);

            return asset;
        }

        #endregion Loading


        #region Window Header

        private void DrawInlineDialogDatabaseSlot()
        {
            DrawInlineScriptableObjectSlot(
                "Dialog Database",
                EndsSessionED.SourceAsset(database),
                OnDialogDatabaseChanged,
                CreateNewDialogDatabaseAsset,
                isEditingDialog ? DIALOG_DATABASE_SWITCH_LOCKED_TOOLTIP : null);
        }

        private void DrawToolbarTabRow(ref int selectedTab, GUIContent[] tabs)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            selectedTab = GUILayout.Toolbar(selectedTab, tabs, EditorStyles.toolbarButton, GUILayout.Height(TOOLBAR_HEIGHT));
            EditorGUILayout.EndHorizontal();
        }

        private void EndInlineHeaderSection()
        {
            EditorGUILayout.Space(INLINE_HEADER_SECTION_PADDING);
        }

        private bool AssetRowToolbarButton(GUIContent content, int width)
        {
            return GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.Width(width), GUILayout.Height(ASSET_ACTION_ROW_HEIGHT));
        }

        private void BeginAssetSlotActionRow()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(ASSET_ACTION_ROW_HEIGHT));
        }

        private void EndAssetSlotActionRow()
        {
            EditorGUILayout.EndHorizontal();
        }

        private void FillAssetSlotToolbarRow()
        {
            GUILayout.FlexibleSpace();
        }

        private void DrawSearchRow(ref string filter, Action<string> onFilterChanged = null, bool showClearButton = false)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(ASSET_ACTION_ROW_HEIGHT));
            GUILayout.Label(_searchContent, GUILayout.Width(SEARCH_LABEL_WIDTH), GUILayout.Height(INLINE_ASSET_CONTROL_HEIGHT));
            string newFilter = EditorGUILayout.TextField(filter ?? "", GUILayout.Height(INLINE_ASSET_CONTROL_HEIGHT));

            if (newFilter != filter)
            {
                filter = newFilter;
                onFilterChanged?.Invoke(newFilter);
            }

            if (showClearButton && GUILayout.Button(_clearSearchContent, EditorStyles.toolbarButton, GUILayout.Width(CLEAR_BUTTON_WIDTH), GUILayout.Height(ASSET_ACTION_ROW_HEIGHT)) && !string.IsNullOrEmpty(filter))
            {
                filter = "";
                onFilterChanged?.Invoke("");
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawInlineScriptableObjectSlot<T>( string label, T asset, Action<T> onChanged, Func<T> createNew, string switchLockedTooltip = null) where T : ScriptableObject
        {
            bool switchLocked = !string.IsNullOrEmpty(switchLockedTooltip);
            string assetTooltip = DATABASE_SLOT_TOOLTIP;
            string createTooltip = NEW_ASSET_TOOLTIP;

            if (switchLocked)
            {
                assetTooltip = switchLockedTooltip;
                createTooltip = switchLockedTooltip;
            }

            EditorGUILayout.LabelField( new GUIContent(label, assetTooltip), EditorStyles.miniLabel, GUILayout.Width(INLINE_ASSET_LABEL_WIDTH), GUILayout.Height(INLINE_ASSET_CONTROL_HEIGHT));
            GUILayout.Space(INLINE_ASSET_SLOT_INDENT);
            EditorGUI.BeginDisabledGroup(switchLocked);
            EditorGUI.BeginChangeCheck();
            var newAsset = (T)EditorGUILayout.ObjectField( new GUIContent("", assetTooltip), asset, typeof(T), false, GUILayout.Width(INLINE_ASSET_FIELD_WIDTH), GUILayout.Height(INLINE_ASSET_CONTROL_HEIGHT));

            if (EditorGUI.EndChangeCheck()) onChanged(newAsset);
            if (GUILayout.Button( new GUIContent("New", createTooltip), EditorStyles.toolbarButton, GUILayout.Width(INLINE_ASSET_NEW_BUTTON_WIDTH), GUILayout.Height(INLINE_ASSET_CONTROL_HEIGHT)))
            {
                T created = createNew();
                if (created != null) onChanged(created);
            }

            EditorGUI.EndDisabledGroup();

            if (switchLocked) GUILayout.Label( new GUIContent("⚠️", switchLockedTooltip), EditorStyles.miniLabel, GUILayout.Width(INLINE_ASSET_LOCKED_HINT_WIDTH), GUILayout.Height(INLINE_ASSET_CONTROL_HEIGHT));
        }

        #endregion Window Header


        #region Saving and Validation

        private string GetUniqueDialogName(string baseName, DialogEntry excludeDialog = null)
        {
            return EndsDialogCommandsED.GetUniqueName(database, baseName, excludeDialog?.dialogName);
        }

        private void SyncPendingDialogNameEdit()
        {
            if (selectedDialog == null)
            {
                _pendingDialogNameSelectedKey = null;
                _pendingDialogNameEdit = "";

                return;
            }

            if (_pendingDialogNameSelectedKey == selectedDialogName) return;

            _pendingDialogNameSelectedKey = selectedDialogName;
            _pendingDialogNameEdit = selectedDialog.dialogName ?? "";
        }

        private void CommitPendingDialogNameEdit()
        {
            if (!isEditingDialog || selectedDialog == null || database == null) return;

            SyncPendingDialogNameEdit();
            string finalName = GetUniqueDialogName(_pendingDialogNameEdit, selectedDialog);

            if (selectedDialog.dialogName == finalName)
            {
                _pendingDialogNameEdit = finalName;

                return;
            }

            SubmitNamedEditorCommand("dialog.rename", selectedDialog.dialogName, finalName);
            selectedDialogName = finalName;
            _pendingDialogNameSelectedKey = finalName;
            _pendingDialogNameEdit = finalName;
        }

        private bool SaveDialogEditorSession(string logTrigger)
        {
            CommitPendingDialogNameEdit();

            if (!SaveDialogGraphChanges(logTrigger)) return false;

            CaptureDialogEditorDirtyBaseline();
            _lastDialogEditorAutoSaveTime = EditorApplication.timeSinceStartup;

            return true;
        }

        private bool HasPendingDialogNameEdit()
        {
            if (selectedDialog == null) return false;
            if (_pendingDialogNameSelectedKey != selectedDialogName) return false;

            string pending = _pendingDialogNameEdit ?? "";
            string current = selectedDialog.dialogName ?? "";

            if (pending == current) return false;

            return GetUniqueDialogName(pending, selectedDialog) != current;
        }

        private void RestoreScriptableObjectAssetName(ScriptableObject asset)
        {
            if (asset == null) return;

            string path = AssetDatabase.GetAssetPath(asset);

            if (string.IsNullOrEmpty(path)) return;

            asset.name = Path.GetFileNameWithoutExtension(path);
        }

        private void CaptureDialogEditorDirtyBaseline()
        {
            if (database != null) EndsSessionED.ForDatabase(database).ObserveChanges();
        }

        private bool HasUnsavedDialogGraphChanges()
        {
            if (database == null) return false;
            if (_graphDataDirty || _queuedEditorCommands.Count > 0) return true;
            if (HasPendingDialogNameEdit()) return true;

            return EndsSessionED.ForDatabase(database).IsDialogDirty(selectedDialogName);
        }

        private bool HasUnsavedDialogEditorChanges()
        {
            if (!isEditingDialog || database == null) return false;
            if (EndsSessionED.ExclusiveEditing && EndsSessionED.ForDatabase(database).DialogOwner(selectedDialogName) != null) return false;

            return HasUnsavedDialogGraphChanges();
        }

        private bool DiscardUnsavedDialogDatabaseChanges()
        {
            if (database == null) return false;

            _graphDataDirty = false;
            _graphGestureActive = false;
            _graphBefore = null;
            _graphView = null;
            _queuedEditorCommands.Clear();
            string restoreDialogName = selectedDialogName;
            EndsSessionED.Result result = EndsSessionED.ForDatabase(database).DiscardSelection(new[] { selectedDialogName }, false);

            if (!result.Success)
            {
                EditorUtility.DisplayDialog("Discard failed", result.Error, "OK");

                return false;
            }

            RestoreScriptableObjectAssetName(database);
            selectedDialogName = restoreDialogName;
            _pendingDialogNameSelectedKey = null;
            SyncPendingDialogNameEdit();
            selectedNodes.Clear();
            RefreshSavedDialogReferenceIndex();
            InvalidateReferenceIndex();
            LogDialogEditorDiscard(restoreDialogName);
            EndsAiHighlightED.Bind(database);

            return true;
        }







        private bool TryExitDialogEditorToList()
        {
            CommitPendingDialogNameEdit();

            if (HasUnsavedDialogEditorChanges())
            {
                string dialogLabel = selectedDialog?.dialogName ?? selectedDialogName ?? "dialog";
                int choice = EditorUtility.DisplayDialogComplex("Unsaved changes", $"Dialog '{dialogLabel}' has unsaved changes. Exit without saving keeps them in the working session. Discard restores the saved version.", "Save and exit", "Exit without saving", "Discard and exit");

                if (choice == 2 && !DiscardUnsavedDialogDatabaseChanges()) return false;
                if (choice == 0 && !SaveDialogEditorSession("Back to list - Save")) return false;
            }

            isEditingDialog = false;
            ReleaseViewedDialog();
            ClearDialogEditorSessionTracking();
            EndsAiHighlightED.Bind(database);
            GUI.FocusControl(null);

            return true;
        }

        private bool SaveDialogDatabase(string logTrigger)
        {
            if (database == null) return false;

            EndsSessionED session = EndsSessionED.ForDatabase(database);
            EndsSessionED.Result result;

            if (isEditingDialog) result = session.SaveSelection(new[] { selectedDialogName }, false);
            else result = session.Save();

            if (!result.Success)
            {
                EditorUtility.DisplayDialog("Save failed", result.Error, "OK");

                return false;
            }

            EndsAiHighlightED.SaveBoundMemory();
            RefreshSavedDialogReferenceIndex();
            LogDialogEditorSave(logTrigger);

            return true;
        }


        private void ValidateBrokenLinks()
        {
            if (selectedDialog == null) return;

            List<string> issues = EndsDialogGraphContractED.ReadGraphIssues(database, selectedDialog);

            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("Integrity Check", "No graph issues found.", "OK");

                return;
            }

            System.Text.StringBuilder message = new System.Text.StringBuilder();
            message.AppendLine("Graph issues found: " + issues.Count);

            for (int i = 0; i < Math.Min(issues.Count, BROKEN_LINKS_MAX_SHOW); i++)
            {
                message.AppendLine(issues[i]);
            }

            message.AppendLine("Repair Links fixes invalid transitions and redirect targets. Other issues remain in the report.");

            if (!EditorUtility.DisplayDialog("Integrity Check", message.ToString(), "Repair Links", "Close")) return;

            EndsMcpJsonED command = EndsDialogGraphContractED.Command("graph.repair");
            command.Set("dialog", EndsMcpJsonED.Str(selectedDialog.dialogName));
            SubmitEditorCommand(command);
        }

        private List<EndsDialogSelectionDiagnosticsED.ValidationIssue> GetCategoryStartConditionIssues(string category)
        {
            if (database?.Dialogs == null || string.IsNullOrEmpty(category)) return new List<EndsDialogSelectionDiagnosticsED.ValidationIssue>();

            int revision = EndsSessionED.ForDatabase(database).CategoryRevision(category);

            if (!_categoryIssues.TryGetValue(category, out CategoryIssues cached) || cached.Database != database || cached.Revision != revision)
            {
                cached = new CategoryIssues { Database = database, Revision = revision, Issues = EndsDialogSelectionDiagnosticsED.ValidateCategory(database.Dialogs, category) };
                _categoryIssues[category] = cached;
            }

            return cached.Issues;
        }

        private void ShowDialogStartConditionValidationResults(string scopeTitle, IReadOnlyList<EndsDialogSelectionDiagnosticsED.ValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                EditorUtility.DisplayDialog(scopeTitle, "No overlaps or ambiguities found.", "OK");

                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Issues found: {issues.Count}");
            sb.AppendLine("When conditions match, the first suitable dialog in the Dialog Database list is triggered.");
            sb.AppendLine();
            int maxShow = START_CONDITION_ISSUES_MAX_SHOW;

            for (int i = 0; i < Math.Min(issues.Count, maxShow); i++)
            {
                sb.AppendLine($"• {issues[i].Message}");
            }

            if (issues.Count > maxShow) sb.AppendLine($"... and {issues.Count - maxShow} more");

            EditorUtility.DisplayDialog(scopeTitle, sb.ToString(), "OK");
        }

        private void ValidateCurrentCategoryStartConditions()
        {
            if (selectedDialog == null) return;

            string category = selectedDialog.initiator;

            if (string.IsNullOrEmpty(category)) category = DEFAULT_CATEGORY_NAME;

            List<EndsDialogSelectionDiagnosticsED.ValidationIssue> issues = GetCategoryStartConditionIssues(category);
            ShowDialogStartConditionValidationResults($"Conditions for category '{category}'", issues);
        }

        private void ValidateAllCategoriesStartConditions()
        {
            if (database?.Dialogs == null) return;

            var categories = new HashSet<string>();

            for (int i = 0; i < database.Dialogs.Count; i++)
            {
                DialogEntry dialog = database.Dialogs[i];
                if (dialog == null) continue;

                string category = dialog.initiator;
                if (string.IsNullOrEmpty(category)) category = DEFAULT_CATEGORY_NAME;
                categories.Add(category);
            }

            var allIssues = new List<EndsDialogSelectionDiagnosticsED.ValidationIssue>();

            foreach (string category in categories)
            {
                allIssues.AddRange(GetCategoryStartConditionIssues(category));
            }

            ShowDialogStartConditionValidationResults("Conditions of all categories", allIssues);
        }


        private void RemoveDialogNode(DialogEntry dialog, DialogNode node)
        {
            if (_graphDraft != null) EndsGraphCommandsED.Remove(dialog, node);
            else SubmitEditorCommand(EndsDialogGraphContractED.NodeCommand("node.remove", dialog.dialogName, node.nodeId));
            selectedPorts.RemoveAll(p => p.Node == node);

            if (pendingPort != null && pendingPort.Node == node) pendingPort = null;

            selectedNodes.Remove(node);
        }

        #endregion Saving and Validation


        #region OnGUI

        private void DrawEditorGUI()
        {
            Event current = Event.current;

            if (current != null && (current.isMouse || current.isKey))
            {
                bool altHover = current.alt && EndsAiHighlightED.Enabled;

                if (wantsMouseMove != altHover) wantsMouseMove = altHover;
                if (altHover && (current.type == EventType.MouseMove || current.type == EventType.KeyDown)) Repaint();
            }

            if (database != null)
            {
                EndsSessionED session = EndsSessionED.ForDatabase(database);
                database = session.WorkingDatabase;

                if (session.HasExternalAssetChange)
                {
                    hasUnsavedChanges = session.Dirty;
                    EditorGUILayout.HelpBox("This database changed outside ENDS, for example during package import. Your pending work has been preserved and saving is blocked. Choose which content to continue with.", MessageType.Warning);

                    try
                    {
                        if (GUILayout.Button("Review pending work against imported database")) session.ResolveExternalAssetChange(keepWorking: true);
                        if (GUILayout.Button("Use imported database") && EditorUtility.DisplayDialog("Use imported database",
                            "Discard the recovered pending work and load the imported database? The imported asset will not be overwritten.", "Discard pending work", "Cancel")) session.ResolveExternalAssetChange(keepWorking: false);
                    }
                    catch (Exception exception)
                    {
                        EditorUtility.DisplayDialog("External database change", exception.Message, "OK");
                    }

                    return;
                }
            }

            RefreshViewedDialog();
            saveChangesMessage = "This dialog has unsaved ENDS workspace changes.";

            if (database == null)
            {
                hasUnsavedChanges = false;
                DrawEditorView();

                return;
            }

            using (OpenEditorReadOnlyView())
            using (EndsAiHighlightED.OpenView(database))
            {
                hasUnsavedChanges = isEditingDialog && HasUnsavedDialogEditorChanges();
                DrawEditorView();
            }
        }

        private void DrawEditorView()
        {
            _graphGuiOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);

            if (!stylesInitialized)
            {
                lineBgTexture = MakeTex(1, 1, Color.white);
                _startToolbarTexture = MakeTex(1, 1, ORANGE_TOOLBAR);
                stylesInitialized = true;
            }

            if (dialogButtonStyle == null || categoryStyle == null || _listStyleSkin != GUI.skin || _listStyleProSkin != EditorGUIUtility.isProSkin)
            {
                dialogButtonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(NODE_LEFT_RIGHT_PADDING, NODE_LEFT_RIGHT_PADDING, NODE_BOTTOM_PADDING, NODE_BOTTOM_PADDING) };
                categoryStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = CATEGORY_FONT_SIZE, margin = new RectOffset(4, 4, 6, 2) };
                _listStyleSkin = GUI.skin;
                _listStyleProSkin = EditorGUIUtility.isProSkin;
            }

            if (!isDatabasesLoaded)
            {
                EditorGUILayout.HelpBox("Initializing editor...", MessageType.Info);
                return;
            }

            DrawToolbarTabRow(ref currentMainTab, _mainTabs);

            switch (currentMainTab)
            {
                case 0:
                    DrawToolbarTabRow(ref dialogSubTab, _dialogTabs);

                    if (dialogSubTab == 0)
                    {
                        if (isEditingDialog) DrawDialogEditor();
                        else DrawDialogList();
                    }
                    else DrawRegistryEditor(DrawCharacterNameColors);

                    break;

                case 1:
                    DrawRegistryEditor(DrawCharacterEditor);
                    break;

                case 2:
                    DrawRegistryEditor(DrawVariableEditor);
                    break;
            }

            // Escape - cancel connection waiting
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape && pendingPort != null)
            {
                selectedPorts.Clear();
                pendingPort = null;
            }
            // Clear ports only when not waiting for connection (pendingPort), otherwise the user won't have time to click the target port
            else if (!isContextMenuOpen && !actionWasPerformed && selectedPorts.Count > 0 && pendingPort == null)
            {
                selectedPorts.Clear();
                pendingPort = null;
            }
                }

        #endregion OnGUI


        #region Dialog List

        private void DrawDialogList()
        {
            const int DIALOG_LIST_TOOLBAR_BUTTON_WIDTH = 150;
            const int SPACE_BEFORE_DIALOG_LIST = 18;
            const int MENU_BUTTON_WIDTH = 30;

            BeginAssetSlotActionRow();
            DrawInlineDialogDatabaseSlot();
            GUILayout.FlexibleSpace();
            GUI.enabled = database != null;

            if (AssetRowToolbarButton(_addDialogContent, DIALOG_LIST_TOOLBAR_BUTTON_WIDTH))
            {
                GenericMenu menu = new GenericMenu();
                foreach (string category in dialogCategories)
                {
                    string cat = category;
                    menu.AddItem(new GUIContent(cat), false, GraphCallback(() =>
                    {
                        EndsMcpJsonED command = EndsDialogGraphContractED.Command("dialog.add");
                        command.Set("name", EndsMcpJsonED.Str(GetUniqueDialogName(DEFAULT_DIALOG_BASE_NAME)));
                        command.Set("category", EndsMcpJsonED.Str(cat));
                        command.Set("position", EndsDialogGraphContractED.Point(DEFAULT_POSITION_XY, DEFAULT_POSITION_XY));
                        SubmitEditorCommand(command);
                    }));
                }
                menu.ShowAsContext();
            }

            if (AssetRowToolbarButton(_manageCategoriesContent, DIALOG_LIST_TOOLBAR_BUTTON_WIDTH)) PopupWindow.Show(new Rect(Event.current.mousePosition, Vector2.zero), new DialogCategoryPopup(database, _ => RefreshDialogRegistry()));
            if (AssetRowToolbarButton(_manageLanguagesContent, DIALOG_LIST_TOOLBAR_BUTTON_WIDTH)) PopupWindow.Show(new Rect(Event.current.mousePosition, Vector2.zero), new LanguageManagementPopup(database, _ => RefreshDialogRegistry()));
            if (AssetRowToolbarButton(_importExportContent, DIALOG_LIST_TOOLBAR_BUTTON_WIDTH)) PopupWindow.Show(new Rect(Event.current.mousePosition, Vector2.zero), new DialogLocalizationImportExportPopup(database, OnLocalizationImportApplied));
            if (AssetRowToolbarButton(_checkAllConditionsContent, DIALOG_LIST_TOOLBAR_BUTTON_WIDTH)) ValidateAllCategoriesStartConditions();
            if (AssetRowToolbarButton(_settingsContent, SETTINGS_TOOLBAR_BUTTON_WIDTH))
            {
                PopupWindow.Show(new Rect(Event.current.mousePosition, Vector2.zero), new DialogEditorSettingsPopup(database, logTrigger =>
                {
                    RefreshDialogRegistry();
                    Repaint();
                },
                () =>
                {
                    ReloadDialogEditorPreferences();
                    Repaint();
                }));
            }

            GUI.enabled = true;
            EndAssetSlotActionRow();

            if (database == null)
            {
                EndInlineHeaderSection();
                EditorGUILayout.HelpBox("Assign or create a Dialog Database.", MessageType.Warning);
                return;
            }

            if (database.Dialogs == null)
            {
                EditorGUILayout.HelpBox("The database has no dialog registry.", MessageType.Warning);

                if (GUILayout.Button("Initialize database")) SubmitEditorCommand(EndsDialogGraphContractED.Command("database.initialize"));

                EndInlineHeaderSection();

                return;
            }

            EditorGUILayout.Space(INLINE_HEADER_SECTION_PADDING);

            DrawSearchRow(ref dialogSearchFilter, _ =>
            {
                if (string.IsNullOrEmpty(dialogSearchFilter)) return;

                foreach (string category in dialogCategories)
                {
                    categoryFoldouts[category] = true;
                }

            });

            EndInlineHeaderSection();
            dialogListScrollPos = EditorGUILayout.BeginScrollView(dialogListScrollPos);

            var groupedDialogs = GetUiCatalog().GetDialogGroups(dialogSearchFilter);

            foreach (var categoryGroup in groupedDialogs)
            {
                string category = categoryGroup.Key;
                List<DialogEntry> dialogs = categoryGroup.Value;

                if (!categoryFoldouts.ContainsKey(category)) categoryFoldouts[category] = true;

                GUILayout.Space(SPACE_AFTER_CATEGORY);
                EditorGUILayout.BeginHorizontal(GUILayout.Height(CATEGORY_HEADER_HEIGHT));
                Rect catRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(CATEGORY_HEADER_HEIGHT), GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(catRect, CATEGORY_BG);
                GUILayout.Space(SPACE_BEFORE_DIALOG_LIST);
                categoryFoldouts[category] = EditorGUILayout.Foldout(categoryFoldouts[category], $"Category name: {category}", true, categoryStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (categoryFoldouts[category])
                {
                    List<EndsDialogSelectionDiagnosticsED.ValidationIssue> categoryIssues = GetCategoryStartConditionIssues(category);

                    if (categoryIssues.Count > 0)
                    {
                        EditorGUILayout.HelpBox(EndsDialogSelectionDiagnosticsED.FormatSummary(categoryIssues, 2), MessageType.Warning);
                    }

                    for (int i = 0; i < dialogs.Count; i++)
                    {
                        var dialog = dialogs[i];
                        EditorGUILayout.BeginHorizontal(GUILayout.Height(DIALOG_ROW_HEIGHT));
                        GUILayout.Space(SPACE_AFTER_CATEGORY);
                        GUIContent icon;

                        if (dialog.isLocked) icon = _lockedDialogContent;
                        else if (dialog.conditions != null && dialog.conditions.Count > 0) icon = _conditionalDialogContent;
                        else icon = _unconditionalDialogContent;

                        GUILayout.Label(icon, GUILayout.Width(DIALOG_ICON_SIZE), GUILayout.Height(DIALOG_ICON_SIZE));

                        Color dialogRowColor = GUI.backgroundColor;
                        string dialogHighlightKey = EndsNodeDrawersED.Root<DialogEntry>(dialog.dialogName).Key;

                        if (EndsAiHighlightED.IsMarked(dialogHighlightKey)) GUI.backgroundColor = EndsAiHighlightED.Tint(dialogHighlightKey);

                        if (GUILayout.Button(dialog.dialogName ?? DEFAULT_CATEGORY_NAME, dialogButtonStyle, GUILayout.Height(CATEGORY_HEADER_HEIGHT)))
                        {
                            if (database.Dialogs.Any(d => d != null && d.dialogName == dialog.dialogName))
                            {
                                selectedDialogName = dialog.dialogName;
                                selectedNodes.Clear();
                                isDraggingSelection = false;
                                isEditingDialog = true;
                                CaptureDialogEditorDirtyBaseline();
                                var startNode = dialog.nodes.Find(n => n.NodeType == NodeType.Start);

                                if (startNode != null) editorOffset = startNode.Position - new Vector2(DEFAULT_POSITION_XY, DEFAULT_POSITION_XY);
                                else editorOffset = Vector2.zero;
                            }
                        }

                        EndsAiHighlightED.ReleaseIfAltHovered(dialogHighlightKey, GUILayoutUtility.GetLastRect());
                        GUI.backgroundColor = dialogRowColor;

                        if (GUILayout.Button(_dialogMenuContent, GUILayout.Width(MENU_BUTTON_WIDTH), GUILayout.Height(CATEGORY_HEADER_HEIGHT))) ShowDialogContextMenu(dialog);

                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void ShowDialogContextMenu(DialogEntry dialog)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Duplicate"), false, GraphCallback(() =>
            {
                SubmitNamedEditorCommand("dialog.duplicate", dialog.dialogName);
            }));
            menu.AddSeparator("");

            foreach (string cat in dialogCategories)
            {
                string category = cat;
                menu.AddItem(new GUIContent("Move to/" + category), dialog.initiator == category, GraphCallback(() =>
                {
                    EndsMcpJsonED patch = EndsMcpJsonED.Obj();
                    patch.Set(nameof(DialogEntry.initiator), EndsMcpJsonED.Str(category));
                    SubmitDialogPatch(dialog.dialogName, patch);
                }));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete"), false, GraphCallback(() =>
            {
                string confirmMessage = BuildDeleteDialogConfirmMessage(dialog.dialogName);
                if (EditorUtility.DisplayDialog("Delete dialog", confirmMessage, "Yes", "Cancel"))
                {
                    if (database.Dialogs.Contains(dialog))
                    {
                        SubmitNamedEditorCommand("dialog.remove", dialog.dialogName);

                        if (selectedDialogName == dialog.dialogName)
                        {
                            selectedDialogName = null;
                            isEditingDialog = false;
                            selectedPorts.Clear();
                            selectedNodes.Clear();
                            isDraggingSelection = false;
                            pendingPort = null;
                        }

                        InvalidateReferenceIndex();
                    }
                }
            }));

            menu.ShowAsContext();
        }

        #endregion Dialog List

        #region Variable Helpers (Graph and Editor)

        private string[] GetVariableNamesByType(ConditionVariableType type)
        {
            if (database == null) return Array.Empty<string>();

            switch (type)
            {
                case ConditionVariableType.Bool:
                    return GetUiCatalog().GetVariableNames(type);
                case ConditionVariableType.Int:
                    return GetUiCatalog().GetVariableNames(type);
                case ConditionVariableType.Float:
                    return GetUiCatalog().GetVariableNames(type);
                case ConditionVariableType.String:
                    return GetUiCatalog().GetVariableNames(type);
                default:
                    Debug.LogWarning($"Unknown variable type: {type}");
                    return GetUiCatalog().GetVariableNames(type);
            }
        }

















        private static string[] FilterVariableNamesForSelection(string[] variableNames, string currentVariableName, IReadOnlyList<string> excludedVariableNames)
        {
            if (variableNames == null || variableNames.Length == 0) return Array.Empty<string>();
            if (excludedVariableNames == null || excludedVariableNames.Count == 0) return variableNames;

            HashSet<string> excluded = new HashSet<string>();

            for (int i = 0; i < excludedVariableNames.Count; i++)
            {
                string name = excludedVariableNames[i];
                if (string.IsNullOrEmpty(name)) continue;
                if (name == currentVariableName) continue;
                excluded.Add(name);
            }

            if (excluded.Count == 0) return variableNames;

            List<string> filtered = new List<string>(variableNames.Length);

            for (int i = 0; i < variableNames.Length; i++)
            {
                string name = variableNames[i];

                if (excluded.Contains(name)) continue;

                filtered.Add(name);
            }

            return filtered.ToArray();
        }

        private static ConditionOperator[] GetAllowedNumericConditionOperators(IReadOnlyList<DialogCondition> conditions, int excludeIndex, string variableName)
        {
            return EndsDialogRulesED.GetAllowedNumericConditionOperators(conditions, excludeIndex, variableName);
        }

        private static ConditionOperator[] GetAllowedNumericConditionOperators(IReadOnlyList<ChoiceCondition> conditions, int excludeIndex, string variableName)
        {
            return EndsDialogRulesED.GetAllowedNumericConditionOperators(conditions, excludeIndex, variableName);
        }

        private static List<string> CollectUsedVariableNames(IReadOnlyList<DialogCondition> conditions, int excludeIndex)
        {
            return EndsDialogRulesED.CollectUsedVariableNames(conditions, excludeIndex);
        }

        private static List<string> CollectUsedVariableNames(IReadOnlyList<ChoiceCondition> conditions, int excludeIndex)
        {
            return EndsDialogRulesED.CollectUsedVariableNames(conditions, excludeIndex);
        }

        private static List<string> CollectUsedVariableNames(IReadOnlyList<ChoiceConsequence> consequences, int excludeIndex)
        {
            return EndsDialogRulesED.CollectUsedVariableNames(consequences, excludeIndex);
        }

        private static string DrawVariableNamePopup(Rect rect, string[] variableNames, string currentVariableName, GUIStyle style = null, IReadOnlyList<string> excludedVariableNames = null, string highlightKey = null)
        {
            string[] selectableVariableNames = FilterVariableNamesForSelection(variableNames, currentVariableName, excludedVariableNames);

            if (selectableVariableNames == null || selectableVariableNames.Length == 0)
            {
                EditorGUI.BeginDisabledGroup(true);
                Color disabledColor = EndsAiHighlightED.BeginField(highlightKey);
                EditorGUI.TextField(rect, NO_VARS_PLACEHOLDER, style ?? EditorStyles.textField);
                EndsAiHighlightED.EndField(highlightKey, rect, disabledColor);
                EditorGUI.EndDisabledGroup();
                return currentVariableName;
            }

            string[] displayOptions = new string[selectableVariableNames.Length + 1];
            displayOptions[0] = SELECT_VAR_PLACEHOLDER;

            for (int i = 0; i < selectableVariableNames.Length; i++) 
            {
                displayOptions[i + 1] = selectableVariableNames[i];
            }

            int selectedIndex;

            if (string.IsNullOrEmpty(currentVariableName)) selectedIndex = 0;
            else selectedIndex = Array.IndexOf(selectableVariableNames, currentVariableName) + 1;
            
            if (selectedIndex <= 0) selectedIndex = 0;

            EditorGUI.BeginChangeCheck();
            Color fieldColor = EndsAiHighlightED.BeginField(highlightKey);
            int newIndex = EditorGUI.Popup(rect, selectedIndex, displayOptions, style ?? EditorStyles.popup);
            EndsAiHighlightED.EndField(highlightKey, rect, fieldColor);

            if (!EditorGUI.EndChangeCheck()) return currentVariableName;

            if (newIndex == 0) return null;

            return selectableVariableNames[newIndex - 1];
        }

        private void DrawMissingReferenceWarning(Rect rect, string message, float zoomScale, GUIStyle scaledLabelStyle = null)
        {
            GUIStyle style;

            GraphStyles styles = GetGraphStyles(zoomScale);

            if (scaledLabelStyle == null) style = styles.MissingDefaultWarning;
            else if (ReferenceEquals(scaledLabelStyle, styles.Label)) style = styles.MissingWarning;
            else style = new GUIStyle(scaledLabelStyle) { wordWrap = true, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };

            string fullText = MISSING_WARNING_EMOJI_PREFIX + message;
            float textHeight = style.CalcHeight(new GUIContent(fullText), rect.width);

            if (textHeight > rect.height) textHeight = rect.height;

            float textY = rect.y + (rect.height - textHeight) * 0.5f;
            Rect textRect = new Rect(rect.x, textY, rect.width, textHeight);
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 0.82f, 0.35f);
            GUI.Label(textRect, fullText, style);
            GUI.color = previousColor;
        }

        private float DrawLineVoiceClipRow(LineText localizedText, float currentY, float contentWidthZS, float zoomScale, GUIStyle labelStyle, GUIStyle buttonStyle, Event e, string highlightKey)
        {
            EndsEditorAudioPreviewED.SetRepaintTarget(this);
            float x = 0f;
            float rowH = FIELD_ROW_HEIGHT * zoomScale;
            float gap = LINE_VOICE_FIELD_GAP * zoomScale;
            Rect labelRect = new Rect(x, currentY, LINE_VOICE_LABEL_WIDTH * zoomScale, rowH);
            GUI.Label(labelRect, "Voice:", labelStyle);
            x += LINE_VOICE_LABEL_WIDTH * zoomScale + gap;
            float fieldW = LINE_VOICE_OBJECT_FIELD_WIDTH * zoomScale;
            Rect fieldRect = new Rect(x, currentY, fieldW, rowH);
            EditorGUI.BeginChangeCheck();
            Color clipColor = EndsAiHighlightED.BeginField(highlightKey);
            AudioClip newClip = (AudioClip)EditorGUI.ObjectField(fieldRect, localizedText.VoiceClip, typeof(AudioClip), false);
            EndsAiHighlightED.EndField(highlightKey, fieldRect, clipColor);

            if (EditorGUI.EndChangeCheck())
            {
                if (EndsEditorAudioPreviewED.ActiveClip == localizedText.VoiceClip) EndsEditorAudioPreviewED.Stop();
                RecordEditorIntent("Change Line Voice Clip");
                localizedText.VoiceClip = newClip;
            }

            x += fieldW + LINE_VOICE_OBJECT_TO_SCRUB_GAP * zoomScale;
            AudioClip clip = localizedText.VoiceClip;
            float duration = EndsEditorAudioPreviewED.GetDuration(clip);
            float currentTime = EndsEditorAudioPreviewED.GetCurrentTime(clip);
            float playW = LINE_VOICE_PLAY_WIDTH * zoomScale;
            float timeW = LINE_VOICE_COMBINED_TIME_WIDTH * zoomScale;
            float scrubW = contentWidthZS - x - timeW - playW - gap;

            if (scrubW < LINE_VOICE_SCRUBBER_MIN_WIDTH * zoomScale) scrubW = LINE_VOICE_SCRUBBER_MIN_WIDTH * zoomScale;

            Rect scrubRect = new Rect(x, currentY + (rowH - LINE_VOICE_SCRUBBER_HEIGHT * zoomScale) * 0.5f, scrubW, LINE_VOICE_SCRUBBER_HEIGHT * zoomScale);
            Rect scrubHitRect = new Rect(x, currentY, scrubW, rowH);
            currentTime = DrawVoiceScrubber(scrubRect, scrubHitRect, clip, duration, currentTime, e);
            x += scrubW + gap;

            GUIStyle timeStyle = GetGraphStyles(zoomScale).TimeLabel;

            Rect timeRect = new Rect(x, currentY, timeW, rowH);
            string timeLabel = EndsEditorAudioPreviewED.FormatTime(currentTime) + "/" + EndsEditorAudioPreviewED.FormatTime(duration);
            GUI.Label(timeRect, timeLabel, timeStyle);
            x += timeW;
            Rect playRect = new Rect(x, currentY, playW, rowH);
            GUIContent playContent = _playVoiceContent;

            if (EndsEditorAudioPreviewED.IsActivelyPlaying(clip))
            {
                playContent = _pauseVoiceContent;
            }

            EditorGUI.BeginDisabledGroup(clip == null);

            if (GUI.Button(playRect, playContent, buttonStyle) && e.button == 0) EndsEditorAudioPreviewED.TogglePlayPause(clip);

            EditorGUI.EndDisabledGroup();

            return currentY + LINE_NODE_AUDIO_ROW_HEIGHT * zoomScale;
        }

        private float DrawVoiceScrubber(Rect scrubRect, Rect hitRect, AudioClip clip, float duration, float currentTime, Event e)
        {
            Color trackColor;
            Color fillColor;
            Color handleColor;

            if (EditorGUIUtility.isProSkin)
            {
                trackColor = new Color(0.15f, 0.15f, 0.15f, 1f);
                fillColor = new Color(0.28f, 0.45f, 0.72f, 1f);
                handleColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            }
            else
            {
                trackColor = new Color(0.65f, 0.65f, 0.65f, 1f);
                fillColor = new Color(0.35f, 0.55f, 0.85f, 1f);
                handleColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            }

            if (clip == null || duration <= 0f)
            {
                EditorGUI.DrawRect(scrubRect, trackColor);
                return currentTime;
            }

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            bool isScrubbing = GUIUtility.hotControl == controlId;
            EventType eventType = e.GetTypeForControl(controlId);
            Vector2 mousePos = Event.current.mousePosition;

            if (eventType == EventType.MouseDown && e.button == 0 && hitRect.Contains(mousePos))
            {
                GUIUtility.hotControl = controlId;
                GUIUtility.keyboardControl = 0;
                _voiceScrubKeepPlaying = EndsEditorAudioPreviewED.IsActivelyPlaying(clip);

                if (_voiceScrubKeepPlaying) EndsEditorAudioPreviewED.Pause();

                currentTime = SeekTimeFromMouse(scrubRect, duration, mousePos.x);
                EndsEditorAudioPreviewED.Seek(clip, currentTime);
                e.Use();
                isScrubbing = true;
            }
            else if (eventType == EventType.MouseDrag && GUIUtility.hotControl == controlId)
            {
                currentTime = SeekTimeFromMouse(scrubRect, duration, mousePos.x);
                EndsEditorAudioPreviewED.Seek(clip, currentTime);
                e.Use();
                isScrubbing = true;
            }
            else if (eventType == EventType.MouseUp && GUIUtility.hotControl == controlId)
            {
                currentTime = SeekTimeFromMouse(scrubRect, duration, mousePos.x);
                EndsEditorAudioPreviewED.Seek(clip, currentTime);

                if (_voiceScrubKeepPlaying) EndsEditorAudioPreviewED.PlayFromTime(clip, currentTime);

                GUIUtility.hotControl = 0;
                _voiceScrubKeepPlaying = false;
                e.Use();
            }
            else if (isScrubbing) currentTime = EndsEditorAudioPreviewED.GetCurrentTime(clip);

            float progress = Mathf.Clamp01(currentTime / duration);
            EditorGUI.DrawRect(scrubRect, trackColor);
            Rect fillRect = new Rect(scrubRect.x, scrubRect.y, scrubRect.width * progress, scrubRect.height);
            EditorGUI.DrawRect(fillRect, fillColor);
            float handleX = scrubRect.x + scrubRect.width * progress;
            const float HANDLE_WIDTH = 3f;
            EditorGUI.DrawRect(new Rect(handleX - HANDLE_WIDTH * 0.5f, scrubRect.y, HANDLE_WIDTH, scrubRect.height), handleColor);

            return currentTime;
        }

        private static float SeekTimeFromMouse(Rect scrubRect, float duration, float mouseX)
        {
            float width = scrubRect.width;

            if (width <= 0.0001f) return 0f;

            float normalized = (mouseX - scrubRect.x) / width;

            if (normalized < 0f) normalized = 0f;
            else if (normalized > 1f) normalized = 1f;

            return normalized * duration;
        }

        private struct ConditionFieldsDrawContext
        {
            public float ZoomScale;
            public float ContentWidth;
            public GUIStyle LabelStyle;
            public GUIStyle PopupStyle;
            public GUIStyle TextFieldStyle;
            public GUIStyle ButtonStyle;
            public bool BlockInteraction;
            public bool RequireMouseButton;
            public Event CurrentEvent;
            public bool CompactOperatorLabels;
            public Action OnChanged;
            public string OperatorKey;
            public string ValueKey;
        }

        private bool DrawZoomScaledBoolToggle(Rect rect, bool value, float zoomScale, GUIStyle buttonStyle)
        {
            float size = Mathf.Min(rect.width, rect.height);

            if (size <= 0f) size = FIELD_ROW_HEIGHT * zoomScale;

            Rect boxRect = new Rect(rect.x, rect.y + (rect.height - size) * 0.5f, size, size);

            GUIStyle style = buttonStyle;

            if (style == null)
            {
                style = GetGraphStyles(zoomScale).BoolToggle;
            }

            string label = "";

            if (value) label = "✓";
            if (GUI.Button(boxRect, label, style)) return !value;

            return value;
        }

        private void DrawConditionOperatorValueUI(
            ConditionVariableType variableType,
            string variableName,
            ConditionOperator conditionOperator,
            bool boolValue,
            int intValue,
            float floatValue,
            string stringValue,
            Rect operatorFieldRect,
            Rect valueFieldRect,
            ConditionFieldsDrawContext ctx,
            Action<ConditionOperator> setOperator,
            Action<bool> setBool,
            Action<int> setInt,
            Action<float> setFloat,
            Action<string> setString,
            string undoOperatorLabel,
            string undoValueLabelPrefix,
            ConditionOperator[] allowedNumericOperators = null)
        {
            GUIStyle textFieldStyle = ctx.TextFieldStyle ?? EditorStyles.textField;

            if (ctx.BlockInteraction) GUI.enabled = false;
            else GUI.enabled = !string.IsNullOrEmpty(variableName);

            Color operatorColor = EndsAiHighlightED.BeginField(ctx.OperatorKey);

            if (variableType == ConditionVariableType.Bool || variableType == ConditionVariableType.String)
            {
                string[] stringOperators;

                if (ctx.CompactOperatorLabels) stringOperators = new[] { "Equals", "Not Equals" };
                else stringOperators = new[] { "Equals", "NotEquals" };

                int opIndex;

                if (conditionOperator == ConditionOperator.Equals) opIndex = 0;
                else opIndex = 1;

                if (variableType == ConditionVariableType.String && conditionOperator != ConditionOperator.Equals && conditionOperator != ConditionOperator.NotEquals) opIndex = 0;

                int newOpIndex = EditorGUI.Popup(operatorFieldRect, opIndex, stringOperators, ctx.PopupStyle ?? EditorStyles.popup);

                if (newOpIndex != opIndex && (!ctx.RequireMouseButton || ctx.CurrentEvent.button == 0))
                {
                    if (ctx.OnChanged == null) RecordEditorIntent(undoOperatorLabel);

                    setOperator(EndsDialogRulesED.GetSupportedConditionOperators(variableType)[newOpIndex]);
                }
            }
            else
            {
                ConditionOperator[] selectableOperators = allowedNumericOperators ?? EndsDialogRulesED.GetAllowedNumericConditionOperators((IReadOnlyList<DialogCondition>)null, -1, null);

                if (selectableOperators.Length == 0)
                {
                    string blockedOperatorLabel = conditionOperator.ToString();
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUI.Popup(operatorFieldRect, 0, new[] { blockedOperatorLabel }, ctx.PopupStyle ?? EditorStyles.popup);
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    int operatorIndex = Array.IndexOf(selectableOperators, conditionOperator);
                    bool preserveExistingOperator = operatorIndex < 0;

                    if (preserveExistingOperator)
                    {
                        ConditionOperator[] preservedOperators = new ConditionOperator[selectableOperators.Length + 1];
                        preservedOperators[0] = conditionOperator;
                        Array.Copy(selectableOperators, 0, preservedOperators, 1, selectableOperators.Length);
                        selectableOperators = preservedOperators;
                        operatorIndex = 0;
                    }

                    GUIContent[] operatorLabels = new GUIContent[selectableOperators.Length];

                    for (int i = 0; i < selectableOperators.Length; i++) 
                    {
                        operatorLabels[i] = new GUIContent(selectableOperators[i].ToString());
                    }

                    if (preserveExistingOperator) operatorLabels[0] = new GUIContent(conditionOperator + " (current)", "The current operator is preserved. Select a different operator to change it.");

                    int newOperatorIndex = EditorGUI.Popup(operatorFieldRect, operatorIndex, operatorLabels, ctx.PopupStyle ?? EditorStyles.popup);

                    if (newOperatorIndex != operatorIndex && (!ctx.RequireMouseButton || ctx.CurrentEvent.button == 0))
                    {
                        if (ctx.OnChanged == null) RecordEditorIntent(undoOperatorLabel);

                        setOperator(selectableOperators[newOperatorIndex]);
                    }
                }
            }

            EndsAiHighlightED.EndField(ctx.OperatorKey, operatorFieldRect, operatorColor);
            Color valueColor = EndsAiHighlightED.BeginField(ctx.ValueKey);

            switch (variableType)
            {
                case ConditionVariableType.Bool:
                    bool newBoolValue = DrawZoomScaledBoolToggle(valueFieldRect, boolValue, ctx.ZoomScale, ctx.ButtonStyle);

                    if (newBoolValue != boolValue && (!ctx.RequireMouseButton || ctx.CurrentEvent.button == 0))
                    {
                        if (ctx.OnChanged == null) RecordEditorIntent(undoValueLabelPrefix + " Bool Value");

                        setBool(newBoolValue);
                    }

                    break;
                case ConditionVariableType.Int:
                    EditorGUI.BeginChangeCheck();

                    int newIntValue = EditorGUI.IntField(valueFieldRect, intValue, textFieldStyle);

                    if (EditorGUI.EndChangeCheck() && (!ctx.RequireMouseButton || ctx.CurrentEvent.button == 0))
                    {
                        if (ctx.OnChanged == null) RecordEditorIntent(undoValueLabelPrefix + " Int Value");

                        setInt(newIntValue);
                    }

                    break;
                case ConditionVariableType.Float:
                    EditorGUI.BeginChangeCheck();

                    float newFloatValue = EditorGUI.FloatField(valueFieldRect, floatValue, textFieldStyle);

                    if (EditorGUI.EndChangeCheck() && (!ctx.RequireMouseButton || ctx.CurrentEvent.button == 0))
                    {
                        if (ctx.OnChanged == null) RecordEditorIntent(undoValueLabelPrefix + " Float Value");

                        setFloat(newFloatValue);
                    }

                    break;
                case ConditionVariableType.String:
                    EditorGUI.BeginChangeCheck();

                    string newStringValue = EditorGUI.TextField(valueFieldRect, stringValue ?? "", textFieldStyle);

                    if (EditorGUI.EndChangeCheck() && (!ctx.RequireMouseButton || ctx.CurrentEvent.button == 0))
                    {
                        if (ctx.OnChanged == null) RecordEditorIntent(undoValueLabelPrefix + " String Value");

                        setString(newStringValue);
                    }

                    break;
            }

            EndsAiHighlightED.EndField(ctx.ValueKey, valueFieldRect, valueColor);
            GUI.enabled = true;
        }





        private void DrawVariableTypeDropdown(Rect rect, ConditionVariableType currentType, Action<ConditionVariableType> onTypeChanged, GUIStyle style = null, string highlightKey = null)
        {
            Color fieldColor = EndsAiHighlightED.BeginField(highlightKey);

            if (EditorGUI.DropdownButton(rect, new GUIContent(currentType.ToString()), FocusType.Passive, style ?? EditorStyles.popup))
            {
                GenericMenu menu = new GenericMenu();
                foreach (ConditionVariableType t in Enum.GetValues(typeof(ConditionVariableType)))
                {
                    int count = GetVariableNamesByType(t).Length;
                    if (count > 0)
                    {
                        ConditionVariableType typeCopy = t;
                        menu.AddItem(new GUIContent(t.ToString()), t == currentType, GraphCallback(() => onTypeChanged(typeCopy)));
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent(t.ToString() + NO_VARS_HINT));
                    }
                }
                menu.DropDown(rect);
            }

            EndsAiHighlightED.EndField(highlightKey, rect, fieldColor);
        }

        #endregion Variable Helpers (Graph and Editor)


        #region Dialog Editor (Graph)

        private void DrawDialogEditorContent()
        {
            const int TITLE_LABEL_WIDTH = 80;
            const int DIALOG_NAME_FIELD_WIDTH = 500;
            const int ADD_BUTTON_GAP = 10;
            const int ADD_CHOICE_BUTTON_OFFSET = ADD_BUTTON_GAP + ADD_LINE_BUTTON_WIDTH;
            const int ADD_CONDITION_BUTTON_OFFSET = ADD_CHOICE_BUTTON_OFFSET + ADD_BUTTON_GAP + ADD_LINE_BUTTON_WIDTH;
            const int ADD_REDIRECT_BUTTON_OFFSET = ADD_CONDITION_BUTTON_OFFSET + ADD_BUTTON_GAP + ADD_LINE_BUTTON_WIDTH;
            const int LINES_HEADER_WIDTH = ADD_REDIRECT_BUTTON_OFFSET + ADD_LINE_BUTTON_WIDTH;
            const int EDITOR_HEADER_SPACE = 10;
            const int INITIATOR_LABEL_WIDTH = 75;
            const int INITIATOR_POPUP_WIDTH = 180;
            const int BUTTON_VALIDATE_LINKS_WIDTH = 150;
            const int BUTTON_VALIDATE_START_CONDITIONS_WIDTH = 150;
            const int BUTTON_GO_TO_START_WIDTH = 130;
            const int BUTTON_SAVE_WIDTH = 90;
            const int AUTO_SAVE_TOGGLE_WIDTH = 52;
            const int AUTO_SAVE_INTERVAL_WIDTH = 40;
            const int AUTO_SAVE_SUFFIX_WIDTH = 24;

            Event e = Event.current;
            EventType inputRawType = e.rawType;

            _graphNodeHeights.Clear();
            _graphChoiceHeights.Clear();
            graphPortClickHandled = false;

            if (selectedDialog == null)
            {
                Debug.LogWarning("Selected dialog is missing, returning to the list");
                isEditingDialog = false;
                return;
            }

            if (selectedDialog.nodes == null)
            {
                EditorGUILayout.HelpBox("The dialog has no graph data.", MessageType.Warning);

                if (GUILayout.Button("Add Start node")) AddStartNode(Vector2.zero);

                return;
            }

            if (editorOffset == Vector2.zero)
            {
                var startNode = selectedDialog.nodes.Find(n => n.NodeType == NodeType.Start);
                if (startNode != null) editorOffset = startNode.Position - new Vector2(DEFAULT_POSITION_XY, DEFAULT_POSITION_XY);
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(ASSET_ACTION_ROW_HEIGHT));

            if (GUILayout.Button(_backContent, EditorStyles.toolbarButton, GUILayout.Width(BUTTON_BACK_WIDTH)))
            {
                if (TryExitDialogEditorToList()) GUIUtility.ExitGUI();
            }

            if (GUILayout.Button(_showConditionsContent, EditorStyles.toolbarButton, GUILayout.Width(BUTTON_BACK_WIDTH))) showConditions = !showConditions;

            if (GUILayout.Button(_checkIntegrityContent, EditorStyles.toolbarButton, GUILayout.Width(BUTTON_VALIDATE_LINKS_WIDTH))) ValidateBrokenLinks();

            if (GUILayout.Button(_checkConditionsContent, EditorStyles.toolbarButton, GUILayout.Width(BUTTON_VALIDATE_START_CONDITIONS_WIDTH))) ValidateCurrentCategoryStartConditions();

            if (GUILayout.Button(_goToStartContent, EditorStyles.toolbarButton, GUILayout.Width(BUTTON_GO_TO_START_WIDTH)))
            {
                var startNode = selectedDialog.nodes.Find(n => n.NodeType == NodeType.Start);
                if (startNode != null) editorOffset = startNode.Position - new Vector2(DEFAULT_POSITION_XY, DEFAULT_POSITION_XY);
            }

            bool saveRequested = GUILayout.Button(_saveContent, EditorStyles.toolbarButton, GUILayout.Width(BUTTON_SAVE_WIDTH));

            EditorGUI.BeginChangeCheck();
            bool newAutoSaveEnabled = EditorGUILayout.ToggleLeft(_autoSaveContent, _dialogEditorAutoSaveEnabled, GUILayout.Width(AUTO_SAVE_TOGGLE_WIDTH));
            GUI.enabled = newAutoSaveEnabled;
            int newAutoSaveMinutes = EditorGUILayout.IntPopup(_dialogEditorAutoSaveIntervalMinutes, AUTO_SAVE_MINUTE_LABELS, AUTO_SAVE_MINUTE_VALUES, EditorStyles.toolbarPopup, GUILayout.Width(AUTO_SAVE_INTERVAL_WIDTH));
            GUI.enabled = true;
            GUILayout.Label("min", EditorStyles.miniLabel, GUILayout.Width(AUTO_SAVE_SUFFIX_WIDTH));

            if (EditorGUI.EndChangeCheck())
            {
                if (newAutoSaveEnabled != _dialogEditorAutoSaveEnabled || newAutoSaveMinutes != _dialogEditorAutoSaveIntervalMinutes) _lastDialogEditorAutoSaveTime = -1;
                _dialogEditorAutoSaveEnabled = newAutoSaveEnabled;
                _dialogEditorAutoSaveIntervalMinutes = newAutoSaveMinutes;
                EditorPrefs.SetBool(PREFS_DIALOG_EDITOR_AUTO_SAVE_KEY, _dialogEditorAutoSaveEnabled);
                EditorPrefs.SetInt(PREFS_DIALOG_EDITOR_AUTO_SAVE_MINUTES_KEY, _dialogEditorAutoSaveIntervalMinutes);
            }

            DrawDialogWorkspaceTools();
            EditorGUILayout.EndHorizontal();

            if (saveRequested)
            {
                SaveDialogEditorSession("Toolbar Save button");
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.Space(EDITOR_SPACE_MEDIUM);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EDITOR_SPACE_SMALL);
            GUILayout.Label("Name:", EditorStyles.boldLabel, GUILayout.Width(TITLE_LABEL_WIDTH));
            SyncPendingDialogNameEdit();
            GUI.SetNextControlName(DIALOG_NAME_FIELD_CONTROL);
            _pendingDialogNameEdit = EditorGUILayout.TextField(_pendingDialogNameEdit ?? "", GUILayout.Width(DIALOG_NAME_FIELD_WIDTH)).Replace(".", "");

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape && GUI.GetNameOfFocusedControl() == DIALOG_NAME_FIELD_CONTROL)
            {
                _pendingDialogNameEdit = selectedDialog.dialogName ?? "";
                GUI.FocusControl(null);
                Event.current.Use();
            }

            GUILayout.Space(EDITOR_SPACE_MEDIUM);
            Rect linesHeaderRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(LINES_HEADER_WIDTH));

            if (GUI.Button(new Rect(linesHeaderRect.x, linesHeaderRect.y - LINES_HEADER_BUTTON_OFFSET, ADD_LINE_BUTTON_WIDTH, ADD_LINE_BUTTON_HEIGHT), _addLineContent)) AddLineNode(GetToolbarNewNodePosition());
            if (GUI.Button(new Rect(linesHeaderRect.x + ADD_CHOICE_BUTTON_OFFSET, linesHeaderRect.y - LINES_HEADER_BUTTON_OFFSET, ADD_LINE_BUTTON_WIDTH, ADD_LINE_BUTTON_HEIGHT), _addChoiceNodeContent)) AddChoiceNode(GetToolbarNewNodePosition());
            if (GUI.Button(new Rect(linesHeaderRect.x + ADD_CONDITION_BUTTON_OFFSET, linesHeaderRect.y - LINES_HEADER_BUTTON_OFFSET, ADD_LINE_BUTTON_WIDTH, ADD_LINE_BUTTON_HEIGHT), _addConditionNodeContent)) AddConditionNode(GetToolbarNewNodePosition());
            if (GUI.Button(new Rect(linesHeaderRect.x + ADD_REDIRECT_BUTTON_OFFSET, linesHeaderRect.y - LINES_HEADER_BUTTON_OFFSET, ADD_LINE_BUTTON_WIDTH, ADD_LINE_BUTTON_HEIGHT), _addRedirectContent)) AddRedirectNode(GetToolbarNewNodePosition());

            GUILayout.Space(EDITOR_HEADER_SPACE);
            GUILayout.Label("Initiator:", EditorStyles.boldLabel, GUILayout.Width(INITIATOR_LABEL_WIDTH));
            int initiatorIndex = dialogCategories.IndexOf(selectedDialog.initiator ?? DEFAULT_CATEGORY_NAME);

            if (initiatorIndex < 0) initiatorIndex = 0;

            int newInitiatorIndex = EditorGUILayout.Popup(initiatorIndex, GetUiCatalog().Categories, GUILayout.Width(INITIATOR_POPUP_WIDTH));

            if (newInitiatorIndex != initiatorIndex)
            {
                RecordEditorIntent("Change Dialog Initiator");
                EndsDialogCommandsED.SetCategory(database, selectedDialog, dialogCategories[newInitiatorIndex], false);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(EDITOR_SPACE_MEDIUM);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            if (selectedDialog.nodes == null) selectedDialog.nodes = new List<DialogNode>();

            Rect graphLayoutRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (e.type == EventType.Repaint && editorRect != graphLayoutRect)
            {
                editorRect = graphLayoutRect;
                Repaint();
            }

            Rect conditionsRect = new Rect(editorRect.x, editorRect.y, CONDITION_NODE_WIDTH, editorRect.height);
            float changesWidth = Mathf.Min(CONDITION_NODE_WIDTH, editorRect.width);
            Rect changesRect = new Rect(editorRect.xMax - changesWidth, editorRect.y, changesWidth, editorRect.height);
            bool overChanges = _showChanges && changesRect.Contains(e.mousePosition);

            if (inputRawType == EventType.MouseDown)
            {
                _changesKeyboardFocus = overChanges;
                _changesPointerActive = false;
            }

            EventType changesInputType = e.type;
            bool changesKeyboardInput = _changesKeyboardFocus && (e.isKey || e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand);
            bool changesInput = _showChanges && !_graphGestureActive && !isDragging && !isBoxSelecting && (overChanges || _changesPointerActive || changesKeyboardInput) && e.type != EventType.Layout && e.type != EventType.Repaint && e.type != EventType.Used && e.type != EventType.Ignore;

            if (changesInput && e.type == EventType.MouseDown) _changesPointerActive = true;
            if (changesInput) e.type = EventType.Ignore;

            GUI.BeginGroup(editorRect);
            Rect bgRect = new Rect(0, 0, editorRect.width, editorRect.height);
            float portMargin = NODE_PORT_SIZE * zoomScale;
            Rect nodeViewport = new Rect(-portMargin, -portMargin, bgRect.width + 2 * portMargin, bgRect.height + 2 * portMargin);

            if (gridTexture != null)
            {
                float scaledTileSize = GRID_TILE_SIZE * zoomScale;
                GUI.DrawTextureWithTexCoords(bgRect, gridTexture, new Rect(editorOffset.x * zoomScale / scaledTileSize, -editorOffset.y * zoomScale / scaledTileSize, editorRect.width / scaledTileSize, editorRect.height / scaledTileSize));
            }

            Handles.BeginGUI();
            Vector2 graphLocalMouse = e.mousePosition; // Inside GUI.BeginGroup + Handles.BeginGUI coordinates are already in graph local space.

            if (e.rawType == EventType.MouseDown || e.rawType == EventType.MouseDrag || e.rawType == EventType.MouseUp) _graphLastPointer = graphLocalMouse;

            DrawGraphConnections(bgRect);

            if (selectedNodes.Count == 0 && e.type == EventType.MouseDown && e.button == 0 && bgRect.Contains(graphLocalMouse))
            {
                for (int i = 0; i < selectedDialog.nodes.Count; i++)
                {
                    var node = selectedDialog.nodes[i];
                    
                    if (node == null) continue;

                    Rect inputPort = GetGraphPortRect(GetNodeInputPosition(node));

                    if (node.NodeType != NodeType.Start && e.type == EventType.MouseDown && e.button == 0 && inputPort.Contains(graphLocalMouse))
                    {
                        graphPortClickHandled = true;
                        if (TryDeselectSamePortClick(node, true))
                        {
                            GUI.changed = true;
                            e.Use();
                        }
                        else if (TryCompleteConnectionByClick(node, true, -1)) e.Use();
                        else if (IsNodeConnected(node.nodeId))
                        {
                            RecordEditorIntent("Disconnect Input Port");
                            EndsGraphCommandsED.ClearIncoming(selectedDialog, node.nodeId);

                            selectedPorts.Clear();
                            pendingPort = null;
                            GUI.changed = true;
                        }
                        else
                        {
                            selectedPorts.Clear();
                            selectedPorts.Add(new SelectedPort(node, true));
                            pendingPort = new SelectedPort(node, true);
                            GUI.changed = true;
                        }

                        e.Use();
                    }

                    if (node.NodeType == NodeType.Start && e.type == EventType.MouseDown && e.button == 0 && pendingPort != null && pendingPort.IsInput && pendingPort.Node != node)
                    {
                        Rect startNodeRect = GetGraphNodeRect(node);

                        if (startNodeRect.Contains(graphLocalMouse))
                        {
                            graphPortClickHandled = true;

                            if (TryCompleteConnectionByClick(node, false, -1))
                            {
                                e.Use();

                                continue;
                            }
                        }
                    }

                    HandleGraphOutputPortInput(node, graphLocalMouse, ref graphPortClickHandled);
                }
            }

            if (e.type == EventType.MouseDown && e.button == 0 && pendingPort != null && !graphPortClickHandled && !IsGraphLocalMouseOverAnyNode(graphLocalMouse))
            {
                ResetGraphInputFocus();
                selectedPorts.Clear();
                pendingPort = null;
                copiedActionData = null;
                copiedActionSourceNodeId = null;
                selectedNodes.Clear();
                isDraggingSelection = false;
                selectionDragStartNodePositions = null;
                graphPortClickHandled = true;
                e.Use();
            }

            bool inSelectionZone = selectedNodes.Count > 0 && !isBoxSelecting
                && TryGetNodeSelectionOuterRectLocal(out Rect selectionOuterLocal)
                && selectionOuterLocal.Contains(graphLocalMouse);

            if (inSelectionZone && e.type == EventType.MouseDown && e.button == 0 && !graphPortClickHandled)
            {
                BeginSelectionDrag(e);
                graphPortClickHandled = true;
                e.Use();
            }

            HandleSelectionDragInput(e);

            bool layoutWindows = inputRawType == EventType.Layout;

            if (layoutWindows) _graphLayoutNodes.Clear();

            BeginWindows();

            for (int i = 0; i < selectedDialog.nodes.Count; i++)
            {
                var node = selectedDialog.nodes[i];

                if (node == null) continue;
                if (!layoutWindows && !_graphLayoutNodes.Contains(node)) continue;

                Rect nodeRect = GetGraphNodeRect(node);

                if (layoutWindows)
                {
                    if (pressedHeaderWindow != i && !nodeRect.Overlaps(nodeViewport)) continue;

                    _graphLayoutNodes.Add(node);
                }

                float headerHeight = NODE_TOOLBAR_HEIGHT * zoomScale;
                Rect headerRect = new Rect(nodeRect.x, nodeRect.y, nodeRect.width, headerHeight);
                bool isNodeSelected = selectedNodes.Contains(node);
                bool onNodeHeader = headerRect.Contains(graphLocalMouse);

                if (e.type == EventType.MouseDown && e.button == 0 && onNodeHeader && !graphPortClickHandled)
                {
                    pressedHeaderWindow = i;
                    Repaint();
                }

                Rect windowRectBefore = nodeRect;
                string nodeHighlightKey = NodeControls(node).Key;
                Color nodeChromeColor = GUI.backgroundColor;
                EndsAiHighlightED.Shade nodeShade = NodeShade(i, node);

                GUIStyle nodeStyle = EndsAiHighlightED.PaintNode(nodeHighlightKey, nodeShade);

                nodeRect = GUI.Window(i, nodeRect, (id) =>
                {
                    GUI.backgroundColor = nodeChromeColor;
                    DrawNodeWindow(id, node);
                }, "", nodeStyle);
                GUI.backgroundColor = nodeChromeColor;
                if (isDraggingSelection && isNodeSelected && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
                    GUIUtility.hotControl = selectionDragControlId;
                // Do not rewrite Position every frame: screen <-> world round-trip drifts floats while panning/zooming
                // and falsely marks the dialog dirty. Only apply when GUI.Window actually moved the rect.
                // During multi-select drag HandleSelectionDragInput owns Position - ignore GUI.Window moves.
                if (pressedHeaderWindow == i && !isNodeSelected && (nodeRect.x != windowRectBefore.x || nodeRect.y != windowRectBefore.y))
                {
                    Vector2 newPos = new Vector2(nodeRect.x / zoomScale + editorOffset.x, nodeRect.y / zoomScale + editorOffset.y);

                    RecordEditorIntent("Move graph node");
                    node.Position = newPos;
                }
            }

            EndWindows();

            if (e.rawType == EventType.MouseUp && e.button == 0)
            {
                if (pressedHeaderWindow >= 0) GUI.FocusWindow(-1);

                pressedHeaderWindow = -1;
                Repaint();
            }

            bool paintPorts = e.type == EventType.Repaint;

            for (int i = 0; (paintPorts || e.alt) && i < selectedDialog.nodes.Count; i++)
            {
                var node = selectedDialog.nodes[i];

                if (node == null) continue;

                Rect highlightRect = GetGraphNodeRect(node);

                if (!highlightRect.Overlaps(nodeViewport)) continue;
                if (e.alt) EndsAiHighlightED.ReleaseIfAltHovered(NodeControls(node).Key, highlightRect);
                if (!paintPorts) continue;

                bool isInputConnected = IsNodeConnected(node.nodeId);
                Rect inputPort = GetGraphPortRect(GetNodeInputPosition(node));

                if (node.NodeType != NodeType.Start)
                {
                    bool isInputSelected = isInputConnected || selectedPorts.Any(p => p.Node == node && p.IsInput) || (pendingPort != null && pendingPort.Node == node && pendingPort.IsInput);
                    Texture2D inputTexture;
                    if (isInputSelected) inputTexture = nodeActiveInputTexture;
                    else inputTexture = nodeInactiveInputTexture;

                    if (inputTexture != null)
                    {
                        GUI.DrawTexture(inputPort, inputTexture, ScaleMode.StretchToFill);
                    }
                }

                DrawGraphOutputPorts(node);
            }

            Handles.EndGUI();
            GUI.EndGroup();

            if (showConditions)
            {
                if (selectedDialog.conditions == null) selectedDialog.conditions = new List<DialogCondition>();

                const int CONDITIONS_ADD_BUTTON_HEIGHT = 30;
                const int CONDITIONS_ADD_BUTTON_GAP = 5;
                const int CONDITIONS_LOGIC_TOGGLE_SIZE = 30;
                const int CONDITIONS_LOCK_BUTTON_WIDTH = 40;
                const int CONDITIONS_HELPBOX_HEIGHT = 48;
                const int CONDITIONS_VALIDATION_HELPBOX_HEIGHT = 72;
                const int CONDITIONS_SECTION_GAP = 5;
                const int CONDITIONS_SCROLL_BOTTOM = 135;
                const int CONDITIONS_HEADER_OFFSET = 30;
                const int CONDITIONS_CLOSE_BUTTON_SIZE = 30;
                const int CONDITIONS_CLOSE_OFFSET = 40;
                const int SCROLLBAR_WIDTH = 15;

                GUI.BeginGroup(conditionsRect);
                GUIStyle conditionsBackgroundStyle = GetWindowStyles().ConditionsBackground;

                GUI.backgroundColor = new Color(LINE_COLOR_2.r, LINE_COLOR_2.g, LINE_COLOR_2.b);
                GUI.Box(new Rect(0, 0, CONDITION_NODE_WIDTH, editorRect.height), "", conditionsBackgroundStyle);
                GUI.backgroundColor = Color.white;
                string conditionsPanelTitle;

                if (selectedDialog.isLocked) conditionsPanelTitle = "Locked (redirect-only)";
                else conditionsPanelTitle = $"Playback conditions ({selectedDialog.conditions.Count})";

                GUI.Label(new Rect(CONDITIONS_WINDOW_BORDER, CONDITIONS_WINDOW_BORDER, CONDITION_NODE_WIDTH - 2 * CONDITIONS_WINDOW_BORDER, CATEGORY_HEADER_HEIGHT), conditionsPanelTitle, EditorStyles.boldLabel);

                if (GUI.Button(new Rect(CONDITION_NODE_WIDTH - CONDITIONS_CLOSE_OFFSET, CONDITIONS_WINDOW_BORDER, CONDITIONS_CLOSE_BUTTON_SIZE, FIELD_ROW_HEIGHT), _closeConditionsContent, EditorStyles.miniButton)) showConditions = false;

                float conditionsPanelY = CONDITIONS_HEADER_OFFSET + CONDITIONS_WINDOW_BORDER;
                float conditionsContentWidth = CONDITION_NODE_WIDTH - 2 * CONDITIONS_WINDOW_BORDER;
                EditorGUI.HelpBox(new Rect(CONDITIONS_WINDOW_BORDER, conditionsPanelY, conditionsContentWidth, CONDITIONS_HELPBOX_HEIGHT), GetDialogStartConditionsHelpMessage(selectedDialog.isAndLogic, selectedDialog.isLocked), MessageType.Info);
                conditionsPanelY += CONDITIONS_HELPBOX_HEIGHT + CONDITIONS_SECTION_GAP;
                string selectedCategory = selectedDialog.initiator;

                if (string.IsNullOrEmpty(selectedCategory)) selectedCategory = DEFAULT_CATEGORY_NAME;

                List<EndsDialogSelectionDiagnosticsED.ValidationIssue> categoryConditionIssues = GetCategoryStartConditionIssues(selectedCategory);

                if (categoryConditionIssues.Count > 0)
                {
                    string validationSummary = EndsDialogSelectionDiagnosticsED.FormatSummary(categoryConditionIssues, 3);
                    EditorGUI.HelpBox(new Rect(CONDITIONS_WINDOW_BORDER, conditionsPanelY, conditionsContentWidth, CONDITIONS_VALIDATION_HELPBOX_HEIGHT), validationSummary, MessageType.Warning);
                    conditionsPanelY += CONDITIONS_VALIDATION_HELPBOX_HEIGHT + CONDITIONS_SECTION_GAP;
                }

                string lockButtonLabel;

                if (selectedDialog.isLocked) lockButtonLabel = "🔒";
                else lockButtonLabel = "🔓";

                string lockButtonTooltip;

                if (selectedDialog.isLocked) lockButtonTooltip = "Click to unlock. Then the dialog can work through the initiator and you can edit conditions.";
                else lockButtonTooltip = "Click to lock. Then the dialog can start only from Redirect node.";

                if (GUI.Button(new Rect(CONDITIONS_WINDOW_BORDER, conditionsPanelY, CONDITIONS_LOCK_BUTTON_WIDTH, CONDITIONS_ADD_BUTTON_HEIGHT), new GUIContent(lockButtonLabel, lockButtonTooltip)))
                {
                    RecordEditorIntent("Toggle Dialog Lock");
                    EndsDialogCommandsED.SetLocked(selectedDialog, !selectedDialog.isLocked);
                }

                EditorGUI.BeginDisabledGroup(selectedDialog.isLocked);

                float addButtonX = CONDITIONS_WINDOW_BORDER + CONDITIONS_LOCK_BUTTON_WIDTH + CONDITIONS_ADD_BUTTON_GAP;
                float addButtonWidth = conditionsContentWidth - CONDITIONS_LOCK_BUTTON_WIDTH - CONDITIONS_ADD_BUTTON_GAP;

                if (selectedDialog.conditions.Count >= 2) addButtonWidth -= CONDITIONS_LOGIC_TOGGLE_SIZE + CONDITIONS_ADD_BUTTON_GAP;

                if (GUI.Button(new Rect(addButtonX, conditionsPanelY, addButtonWidth, CONDITIONS_ADD_BUTTON_HEIGHT), _addConditionContent))
                {
                    RecordEditorIntent("Add Dialog Condition");
                    EndsDialogCommandsED.AddStartCondition(selectedDialog, EndsDialogRulesED.CreateDialogCondition(database));
                }

                if (selectedDialog.conditions.Count >= 2)
                {
                    Rect logicToggleRect = new Rect(addButtonX + addButtonWidth + CONDITIONS_ADD_BUTTON_GAP, conditionsPanelY, CONDITIONS_LOGIC_TOGGLE_SIZE, CONDITIONS_ADD_BUTTON_HEIGHT);
                    GUIContent logicButtonLabel;

                    if (selectedDialog.isAndLogic) logicButtonLabel = _andLogicContent;
                    else logicButtonLabel = _orLogicContent;

                    string dialogLogicKey = DialogControls.Field(DialogSchema.AllConditionsRequired);
                    Color dialogLogicColor = EndsAiHighlightED.BeginField(dialogLogicKey);

                    if (GUI.Button(logicToggleRect, logicButtonLabel))
                    {
                        RecordEditorIntent("Toggle Dialog Start Condition Logic");
                        EndsDialogCommandsED.ToggleStartLogic(selectedDialog);
                    }

                    EndsAiHighlightED.EndField(dialogLogicKey, logicToggleRect, dialogLogicColor);
                }

                conditionsPanelY += CONDITIONS_ADD_BUTTON_HEIGHT + CONDITIONS_SECTION_GAP;

                string conditionLogicSeparator = GetDialogStartConditionLogicSeparator(selectedDialog.isAndLogic);

                Rect scrollViewRect = new Rect(CONDITIONS_WINDOW_BORDER, conditionsPanelY, conditionsContentWidth, conditionsRect.height - conditionsPanelY - CONDITIONS_SCROLL_BOTTOM);
                float totalConditionsHeight = 0;

                for (int i = 0; i < selectedDialog.conditions.Count; i++)
                {
                    totalConditionsHeight += CONDITION_NODE_CONDITION_HEIGHT + CONDITION_ITEM_SPACING;
                }

                bool conditionsNeedScrollbar = totalConditionsHeight > scrollViewRect.height;
                float scrollContentWidth = conditionsContentWidth;
                if (conditionsNeedScrollbar) scrollContentWidth -= SCROLLBAR_WIDTH;

                Rect scrollContentRect = new Rect(0, 0, scrollContentWidth, Mathf.Max(scrollViewRect.height, totalConditionsHeight));
                conditionsScrollPos = GUI.BeginScrollView(scrollViewRect, conditionsScrollPos, scrollContentRect);
                float currentY = 0;
                List<int> conditionsToRemove = new List<int>();

                for (int i = 0; i < selectedDialog.conditions.Count; i++)
                {
                    float conditionHeight = CONDITION_NODE_CONDITION_HEIGHT;
                    Rect conditionRect = new Rect(0, currentY, scrollContentRect.width, conditionHeight);

                    EditorGUI.DrawRect(conditionRect, lineColors[i % lineColors.Length]);

                    GUIStyle conditionBoxStyle = GetWindowStyles().ConditionBox;

                    GUI.backgroundColor = CONDITION_BOX_BG;
                    var startConditionControls = DialogControls.Item(DialogSchema.Conditions, selectedDialog.conditions[i], i);
                    string startConditionKey = startConditionControls.Key;

                    if (EndsAiHighlightED.IsMarked(startConditionKey)) GUI.backgroundColor = EndsAiHighlightED.Tint(startConditionKey);

                    Rect innerConditionBoxRect = new Rect(0, currentY + CONDITIONS_BOX_PADDING, scrollContentRect.width, conditionHeight - 2 * CONDITIONS_BOX_PADDING);
                    GUI.Box(innerConditionBoxRect, GUIContent.none, conditionBoxStyle);
                    GUI.backgroundColor = Color.white;
                    EndsAiHighlightED.ReleaseIfAltHovered(startConditionKey, innerConditionBoxRect);

                    var startCondition = selectedDialog.conditions[i];
                    var shellParams = new ConditionRowShellParams
                    {
                        Layout = ConditionRowShellLayout.DialogStartPanel,
                        Index = i,
                        ReferenceState = startCondition.ReferenceState,
                        VariableName = startCondition.VariableName,
                        MissingReferenceLabel = startCondition.MissingReferenceLabel,
                        ContentWidth = scrollContentRect.width,
                        RowBaseY = currentY,
                        ZoomScale = 1f,
                        WarningBoundsRect = innerConditionBoxRect,
                        UseRedDeleteButton = true
                    };

                    if (DrawConditionRowShell(shellParams, conditionsToRemove, out float fieldY))
                    {
                        currentY += conditionHeight;

                        if (i < selectedDialog.conditions.Count - 1)
                        {
                            GUI.Label(new Rect(0, currentY, scrollContentRect.width, FIELD_ROW_HEIGHT), conditionLogicSeparator, EditorStyles.boldLabel);
                            currentY += CONDITION_AND_SPACING;
                        }

                        continue;
                    }

                    DrawDialogConditionEditableFields(
                        startCondition,
                        i,
                        selectedDialog.conditions,
                        ref fieldY,
                        new ConditionFieldsDrawContext
                        {
                            ZoomScale = 1f,
                            ContentWidth = scrollContentRect.width
                        }, startConditionControls);

                    currentY += conditionHeight;

                    if (i < selectedDialog.conditions.Count - 1)
                    {
                        GUI.Label(new Rect(0, currentY, scrollContentRect.width, FIELD_ROW_HEIGHT), conditionLogicSeparator, EditorStyles.boldLabel);
                        currentY += CONDITION_AND_SPACING;
                    }
                }

                if (conditionsToRemove.Count > 0)
                {
                    RecordEditorIntent("Remove Dialog Condition");

                    foreach (int index in conditionsToRemove.OrderByDescending(i => i)) EndsDialogCommandsED.RemoveStartCondition(selectedDialog, index);
                }

                GUI.EndScrollView();

                EditorGUI.EndDisabledGroup();

                GUI.EndGroup();
            }

            bool mouseOverDialogStartConditionsPanel = showConditions && conditionsRect.Contains(e.mousePosition);

            if (e.type == EventType.MouseDown && e.button == 0 && editorRect.Contains(e.mousePosition) && !mouseOverDialogStartConditionsPanel && !IsMouseOverAnyGraphNode(e) && !IsMouseOverSelectionOuterRect(e))
            {
                bool dismissPendingState = pendingPort != null || copiedActionData != null;
                ResetGraphInputFocus();
                selectedPorts.Clear();
                pendingPort = null;
                copiedActionData = null;
                copiedActionSourceNodeId = null;
                selectedNodes.Clear();
                isDraggingSelection = false;
                selectionDragStartNodePositions = null;
                isBoxSelecting = false;
                if (!dismissPendingState)
                {
                    isBoxSelecting = true;
                    boxSelectStart = e.mousePosition;
                    boxSelectCurrent = e.mousePosition;
                }
                e.Use();
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && isBoxSelecting)
            {
                boxSelectCurrent = e.mousePosition;
                Rect boxRect = Rect.MinMaxRect(
                    Mathf.Min(boxSelectStart.x, boxSelectCurrent.x),
                    Mathf.Min(boxSelectStart.y, boxSelectCurrent.y),
                    Mathf.Max(boxSelectStart.x, boxSelectCurrent.x),
                    Mathf.Max(boxSelectStart.y, boxSelectCurrent.y));
                selectedNodes.Clear();
                foreach (var n in selectedDialog.nodes)
                {
                    if (n == null) continue;
                    float h = Mathf.Max(GetNodeHeight(n), MIN_NODE_HEIGHT) * zoomScale;
                    float w = GetScaledNodeWidth(n);
                    Rect nodeRect = new Rect(editorRect.x + (n.Position.x - editorOffset.x) * zoomScale, editorRect.y + (n.Position.y - editorOffset.y) * zoomScale, w, h);
                    if (boxRect.Overlaps(nodeRect)) selectedNodes.Add(n);
                }
                Repaint();
                e.Use();
            }

            if (e.type == EventType.MouseUp && e.button == 0 && isBoxSelecting)
            {
                if ((boxSelectStart - boxSelectCurrent).sqrMagnitude < BOX_SELECT_MIN_DRAG * BOX_SELECT_MIN_DRAG)
                    selectedNodes.Clear();
                isBoxSelecting = false;
                ResetGraphInputFocus();
                e.Use();
            }

            if (selectedNodes.Count > 0 && !isBoxSelecting && e.type == EventType.Repaint && TryGetNodeSelectionOuterRectScreen(out Rect selectionOuterRect))
            {
                EditorGUIUtility.AddCursorRect(selectionOuterRect, MouseCursor.MoveArrow);
                EditorGUI.DrawRect(selectionOuterRect, new Color(0.3f, 0.5f, 0.9f, 0.08f));
                DrawBox(selectionOuterRect, new Color(0.3f, 0.5f, 0.9f, 0.6f), 2);
            }

            if (isBoxSelecting && e.type == EventType.Repaint)
            {
                Rect boxRect = Rect.MinMaxRect(
                    Mathf.Min(boxSelectStart.x, boxSelectCurrent.x),
                    Mathf.Min(boxSelectStart.y, boxSelectCurrent.y),
                    Mathf.Max(boxSelectStart.x, boxSelectCurrent.x),
                    Mathf.Max(boxSelectStart.y, boxSelectCurrent.y));

                EditorGUI.DrawRect(boxRect, new Color(0.3f, 0.5f, 0.9f, 0.2f));
                DrawBox(boxRect, new Color(0.3f, 0.5f, 0.9f, 0.8f), 2);
            }

            const int SCALE_LABEL_WIDTH = 100;
            const int SCALE_LABEL_HEIGHT = 20;
            const int SCALE_LABEL_RIGHT_OFFSET = 80;
            const int HINT_OFFSET = 44;
            const int HINT_PADDING = 8;
            const int HINT_MIN_WIDTH_OFFSET = 100;


            GUIStyle scaleLabelStyle = GetWindowStyles().ScaleLabel;

            Rect scaleLabelRect = new Rect(editorRect.x + editorRect.width - SCALE_LABEL_RIGHT_OFFSET, editorRect.y + editorRect.height - SCALE_LABEL_HEIGHT, SCALE_LABEL_WIDTH, SCALE_LABEL_HEIGHT);
            DrawGraphPerformance(scaleLabelRect, scaleLabelStyle);

            if (GUI.Button(scaleLabelRect, new GUIContent($"Scale: {(zoomScale * 100):F0}%", "Click - reset to 100%"), scaleLabelStyle) && Event.current.button == 0)
            {
                zoomScale = DEFAULT_ZOOM_SCALE;
            }

            if (pendingPort != null)
            {
                GUIStyle hintStyle = GetWindowStyles().PortHint;

                string portDesc = GetPortDescription(pendingPort);
                string hintMessage;

                if (pendingPort.Node.NodeType == NodeType.Start) hintMessage = $"{portDesc} is selected and waiting for connection. LMB on the same port or empty space to clear selection, RMB to open port menu.";
                else
                {
                    string nodeIdShort = EndsNodeRefED.ShortRef(pendingPort.Node.nodeId);
                    hintMessage = $"{portDesc} {nodeIdShort} is selected and waiting for connection. LMB on the same port or empty space to clear selection, RMB to open port menu.";
                }

                GUIContent hintContent = new GUIContent(hintMessage);
                Vector2 hintSize = hintStyle.CalcSize(hintContent);
                Rect hintRect = new Rect(editorRect.x + HINT_PADDING, editorRect.y + editorRect.height - hintSize.y - HINT_OFFSET, Mathf.Min(hintSize.x + HINT_EXTRA_WIDTH, editorRect.width - HINT_MIN_WIDTH_OFFSET), hintSize.y + HINT_PADDING);
                GUI.Box(hintRect, hintContent, hintStyle);
            }

            if (copiedActionData != null)
            {
                GUIStyle copyHintStyle = GetWindowStyles().CopyHint;

                string charInfo;

                if (string.IsNullOrEmpty(copiedActionData.CharacterName)) charInfo = "-";
                else charInfo = copiedActionData.CharacterName;

                string emoInfo;

                if (string.IsNullOrEmpty(copiedActionData.EmotionName)) emoInfo = "";
                else emoInfo = $", {copiedActionData.EmotionName}";
                
                GUIContent copyHintContent = new GUIContent($"📋 Action copied ({charInfo}{emoInfo}). Click in empty space to reset.");
                Vector2 copyHintSize = copyHintStyle.CalcSize(copyHintContent);
                float copyHintOffset;

                if (pendingPort != null) copyHintOffset = copyHintSize.y + FIELD_ROW_HEIGHT;
                else copyHintOffset = 0;

                float copyHintY = editorRect.y + editorRect.height - copyHintSize.y - HINT_OFFSET - copyHintOffset;
                Rect copyHintRect = new Rect(editorRect.x + HINT_PADDING, copyHintY, Mathf.Min(copyHintSize.x + 16, editorRect.width - HINT_MIN_WIDTH_OFFSET), copyHintSize.y + HINT_PADDING);
                GUI.Box(copyHintRect, copyHintContent, copyHintStyle);
            }

            EditorGUILayout.EndVertical();

            if (e.type == EventType.MouseDown && e.button == 1 && editorRect.Contains(e.mousePosition))
            {
                if (pendingPort != null) ShowPortContextMenu(pendingPort.Node, pendingPort.IsInput, pendingPort.ChoiceIndex);
                
                else
                {
                    Vector2 worldPos = (e.mousePosition - editorRect.position) / zoomScale + editorOffset;
                    ShowNodeContextMenu(worldPos);
                }

                e.Use();
            }
            if (e.type == EventType.MouseDown && e.button == 2 && editorRect.Contains(e.mousePosition))
            {
                isDragging = true;
                dragStartPos = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && isDragging && e.button == 2)
            {
                Vector2 delta = (e.mousePosition - dragStartPos) / zoomScale;
                editorOffset -= delta;
                dragStartPos = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 2)
            {
                isDragging = false;
                e.Use();
            }

            if (e.type == EventType.ScrollWheel && editorRect.Contains(e.mousePosition) && !mouseOverDialogStartConditionsPanel)
            {
                Vector2 mousePos = e.mousePosition;
                float oldZoom = zoomScale;
                float zoomDelta = e.delta.y * -ZOOM_SENSITIVITY;
                zoomScale = Mathf.Clamp(zoomScale + zoomDelta, ZOOM_MIN, ZOOM_MAX);

                if (Mathf.Abs(zoomScale - oldZoom) > ZOOM_EPSILON)
                {
                    Vector2 mouseRelativeToEditor = mousePos - new Vector2(editorRect.x, editorRect.y);
                    Vector2 mouseWorldBeforeZoom = editorOffset + (mouseRelativeToEditor / oldZoom);
                    Vector2 mouseWorldAfterZoom = editorOffset + (mouseRelativeToEditor / zoomScale);
                    editorOffset += (mouseWorldBeforeZoom - mouseWorldAfterZoom);
                }

                Repaint();
            }

            if (changesInput) e.type = changesInputType;

            DrawChangesPanel(changesRect);

            if (changesInput && e.type != EventType.Used) e.Use();
            if (inputRawType == EventType.MouseUp) _changesPointerActive = false;

            CommitPendingDialogNameEditIfUnfocused();
        }

        private Vector2 GetNodeInputPosition(DialogNode node)
        {
            if (node.NodeType == NodeType.Start) return Vector2.zero;

            return (node.Position - editorOffset + GetNodeInputOffset(node.NodeType)) * zoomScale;
        }

        private static Vector2 GetNodeInputOffset(NodeType type)
        {
            switch (type)
            {
                case NodeType.Line: return new Vector2(INPUT_OFFSET_LINE, 0);
                case NodeType.Choice: return new Vector2(0, CHOICE_NODE_ENTER_SPACING);
                case NodeType.Condition: return new Vector2(0, CONDITION_NODE_ENTER_SPACING);
                case NodeType.Redirect: return new Vector2(0, REDIRECT_NODE_ENTER_SPACING);
                default: return Vector2.zero;
            }
        }

        #region Port Context Menu

        private string GetPortDescription(SelectedPort port)
        {
            if (port == null || port.Node == null) return "port";
            var node = port.Node;
            if (node.NodeType == NodeType.Start) return "Start node";

            string nodeTypeName = node.NodeType switch
            {
                NodeType.Line => "line",
                NodeType.Choice => "choice",
                NodeType.Condition => "condition",
                NodeType.Redirect => "redirect",
                _ => "node"
            };
            if (port.IsInput) return $"Input port of {nodeTypeName} node";
            if (node.NodeType == NodeType.Choice && port.ChoiceIndex >= 0 && node.ChoiceData != null && port.ChoiceIndex < node.ChoiceData.Count)
            {
                var choice = node.ChoiceData[port.ChoiceIndex];
                string choiceLabel = $"option {port.ChoiceIndex + 1}";
                if (choice?.Translations != null)
                {
                    var t = choice.Translations.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x?.Text));
                    if (t != null)
                    {
                        if (t.Text.Length > 20) choiceLabel = $"\"{t.Text.Substring(0, 20)}…\"";
                        else choiceLabel = $"\"{t.Text}\"";
                    }
                }
                return $"Output port {choiceLabel} of the choice node";
            }
            if (node.NodeType == NodeType.Condition)
            {
                string portLabel;
                if (port.ChoiceIndex == 0) portLabel = "TRUE";
                else portLabel = "FALSE";
                return $"Output port {portLabel} of the condition node";
            }
            return $"Output port of the {nodeTypeName} node";
        }

        private void ShowPortContextMenu(DialogNode node, bool isInput, int choiceIndex)
        {
            if (node == null || selectedDialog == null || selectedDialog.nodes == null)
            {
                pendingPort = null;
                return;
            }

            string nodeId = node.nodeId;
            DialogNode targetNode = selectedDialog.nodes.Find(n => n != null && n.nodeId == nodeId);

            if (targetNode == null)
            {
                pendingPort = null;
                return;
            }

            GenericMenu menu = new GenericMenu();
            actionWasPerformed = false; // Reset flag before showing menu
            isContextMenuOpen = true;   // Set flag before showing menu

            if (isInput)
            {
                HandleInputPortMenu(targetNode, nodeId, menu);
            }
            else
            {
                HandleOutputPortMenu(targetNode, nodeId, choiceIndex, menu);
            }

            menu.ShowAsContext();
            isContextMenuOpen = false; // Reset flag after closing menu
        }

        private void HandleInputPortMenu(DialogNode targetNode, string nodeId, GenericMenu menu)
        {
            bool isConnected = IsNodeConnected(nodeId);

            if (isConnected)
            {
                menu.AddItem(new GUIContent("Disconnect"), false, GraphCallback(() =>
                {
                    RecordEditorIntent("Disconnect Input Port");

                    EndsGraphCommandsED.ClearIncoming(selectedDialog, nodeId);

                    selectedPorts.Clear();
                    pendingPort = null;
                    actionWasPerformed = true; // Set flag
                }));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Node is not connected"));
            }
        }

        private void HandleOutputPortMenu(DialogNode targetNode, string nodeId, int choiceIndex, GenericMenu menu)
        {
            string currentConnection = GetCurrentConnection(targetNode, choiceIndex);

            if (!string.IsNullOrEmpty(currentConnection))
            {
                menu.AddItem(new GUIContent("Disconnect"), false, GraphCallback(() =>
                {
                    RecordEditorIntent("Disconnect Output Port");
                    DisconnectPort(targetNode, choiceIndex);
                    selectedPorts.Clear();
                    pendingPort = null;
                    actionWasPerformed = true; // Set flag
                }));
            }

            foreach (var target in selectedDialog.nodes)
            {
                if (target == null || EndsDialogGraphContractED.GetConnectionError(targetNode, target) != null) continue;

                if (IsAlreadyConnected(targetNode, target.nodeId, choiceIndex)) continue;

                string targetName = $"Input {EndsNodeRefED.ShortRef(target.nodeId)}";
                string targetId = target.nodeId;

                menu.AddItem(new GUIContent(targetName), false, GraphCallback(() =>
                {
                    RecordEditorIntent("Connect Port");
                    ConnectPort(targetNode, targetId, choiceIndex);
                    selectedPorts.Clear();
                    pendingPort = null;
                    actionWasPerformed = true; // Set flag
                }));
            }
        }

        private void DisconnectPort(DialogNode node, int choiceIndex = -1)
        {
            _graphPortsDirty = true;
            EndsGraphCommandsED.DisconnectPort(node, choiceIndex);
        }

        private void ConnectPort(DialogNode sourceNode, string targetId, int choiceIndex = -1)
        {
            _graphPortsDirty = true;
            EndsGraphCommandsED.ConnectPort(sourceNode, targetId, choiceIndex);
        }

        /// <summary>
        /// If clicking an already selected port, deselect it. Returns true if deselected.
        /// </summary>
        private Rect GetGraphLocalNodeRect(DialogNode node)
        {
            float w = GetScaledNodeWidth(node);
            float h = Mathf.Max(GetNodeHeight(node), MIN_NODE_HEIGHT) * zoomScale;
            return new Rect(
                (node.Position.x - editorOffset.x) * zoomScale,
                (node.Position.y - editorOffset.y) * zoomScale,
                w, h);
        }

        private bool IsGraphLocalMouseOverAnyNode(Vector2 graphLocalMouse)
        {
            if (selectedDialog?.nodes == null) return false;

            foreach (var n in selectedDialog.nodes)
            {
                if (n == null) continue;
                if (GetGraphLocalNodeRect(n).Contains(graphLocalMouse)) return true;
            }

            return false;
        }

        private bool IsMouseOverAnyGraphNode(Event evt)
        {
            if (selectedDialog?.nodes == null || !editorRect.Contains(evt.mousePosition)) return false;
            Vector2 graphLocalMouse = new Vector2(evt.mousePosition.x - editorRect.x, evt.mousePosition.y - editorRect.y);
            return IsGraphLocalMouseOverAnyNode(graphLocalMouse);
        }

        private bool TryGetNodeSelectionOuterRectScreen(out Rect outerRect)
        {
            outerRect = default;
            if (selectedNodes.Count == 0) return false;

            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (var n in selectedNodes)
            {
                if (n == null) continue;
                float w = GetScaledNodeWidth(n);
                float h = Mathf.Max(GetNodeHeight(n), MIN_NODE_HEIGHT) * zoomScale;
                float nx = editorRect.x + (n.Position.x - editorOffset.x) * zoomScale;
                float ny = editorRect.y + (n.Position.y - editorOffset.y) * zoomScale;
                xMin = Mathf.Min(xMin, nx);
                yMin = Mathf.Min(yMin, ny);
                xMax = Mathf.Max(xMax, nx + w);
                yMax = Mathf.Max(yMax, ny + h);
            }

            if (xMin >= float.MaxValue) return false;

            outerRect = new Rect(
                xMin - SELECTION_FRAME_PADDING,
                yMin - SELECTION_FRAME_PADDING,
                xMax - xMin + SELECTION_FRAME_PADDING * 2,
                yMax - yMin + SELECTION_FRAME_PADDING * 2);
            return true;
        }

        private bool TryGetNodeSelectionOuterRectLocal(out Rect outerRect)
        {
            outerRect = default;
            if (!TryGetNodeSelectionOuterRectScreen(out Rect screenRect)) return false;
            outerRect = new Rect(screenRect.x - editorRect.x, screenRect.y - editorRect.y, screenRect.width, screenRect.height);
            return true;
        }

        private bool IsMouseOverSelectionOuterRect(Event evt)
        {
            return selectedNodes.Count > 0 && !isBoxSelecting
                && TryGetNodeSelectionOuterRectScreen(out Rect r) && r.Contains(evt.mousePosition);
        }

        private void ResetGraphInputFocus()
        {
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);
            GUI.FocusWindow(-1);
        }

        private EndsAiHighlightED.Shade NodeShade(int windowId, DialogNode node)
        {
            if (node != null && selectedNodes.Contains(node)) return EndsAiHighlightED.Shade.Clear;

            if (pressedHeaderWindow == windowId) return EndsAiHighlightED.Shade.Bright;

            return EndsAiHighlightED.Shade.Rest;
        }

        private void BeginSelectionDrag(Event evt)
        {
            selectionDragControlId = GUIUtility.GetControlID(FocusType.Passive);
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);
            GUI.FocusWindow(-1);
            GUIUtility.hotControl = selectionDragControlId;
            isDraggingSelection = true;
            selectionDragStartPos = evt.mousePosition;
            selectionDragStartNodePositions = new Dictionary<DialogNode, Vector2>();

            foreach (var n in selectedNodes)
            {
                if (n != null) selectionDragStartNodePositions[n] = n.Position;
            }

            RecordEditorIntent("Move Nodes");
        }

        private static string GetDialogStartConditionsHelpMessage(bool isAndLogic, bool isLocked)
        {
            if (isLocked)
            {
                return "This dialog is locked - redirect only. Start conditions are disabled and will not run from the initiator.";
            }

            string logicPart;

            if (isAndLogic) logicPart = "All conditions must be met.";
            else logicPart = "One met condition is enough.";

            return "Add playback conditions for the dialog. " + logicPart + " The category checks the order of dialogs in the Dialog Database - the first suitable one is triggered.";
        }

        private static string GetDialogStartConditionLogicSeparator(bool isAndLogic)
        {
            if (isAndLogic) return "AND";

            return "OR";
        }

        private void HandleSelectionDragInput(Event evt)
        {
            if (!isDraggingSelection || selectionDragStartNodePositions == null) return;
            if (evt.type == EventType.MouseDrag && evt.button == 0)
            {
                GUIUtility.hotControl = selectionDragControlId;
                Vector2 deltaGraph = (evt.mousePosition - selectionDragStartPos) / zoomScale;
                foreach (var kvp in selectionDragStartNodePositions)
                {
                    if (kvp.Key != null) kvp.Key.Position = kvp.Value + deltaGraph;
                }

                evt.Use();
                Repaint();
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                isDraggingSelection = false;
                selectionDragStartNodePositions = null;
                ResetGraphInputFocus();
                evt.Use();
            }
        }

        private bool TryDeselectSamePortClick(DialogNode node, bool isInput, int choiceIndex = -1)
        {
            if (pendingPort == null || pendingPort.Node != node || pendingPort.IsInput != isInput || pendingPort.ChoiceIndex != choiceIndex) return false;

            selectedPorts.Clear();
            pendingPort = null;
            return true;
        }

        /// <summary>
        /// If connection is expected (pendingPort) and clicking opposite port of another node, connect directly.
        /// Returns true if connected.
        /// </summary>
        private bool TryCompleteConnectionByClick(DialogNode clickedNode, bool clickedIsInput, int clickedChoiceIndex)
        {
            if (pendingPort == null || selectedDialog == null || clickedNode == null) return false;
            if (pendingPort.IsInput == clickedIsInput) return false;

            DialogNode source = pendingPort.Node;
            DialogNode target = clickedNode;

            if (pendingPort.IsInput)
            {
                source = clickedNode;
                target = pendingPort.Node;
            }

            if (EndsDialogGraphContractED.GetConnectionError(source, target) != null) return false;

            if (pendingPort.IsInput)
            {
                if (!clickedIsInput)
                {
                    RecordEditorIntent("Connect Port");
                    if (clickedNode.NodeType == NodeType.Start) ConnectPort(clickedNode, pendingPort.Node.nodeId, -1);
                    else ConnectPort(clickedNode, pendingPort.Node.nodeId, clickedChoiceIndex);
                    selectedPorts.Clear();
                    pendingPort = null;
                    actionWasPerformed = true;
                    return true;
                }
            }
            else
            {
                // Source is output (pendingPort), target is input of clicked node
                if (clickedIsInput && clickedNode.NodeType != NodeType.Start)
                {
                    RecordEditorIntent("Connect Port");
                    ConnectPort(pendingPort.Node, clickedNode.nodeId, pendingPort.ChoiceIndex);
                    selectedPorts.Clear();
                    pendingPort = null;
                    actionWasPerformed = true;

                    return true;
                }
            }

            return false;
        }

        #endregion Port Context Menu



        #region Node Height Calculation

        private float GetScaledNodeWidth(DialogNode node)
        {
            if (node.NodeType == NodeType.Start) return START_NODE_WIDTH * zoomScale;

            return BASE_NODE_WIDTH * zoomScale;
        }





        #endregion Node Height Calculation

        #region Drawing Node Windows


        private void DrawNodeWindow(int id, DialogNode node)
        {
            const int MISSING_DATA_PADDING = 10;
            const int MISSING_DATA_LABEL_Y = 2;
            const int MISSING_DATA_LABEL_HEIGHT = 20;
            const int MISSING_DATA_BUTTON_Y = 28;
            const int MISSING_DATA_BUTTON_HEIGHT = 24;

            if ((node.NodeType == NodeType.Line && (node.LineData == null || node.LineData.DialogsText == null)) || (node.NodeType == NodeType.Redirect && node.RedirectData == null))
            {
                EditorGUI.LabelField(new Rect(MISSING_DATA_PADDING, MISSING_DATA_LABEL_Y, BASE_NODE_WIDTH - MISSING_DATA_PADDING * 2, MISSING_DATA_LABEL_HEIGHT), node.NodeType + " data is missing");

                if (GUI.Button(new Rect(MISSING_DATA_PADDING, MISSING_DATA_BUTTON_Y, BASE_NODE_WIDTH - MISSING_DATA_PADDING * 2, MISSING_DATA_BUTTON_HEIGHT), "Initialize missing data"))
                {
                    EndsMcpJsonED command = EndsDialogGraphContractED.NodeCommand("node.patch", selectedDialogName, node.nodeId);
                    EndsMcpJsonED patch = EndsMcpJsonED.Obj();

                    if (node.NodeType == NodeType.Line)
                    {
                        LineNode line = EndsDialogGraphContractED.Snapshot(node.LineData) ?? EndsDialogRulesED.CreateLine();

                        if (line.DialogsText == null) line.DialogsText = new List<LineText>();
                        if (line.CharacterActions == null) line.CharacterActions = new List<CharacterActionData>();

                        patch.Set(nameof(DialogNode.LineData), EndsDialogGraphContractED.ReadModel(line));
                    }
                    else patch.Set(nameof(DialogNode.RedirectData), EndsDialogGraphContractED.ReadModel(EndsDialogRulesED.CreateRedirect(selectedDialog?.initiator)));

                    command.Set("patch", patch);
                    SubmitEditorCommand(command);
                }

                return;
            }

            GraphStyles styles = GetGraphStyles(zoomScale);
            GUIStyle labelStyle = styles.Label;
            GUIStyle startlabelStyle = styles.StartLabel;
            GUIStyle buttonStyle = styles.Button;

            if (_nodeToolbarStyle == null || _toolbarZoomScale != zoomScale || _toolbarProSkin != EditorGUIUtility.isProSkin || _toolbarSkin != GUI.skin || _actionNumberFieldStyle == null || _actionNumberFieldStyle.fontSize != Mathf.Max(1, Mathf.RoundToInt(BASE_FONT_SIZE * zoomScale)))
            {
                _nodeToolbarStyle = new GUIStyle(EditorStyles.toolbar)
                {
                    fontSize = (int)(BASE_FONT_SIZE * zoomScale),
                    fixedHeight = NODE_TOOLBAR_HEIGHT * zoomScale,
                    padding = new RectOffset((int)(2 * zoomScale), (int)(2 * zoomScale), 0, 0)
                };
                _startToolbarStyle = new GUIStyle(_nodeToolbarStyle) { normal = { background = _startToolbarTexture } };
                _actionSliderStyle = ScaleActionStyle(GUI.skin.horizontalSlider);
                _actionSliderThumbStyle = ScaleActionStyle(GUI.skin.horizontalSliderThumb);
                _actionNumberFieldStyle = ScaleActionStyle(EditorStyles.numberField);
                _actionNumberFieldStyle.fixedHeight = 0;
                _actionNumberFieldStyle.fixedWidth = 0;
                _actionNumberFieldStyle.alignment = TextAnchor.MiddleRight;
                _toolbarZoomScale = zoomScale;
                _toolbarProSkin = EditorGUIUtility.isProSkin;
                _toolbarSkin = GUI.skin;
            }

            Event e = Event.current; // Current event
            bool isNodeSelected = selectedNodes.Contains(node);
            bool blockNodeInteraction = isNodeSelected;
            bool wasGuiEnabled = GUI.enabled;
            Color selectionGuiColor = GUI.color;
            float toolbarHeightZS = NODE_TOOLBAR_HEIGHT * zoomScale;

            // Scalable paddings like in standard Unity window
            float paddingLeftZS = Mathf.Max(NODE_LEFT_RIGHT_PADDING * zoomScale, GUI.skin.window.overflow.left);
            float paddingRightZS = Mathf.Max(NODE_LEFT_RIGHT_PADDING * zoomScale, GUI.skin.window.overflow.right);
            float paddingTopZS = NODE_TOP_PADDING * zoomScale;
            float paddingBottomZS = NODE_BOTTOM_PADDING * zoomScale;
            float spacingZS = NODE_SPACE_OFFSET * zoomScale;
            float nodeWidthZS = GetScaledNodeWidth(node);
            float contentWidthZS = nodeWidthZS - paddingLeftZS - paddingRightZS;

            float baseHeight = GetNodeHeight(node);
            float minHeight = MIN_NODE_HEIGHT;
            float totalHeight = Mathf.Max(baseHeight, minHeight) * zoomScale;
            // Let the background shadows use both side paddings while keeping fields at their content origin.
            Rect contentRect = new Rect(0, paddingTopZS, nodeWidthZS, totalHeight - paddingTopZS - paddingBottomZS);


            if (node.NodeType == NodeType.Start)    // Disable standard window background for Start
            {
                GUI.Box(new Rect(0, 0, nodeWidthZS, totalHeight), GUIContent.none, GetWindowStyles().EmptyNode);
            }

            Rect toolbarRect = new Rect(0, 0, nodeWidthZS, NODE_TOOLBAR_HEIGHT * zoomScale);
            GUIStyle toolbarStyleForNode;
            if (node.NodeType == NodeType.Start) toolbarStyleForNode = _startToolbarStyle;
            else toolbarStyleForNode = _nodeToolbarStyle;
            GUI.Box(toolbarRect, GUIContent.none, toolbarStyleForNode);

            string title = "Node " + EndsNodeRefED.ShortRef(node.nodeId);

            GUIStyle titleLabelStyle;
            if (node.NodeType == NodeType.Start) titleLabelStyle = startlabelStyle;
            else titleLabelStyle = labelStyle;
            EditorGUI.LabelField(new Rect(TITLE_LABEL_OFFSET * zoomScale, 0, TITLE_LABEL_WIDTH_ZOOM * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale), title, titleLabelStyle);

            if (blockNodeInteraction) GUI.enabled = false;

            if (node.NodeType == NodeType.Line)// Header buttons
            {
                GUI.backgroundColor = DELETE_BUTTON_COLOR;

                if (GUI.Button(new Rect(nodeWidthZS - spacingZS - NODE_BUTTON_SIZE * zoomScale, 0, NODE_BUTTON_SIZE * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale), _deleteNodeContent, buttonStyle) && e.button == 0)
                {
                    RecordEditorIntent("Remove Node");
                    RemoveDialogNode(selectedDialog, node);
                    GUIUtility.ExitGUI();
                }

                GUI.backgroundColor = Color.white;

                if (GUI.Button(new Rect(nodeWidthZS - 2 * spacingZS - 2 * NODE_BUTTON_SIZE * zoomScale, 0, NODE_BUTTON_SIZE * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale), _duplicateNodeContent, buttonStyle) && e.button == 0)
                {
                    RecordEditorIntent("Duplicate Node");
                    EndsGraphCommandsED.Duplicate(selectedDialog, node, EndsNodeIdentityED.Allocate(database, selectedDialog, node.NodeType), new Vector2(DUPLICATE_NODE_OFFSET, DUPLICATE_NODE_OFFSET));
                }

                if (GUI.Button(new Rect(nodeWidthZS - 3 * spacingZS - 2 * NODE_BUTTON_SIZE * zoomScale - SPRITE_BUTTON_WIDTH * zoomScale, 0, SPRITE_BUTTON_WIDTH * zoomScale,
                    NODE_TOOLBAR_HEIGHT * zoomScale), _addActionContent, buttonStyle) && e.button == 0)
                {
                    RecordEditorIntent("Add Character Action");
                    EndsNodeContentCommandsED.AddAction(node.LineData, EndsDialogRulesED.CreateAction());
                }

                // "Instant" button - node doesn't wait for click, proceeds immediately
                string instantKey = NodeControls(node).Child(NodeSchema.Line).Field(LineNodeSchema.Instant);

                if (EndsAiHighlightED.IsMarked(instantKey)) GUI.backgroundColor = EndsAiHighlightED.Tint(instantKey);
                else if (node.LineData.isInstant) GUI.backgroundColor = INSTANT_LINE_COLOR;
                else GUI.backgroundColor = DEFAULT_LINE_COLOR;

                Rect instantRect = new Rect(nodeWidthZS - LINE_NODE_INSTANT_BUTTON_SPACING_COUNT * spacingZS - LINE_NODE_TOOLBAR_RIGHT_BUTTONS_COUNT * NODE_BUTTON_SIZE
                    * zoomScale - SPRITE_BUTTON_WIDTH * zoomScale - NODE_BUTTON_SIZE * zoomScale, 0, NODE_BUTTON_SIZE * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale);

                if (GUI.Button(instantRect,
                    _instantLineContent, buttonStyle) && e.button == 0)
                {
                    RecordEditorIntent("Toggle Instant");
                    EndsNodeContentCommandsED.SetInstant(node.LineData, !node.LineData.isInstant);
                }
                EndsAiHighlightED.ReleaseIfAltHovered(instantKey, instantRect);
                GUI.backgroundColor = Color.white;
            }
            else if (node.NodeType == NodeType.Choice)
            {
                GUI.backgroundColor = DELETE_BUTTON_COLOR;    // "Delete" button (5 spacing from right edge)

                if (GUI.Button(new Rect(nodeWidthZS - spacingZS - NODE_BUTTON_SIZE * zoomScale, 0, NODE_BUTTON_SIZE * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale), _deleteNodeContent, buttonStyle) && e.button == 0)
                {
                    RecordEditorIntent("Remove Node");
                    RemoveDialogNode(selectedDialog, node);
                    GUIUtility.ExitGUI();
                }

                GUI.backgroundColor = Color.white;

                if (GUI.Button(new Rect(nodeWidthZS - 2 * spacingZS - 2 * NODE_BUTTON_SIZE * zoomScale, 0, NODE_BUTTON_SIZE * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale), _duplicateNodeContent, buttonStyle) && e.button == 0)   // "Duplicate" button (5 spacing from "Delete" button)
                {
                    RecordEditorIntent("Duplicate Node");
                    EndsGraphCommandsED.Duplicate(selectedDialog, node, EndsNodeIdentityED.Allocate(database, selectedDialog, node.NodeType), new Vector2(DUPLICATE_NODE_OFFSET, DUPLICATE_NODE_OFFSET));
                }

                if (GUI.Button(new Rect(nodeWidthZS - 3 * spacingZS - 2 * NODE_BUTTON_SIZE * zoomScale - SPRITE_BUTTON_WIDTH * zoomScale, 0, SPRITE_BUTTON_WIDTH * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale), _addChoiceOptionContent, buttonStyle) && e.button == 0)
                {
                    RecordEditorIntent("Add Choice Option");
                    EndsNodeContentCommandsED.AddChoice(node);
                }
            }
            else if (node.NodeType == NodeType.Condition)
            {
                GUI.backgroundColor = DELETE_BUTTON_COLOR;    // "Delete" button (5 spacing from right edge)

                if (GUI.Button(new Rect(nodeWidthZS - spacingZS - NODE_BUTTON_SIZE * zoomScale, 0, NODE_BUTTON_SIZE * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale), _deleteNodeContent, buttonStyle) && e.button == 0)
                {
                    RecordEditorIntent("Remove Node");
                    RemoveDialogNode(selectedDialog, node);
                    GUIUtility.ExitGUI();
                }

                GUI.backgroundColor = Color.white;

                if (GUI.Button(new Rect(nodeWidthZS - 2 * spacingZS - 2 * NODE_BUTTON_SIZE * zoomScale, 0, NODE_BUTTON_SIZE * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale), _duplicateNodeContent, buttonStyle) && e.button == 0)   // "Duplicate" button (5 spacing from "Delete" button)
                {
                    RecordEditorIntent("Duplicate Node");
                    EndsGraphCommandsED.Duplicate(selectedDialog, node, EndsNodeIdentityED.Allocate(database, selectedDialog, node.NodeType), new Vector2(DUPLICATE_NODE_OFFSET, DUPLICATE_NODE_OFFSET));
                }

                if (GUI.Button(new Rect(nodeWidthZS - 3 * spacingZS - 2 * NODE_BUTTON_SIZE * zoomScale - SPRITE_BUTTON_WIDTH * zoomScale, 0, SPRITE_BUTTON_WIDTH * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale), _addNodeConditionContent, buttonStyle) && e.button == 0)
                {
                    ConditionNode snapshot = EndsDialogConditionContractED.ReadSnapshot(node.ConditionData ?? EndsDialogRulesED.CreateConditionNode());

                    if (snapshot.DialogConditions == null) snapshot.DialogConditions = new List<DialogCondition>();

                    snapshot.DialogConditions.Add(EndsDialogRulesED.CreateDialogCondition(database));
                    SubmitConditionEdit(selectedDialogName, node.nodeId, snapshot, true, null, EndsSessionED.ForDatabase(database).Revision);
                }
            }
            else if (node.NodeType == NodeType.Redirect)
            {
                DrawRedirectNodeToolbar(node, nodeWidthZS, spacingZS, zoomScale, buttonStyle, e);
            }
            GUI.BeginGroup(contentRect);
            GUI.BeginClip(new Rect(0, 0, contentRect.width, contentRect.height), new Vector2(paddingLeftZS, 0), Vector2.zero, false);
            Rect conditionAreaRect = new Rect(0, 0, contentRect.width - paddingLeftZS, contentRect.height);
            EnsureNodeDrawers();
            NodeDrawContext drawContext = _nodeRenderContext;
            drawContext.WindowId = id;
            drawContext.Node = node;
            drawContext.NodeId = node.nodeId;
            drawContext.Styles = styles;
            drawContext.CurrentEvent = e;
            drawContext.BlockInteraction = blockNodeInteraction;
            drawContext.NodeWidth = nodeWidthZS;
            drawContext.ContentWidth = contentWidthZS;
            drawContext.CursorY = 0;
            drawContext.ChoiceIndex = 0;
            drawContext.ChoicesToRemove = null;
            EndsNodeDrawersED.Drawer<DialogNode, NodeDrawContext> payloadDrawer = FindNodePayloadDrawer(node);

            if (payloadDrawer != null) payloadDrawer.Draw(conditionAreaRect, node, drawContext);

            GUI.EndClip();
            GUI.EndGroup();

            if (blockNodeInteraction) GUI.enabled = wasGuiEnabled;

            GUI.color = selectionGuiColor;

            Rect headerDragRect = new Rect(0, 0, nodeWidthZS, toolbarHeightZS);
            bool headerDrag = e.button == 0 && headerDragRect.Contains(e.mousePosition) && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);

            if (headerDrag && pressedHeaderWindow != id)
            {
                pressedHeaderWindow = id;
                Repaint();
            }

            if (e.button == 0) GUI.DragWindow(headerDragRect);
        }

        private GUIStyle ScaleActionStyle(GUIStyle source)
        {
            return new GUIStyle(source)
            {
                fontSize = Mathf.Max(1, Mathf.RoundToInt(BASE_FONT_SIZE * zoomScale)),
                fixedWidth = source.fixedWidth * zoomScale,
                fixedHeight = source.fixedHeight * zoomScale,
                border = ScaleActionOffset(source.border),
                margin = ScaleActionOffset(source.margin),
                padding = ScaleActionOffset(source.padding),
                overflow = ScaleActionOffset(source.overflow)
            };
        }

        private RectOffset ScaleActionOffset(RectOffset source)
        {
            return new RectOffset(Mathf.RoundToInt(source.left * zoomScale), Mathf.RoundToInt(source.right * zoomScale), Mathf.RoundToInt(source.top * zoomScale), Mathf.RoundToInt(source.bottom * zoomScale));
        }


        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; i++)
            {
                pix[i] = col;
            }

            Texture2D result = new Texture2D(width, height) { hideFlags = HideFlags.HideAndDontSave };
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void DrawBox(Rect rect, Color color, int thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        #endregion 

        #region Graph Node Creation

        private void AddStartNode(Vector2 position)
        {
            AddGraphNode(NodeType.Start, position);
        }

        private void AddLineNode(Vector2 position)
        {
            AddGraphNode(NodeType.Line, position);
        }

        private void AddChoiceNode(Vector2 position)
        {
            AddGraphNode(NodeType.Choice, position);
        }

        private void AddConditionNode(Vector2 position)
        {
            AddGraphNode(NodeType.Condition, position);
        }

        private void AddRedirectNode(Vector2 position)
        {
            AddGraphNode(NodeType.Redirect, position);
        }

        private Vector2 GetToolbarNewNodePosition()
        {
            return editorOffset + new Vector2(DEFAULT_POSITION_XY, DEFAULT_POSITION_XY) / zoomScale;
        }

        #endregion

        #region Redirect Node





        private string BuildDeleteDialogConfirmMessage(string dialogName)
        {
            string message = $"Are you sure you want to delete the dialog \"{dialogName}\"?";

            IReadOnlyList<DialogReferenceUsage> redirectUsages = GetDialogReferenceIndex().GetRedirectTargetUsages(dialogName);

            if (redirectUsages == null || redirectUsages.Count == 0) return message;

            HashSet<string> sourceDialogNames = new HashSet<string>();
            for (int i = 0; i < redirectUsages.Count; i++) sourceDialogNames.Add(redirectUsages[i].DialogName);

            message += $"\n\nThis dialog is used as a redirect target in ({sourceDialogNames.Count}) dialogs across ({redirectUsages.Count}) redirect nodes.\n\nReferences will be marked as missing.";

            return message;
        }

        private static string FormatRedirectTargetDialogLabel(DialogEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.dialogName)) return "Unnamed";
            if (entry.isLocked) return "🔒 " + entry.dialogName;

            return entry.dialogName;
        }

        private bool ShouldDrawMissingDialogSlot(RedirectNode redirect)
        {
            if (redirect == null) return false;
            if (DialogReferenceUtility.IsMissing(redirect.DialogReferenceState)) return true;
            if (string.IsNullOrEmpty(redirect.TargetDialogName)) return false;

            DialogEntry entry = database?.Dialogs?.Find(d => d != null && d.dialogName == redirect.TargetDialogName);

            if (entry == null) return true;
            if (entry.initiator != redirect.TargetCategory) return true;
            if (!DialogEntryUtility.IsRedirectTargetCandidate(entry)) return true;

            List<string> namesInCategory = GetUiCatalog().GetRedirectChoices(redirect.TargetCategory).Names;

            if (!namesInCategory.Contains(redirect.TargetDialogName)) return true;

            return false;
        }

        private string GetMissingDialogDisplayLabel(RedirectNode redirect)
        {
            if (redirect == null) return DialogReferenceMessagesED.MISSING_DIALOG_SLOT;
            if (DialogReferenceUtility.IsMissing(redirect.DialogReferenceState)) return DialogReferenceMessagesED.FormatMissingDialogLabel(redirect.MissingDialogReferenceLabel);
            if (string.IsNullOrEmpty(redirect.TargetDialogName)) return DialogReferenceMessagesED.MISSING_DIALOG_SLOT;

            DialogEntry entry = database?.Dialogs?.Find(d => d != null && d.dialogName == redirect.TargetDialogName);

            if (entry == null) return DialogReferenceMessagesED.FormatMissingDialogLabel(redirect.TargetDialogName);
            if (!DialogEntryUtility.IsRedirectTargetCandidate(entry)) return $"Dialog \"{redirect.TargetDialogName}\" is not a redirect target.";

            return DialogReferenceMessagesED.MISSING_DIALOG_SLOT;
        }



        private const string REDIRECT_TARGET_LOCK_PREFIX = "🔒 ";



        private void DrawRedirectHintHelpBox(Rect rect, string message, float zoomScale)
        {
            GUIStyle hintStyle = GetGraphStyles(zoomScale).RedirectHint;

            GUI.Label(rect, message, hintStyle);
        }

        private void DrawRedirectHelpBoxArea(Rect rect, RedirectNode redirect, float zoomScale, GUIStyle labelStyle)
        {
            if (ShouldDrawMissingDialogSlot(redirect))
            {
                GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
                DrawMissingReferenceWarning(rect, GetMissingDialogDisplayLabel(redirect), zoomScale, labelStyle);
                return;
            }

            DrawRedirectHintHelpBox(rect, "Dialog must be without conditions or locked.", zoomScale);
        }





        #endregion 

        #region Connections Between Ports

        private void DrawNodeConnection(Vector2 start, Vector2 end, string highlightKey)
        {
            bool marked = EndsAiHighlightED.IsMarked(highlightKey);

            if (Event.current.type == EventType.Repaint)
            {
                Color color = Color.white;

                if (marked) color = EndsAiHighlightED.LineTint(highlightKey);

                Handles.DrawBezier(start, end, start + Vector2.up * BEZIER_OFFSET, end + Vector2.down * BEZIER_OFFSET, color, null, BEZIER_THICKNESS);
            }

            if (marked) ReleaseConnectionIfClicked(highlightKey, start, end);
        }

        private void ReleaseConnectionIfClicked(string highlightKey, Vector2 start, Vector2 end)
        {
            Event current = Event.current;

            if (current == null || !current.alt || EndsAiHighlightED.IsPending(highlightKey)) return;

            bool move = current.type == EventType.MouseMove || current.rawType == EventType.MouseMove;
            bool key = current.type == EventType.KeyDown || current.rawType == EventType.KeyDown;
            bool paint = current.type == EventType.Repaint;

            if (!move && !key && !paint) return;

            Vector2 mouse = current.mousePosition;
            Vector2 previous = start;
            Vector2 controlStart = start + Vector2.up * BEZIER_OFFSET;
            Vector2 controlEnd = end + Vector2.down * BEZIER_OFFSET;

            for (int step = 1; step <= 24; step++)
            {
                float t = step / 24f;
                float inverse = 1f - t;
                Vector2 point = inverse * inverse * inverse * start
                    + 3f * inverse * inverse * t * controlStart
                    + 3f * inverse * t * t * controlEnd
                    + t * t * t * end;

                if (DistanceToSegment(mouse, previous, point) <= 36f)
                {
                    EndsAiHighlightED.AcknowledgeSaved(database, highlightKey);

                    return;
                }

                previous = point;
            }
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float length = segment.sqrMagnitude;

            if (length <= 0.001f) return Vector2.Distance(point, a);

            float along = Mathf.Clamp01(Vector2.Dot(point - a, segment) / length);

            return Vector2.Distance(point, a + segment * along);
        }

        #endregion 



        #region RMB Node Creation Menu

        private void ShowNodeContextMenu(Vector2 position)
        {
            GenericMenu menu = new GenericMenu();
            bool hasStartNode = selectedDialog.nodes.Any(n => n.NodeType == NodeType.Start);

            if (!hasStartNode) menu.AddItem(new GUIContent("Add Start Node"), false, GraphCallback(() => AddStartNode(position)));
            else menu.AddDisabledItem(new GUIContent("Add Start Node (already exists)"));

            menu.AddItem(new GUIContent("Add Line Node"), false, GraphCallback(() => AddLineNode(position)));
            menu.AddItem(new GUIContent("Add Choice Node"), false, GraphCallback(() => AddChoiceNode(position)));
            menu.AddItem(new GUIContent("Add Condition Node"), false, GraphCallback(() => AddConditionNode(position)));
            menu.AddItem(new GUIContent("Add Redirect Node"), false, GraphCallback(() => AddRedirectNode(position)));

            menu.ShowAsContext();
        }

        #endregion RMB Node Creation Menu

        #endregion Dialog Editor (Graph)


        #region Localization CSV

        private void OnLocalizationImportApplied()
        {
            CaptureDialogEditorDirtyBaseline();
            InvalidateReferenceIndex();
        }

        #endregion Localization CSV


        #region Character Names Editor

        private void DrawCharacterNameColors()
        {
            const int BUTTON_ADD_NAME_COLOR_WIDTH = 150;
            const int DELETE_BUTTON_SIZE = 20;
            const int LABEL_WIDTH = 50;
            const int NAME_FIELD_WIDTH = 120;
            const int COLOR_FIELD_WIDTH = 80;
            const int COLOR_FIELD_HEIGHT = 18;
            const int LANG_LABEL_WIDTH = 30;
            const int TRANSLATION_FIELD_WIDTH = 150;
            const int FIELD_SPACING = 8;
            const int ROW_SPACING = 20;

            if (database == null || database.CharacterNameColors == null)
            {
                BeginAssetSlotActionRow();
                DrawInlineDialogDatabaseSlot();
                FillAssetSlotToolbarRow();
                EndAssetSlotActionRow();
                EndInlineHeaderSection();
                EditorGUILayout.HelpBox("Assign or create a Dialog Database.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            BeginAssetSlotActionRow();
            DrawInlineDialogDatabaseSlot();
            GUILayout.FlexibleSpace();

            if (AssetRowToolbarButton(_addNameColorContent, BUTTON_ADD_NAME_COLOR_WIDTH))
            {
                EndsMcpJsonED command = EndsDialogGraphContractED.Command("name.add");
                command.Set("name", EndsMcpJsonED.Str("New Name"));
                command.Set("unique", EndsMcpJsonED.Boolean(true));
                SubmitEditorCommand(command);
                InvalidateReferenceIndex();
            }

            EndAssetSlotActionRow();
            EndInlineHeaderSection();

            nameColorsScrollPos = EditorGUILayout.BeginScrollView(nameColorsScrollPos, GUILayout.ExpandHeight(true));
            for (int i = 0; i < database.CharacterNameColors.Count; i++)
            {
                CharacterNameColor sourceName = database.CharacterNameColors[i];

                if (sourceName == null) continue;

                CharacterNameColor colorEntry = EndsDialogGraphContractED.Snapshot(sourceName);
                EndsMcpJsonED namePatch = EndsMcpJsonED.Obj();
                EndsMcpJsonED renameCommand = null;
                Color nameRowColor = GUI.backgroundColor;
                string nameHighlightKey = "name/" + (colorEntry.Name ?? "");

                if (EndsAiHighlightED.IsMarked(nameHighlightKey)) GUI.backgroundColor = EndsAiHighlightED.Tint(nameHighlightKey);

                Rect nameBox = EditorGUILayout.BeginVertical("box");
                GUI.backgroundColor = nameRowColor;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Color for character name {i + 1}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUI.backgroundColor = DELETE_BUTTON_COLOR;

                if (GUILayout.Button(_deleteNameColorContent, GUILayout.Width(DELETE_BUTTON_SIZE), GUILayout.Height(DELETE_BUTTON_SIZE)))
                {
                    if (TryRemoveCharacterNameColorFromRegistry(i, colorEntry)) GUIUtility.ExitGUI();
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Name:", GUILayout.Width(LABEL_WIDTH));
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.TextField(colorEntry.Name ?? "", GUILayout.Width(NAME_FIELD_WIDTH));

                if (EditorGUI.EndChangeCheck() && newName != colorEntry.Name)
                {
                    renameCommand = EndsDialogGraphContractED.Command("name.rename");
                    renameCommand.Set("name", EndsMcpJsonED.Str(colorEntry.Name));
                    renameCommand.Set("newName", EndsMcpJsonED.Str(newName));
                }

                GUILayout.Space(ROW_SPACING);
                GUILayout.Label("Color:", GUILayout.Width(LABEL_WIDTH));
                EditorGUI.BeginChangeCheck();
                Color newColor = EditorGUILayout.ColorField(colorEntry.Color, GUILayout.Width(COLOR_FIELD_WIDTH), GUILayout.Height(COLOR_FIELD_HEIGHT));

                if (EditorGUI.EndChangeCheck())
                {
                    colorEntry.Color = newColor;
                    namePatch.Set(nameof(CharacterNameColor.Color), EndsDialogGraphContractED.ReadModel(newColor));
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Name translations (substituted when selecting a character in a line):", EditorStyles.miniLabel);

                if (colorEntry.Translations == null) colorEntry.Translations = new List<NameTranslation>();

                foreach (string lang in availableLanguages)
                {
                    var trans = colorEntry.Translations.Find(t => t.languageCode == lang);

                    if (trans == null)
                    {
                        trans = new NameTranslation { languageCode = lang, displayName = colorEntry.Name ?? "" };
                    }

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(ROW_SPACING);
                    GUILayout.Label($"{lang}:", GUILayout.Width(LANG_LABEL_WIDTH));
                    EditorGUI.BeginChangeCheck();
                    string newDisplay = EditorGUILayout.TextField(trans.displayName ?? "", GUILayout.Width(TRANSLATION_FIELD_WIDTH));

                    if (EditorGUI.EndChangeCheck())
                    {
                        EndsNameCommandsED.SetTranslation(colorEntry, lang, newDisplay);
                        namePatch.Set(nameof(CharacterNameColor.Translations), EndsDialogGraphContractED.ReadModel(colorEntry.Translations));
                    }

                    GUILayout.Space(FIELD_SPACING);
                    string previewText;

                    if (!string.IsNullOrEmpty(newDisplay)) previewText = newDisplay;
                    else previewText = "-";

                    var nameStyle = GetWindowStyles().NameLabel;
                    nameStyle.normal.textColor = colorEntry.Color;
                    nameStyle.hover.textColor = colorEntry.Color;
                    nameStyle.active.textColor = colorEntry.Color;
                    GUILayout.Label(previewText, nameStyle, GUILayout.ExpandWidth(true));
                    
                    EditorGUILayout.EndHorizontal();
                }

                if (namePatch.Keys.Count > 0)
                {
                    EndsMcpJsonED command = EndsDialogGraphContractED.Command("name.patch");
                    command.Set("name", EndsMcpJsonED.Str(colorEntry.Name));
                    command.Set("patch", namePatch);
                    SubmitEditorCommand(command);
                }

                if (renameCommand != null) SubmitEditorCommand(renameCommand);

                EditorGUILayout.EndVertical();
                EndsAiHighlightED.ReleaseIfAltHovered(nameHighlightKey, nameBox);
                GUI.backgroundColor = nameRowColor;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private bool TryRemoveCharacterNameColorFromRegistry(int index, CharacterNameColor colorEntry)
        {
            if (colorEntry == null || database?.CharacterNameColors == null) return false;
            if (index < 0 || index >= database.CharacterNameColors.Count) return false;

            string name = colorEntry.Name;
            DialogReferenceIndex referenceIndex = GetLiveDialogReferenceIndex();
            IReadOnlyList<DialogReferenceUsage> usages = referenceIndex.GetLineCharacterNameUsages(name);
            IReadOnlyList<DialogReferenceUsage> savedUsages = _savedDialogReferenceIndex.GetLineCharacterNameUsages(name);

            string message;
            if (usages.Count > 0) message = BuildRegistryDeleteUsageMessage(usages, savedUsages, "The name color will be removed from the registry, and references in lines will be marked as missing.");
            else message = $"Delete name color '{name}'?";

            if (!EditorUtility.DisplayDialog("Delete name color", message, "Delete", "Cancel")) return false;

            SubmitNamedEditorCommand("name.remove", name);
            InvalidateReferenceIndex();
            return true;
        }

        #endregion Character Names Editor

    }
}
#endif