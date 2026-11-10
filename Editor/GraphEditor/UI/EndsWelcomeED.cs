#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary> ENDS. Shows release highlights at Unity startup and an optional review request when the dialog editor is opened. </summary>
    internal sealed class EndsWelcomeED : EditorWindow
    {
        #region FIELDS AND PROPERTIES

        private const string VERSION = "2.0.2";
        private const string ASSET_STORE_REVIEW_URL = "https://assetstore.unity.com/packages/slug/390618#reviews";
        private const string STATE_PREFIX = "ALTUSHKA.ENDS.Popups.";
        private const string PREF_NOTES_DISMISSED_VERSION = STATE_PREFIX + "ReleaseNotesDismissedVersion";
        private const string PREF_EDITOR_OPENINGS = STATE_PREFIX + "EditorOpenings";
        private const string PREF_REVIEW_DISMISSED = STATE_PREFIX + "ReviewDismissed";
        private const string SESSION_EDITOR_INSTANCE = STATE_PREFIX + "EditorInstance";
        private const string SESSION_NOTES_VERSION = STATE_PREFIX + "ReleaseNotesShownVersion";
        private const string SESSION_NOTES_PENDING = STATE_PREFIX + "ReleaseNotesPending";
        private const string SESSION_REVIEW_PENDING = STATE_PREFIX + "ReviewPending";
        private const string NOTES_TITLE = "What's new in ENDS";
        private const string REVIEW_TITLE = "Support the developer";
        private const string REVIEW_INTRO = "Thank you for using Easy Node Dialog System.";
        private const string REVIEW_REQUEST = "If ENDS helps your project, please consider leaving an honest rating and review on the Unity Asset Store. Your feedback helps me improve the tool. Thank you!";

        private const int REVIEW_OPENING = 2;
        private const int NOTES_WIDTH = 640;
        private const int REVIEW_WIDTH = 500;
        private const int INITIAL_HEIGHT = 400;
        private const int MIN_HEIGHT = 180;
        private const int WINDOW_PADDING = 20;
        private const int TITLE_SPACING = 16;
        private const int SECTION_SPACING = 12;
        private const int HEADING_SPACING = 6;
        private const int FOOTER_SPACING = 12;
        private const int BUTTON_SPACING = 8;
        private const int BUTTON_HEIGHT = 32;
        private const int TITLE_SIZE = 18;
        private const int VERSION_HEADING_SIZE = 15;
        private const int HEADING_SIZE = 13;
        private const int BODY_SIZE = 12;
        private const int ACCENT_HEIGHT = 2;
        private const int DIVIDER_HEIGHT = 1;
        private const float CENTER_RATIO = 0.5f;
        private const float MAX_HEIGHT_RATIO = 0.95f;
        private const float HEADING_ACCENT_BLEND = 0.5f;
        private const float DIVIDER_ALPHA = 0.3f;

        private static readonly Color DARK_ACCENT = new Color32(92, 230, 230, 255);
        private static readonly Color LIGHT_ACCENT = new Color32(0, 104, 117, 255);
        private static readonly Color DARK_BUTTON_TINT = new Color32(85, 174, 178, 255);
        private static readonly Color LIGHT_BUTTON_TINT = new Color32(175, 232, 232, 255);
        private static readonly GUIContent _reviewButton = new GUIContent("Rate on Asset Store", "Open the ENDS reviews page on the Unity Asset Store.");
        private static readonly Release[] _releases =
        {
            new Release("Version " + VERSION, new[]
            {
                new Feature("Online screenshots", "Documentation screenshots are now hosted on GitHub."),
                new Feature("Git installation", "Install ENDS through Unity Package Manager using a Git URL.")
            }),
            new Release("Version 2.0.1", new[]
            {
                new Feature("Clearer AI graph layouts", "MCP placement uses expanded node sizes and port positions to space cards, stagger tall lines, and make branches and joins easier to follow."),
                new Feature("Smoother editing on large graphs", "The editor skips offscreen node drawing and reuses text measurements. Zooming out keeps node controls available.")
            }),
            new Release("Version 2.0.0", new[]
            {
                new Feature("AI editing through MCP", "Connect an AI client to read and edit dialogs alongside the visual editor. Multiple clients can work on separate dialogs with editing ownership."),
                new Feature("Changes and AI highlights", "Review added, removed and changed blocks in the Changes tree. Yellow marks show pending AI edits, green marks show saved edits awaiting acknowledgement."),
                new Feature("Save selected work", "Save or discard selected dialogs and shared data while leaving other work pending. UI and clients share working content and Undo/Redo."),
                new Feature("Updated CSV localization", "CSV import and export now use readable dot-separated keys and permanent option IDs, keeping translations attached when options are reordered."),
                new Feature("Numeric consequences", "Use =, += and -= to assign values, give rewards or subtract a cost when a player selects an option."),
                new Feature("Smoother graph editing", "Improved responsiveness on large databases, clearer node and option labels, and corrected sprite previews for sprite sheets.")
            })
        };

        private static EndsWelcomeED _activeWindow;
        private static EditorWindow _editorWindow;
        [SerializeField] private bool _reviewPrompt;
        [SerializeField] private bool _wasShown;
        [SerializeField] private Vector2 _scrollPosition;
        private GUIStyle _titleStyle;
        private GUIStyle _versionStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _paddingStyle;
        private GUIStyle _buttonStyle;
        private Color _accentColor;
        private Color _buttonTint;
        private bool _darkTheme;
        private bool _needsScroll;
        private int _contentHeight;

        private readonly struct Release
        {
            internal readonly string Heading;
            internal readonly Feature[] Features;

            internal Release(string heading, Feature[] features)
            {
                Heading = heading;
                Features = features;
            }
        }

        private readonly struct Feature
        {
            internal readonly string Heading;
            internal readonly string Body;

            internal Feature(string heading, string body)
            {
                Heading = heading;
                Body = body;
            }
        }

        #endregion FIELDS AND PROPERTIES

        #region UNITY

        private void OnEnable()
        {
            _activeWindow = this;
            _titleStyle = null;
            _contentHeight = 0;
        }

        private void OnDestroy()
        {
            if (_activeWindow == this) _activeWindow = null;
            if (!_wasShown) return;

            QueuePendingWindow();
        }

        private void OnGUI()
        {
            if (_titleStyle == null || _darkTheme != EditorGUIUtility.isProSkin)
            {
                _darkTheme = EditorGUIUtility.isProSkin;
                _accentColor = LIGHT_ACCENT;
                _buttonTint = LIGHT_BUTTON_TINT;

                if (_darkTheme)
                {
                    _accentColor = DARK_ACCENT;
                    _buttonTint = DARK_BUTTON_TINT;
                }

                _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = TITLE_SIZE, wordWrap = true, margin = new RectOffset(), padding = new RectOffset() };
                _headingStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = HEADING_SIZE, wordWrap = true, margin = new RectOffset(), padding = new RectOffset() };
                _bodyStyle = new GUIStyle(EditorStyles.label) { fontSize = BODY_SIZE, wordWrap = true, margin = new RectOffset(), padding = new RectOffset() };
                _titleStyle.normal.textColor = _accentColor;
                _versionStyle = new GUIStyle(_titleStyle) { fontSize = VERSION_HEADING_SIZE };
                _headingStyle.normal.textColor = Color.Lerp(EditorStyles.boldLabel.normal.textColor, _accentColor, HEADING_ACCENT_BLEND);
                _paddingStyle = new GUIStyle { padding = new RectOffset(WINDOW_PADDING, WINDOW_PADDING, WINDOW_PADDING, WINDOW_PADDING) };
                _buttonStyle = new GUIStyle(GUI.skin.button) { margin = new RectOffset() };
                _contentHeight = 0;
            }

            string heading = NOTES_TITLE;

            if (_reviewPrompt) heading = REVIEW_TITLE;
            if (_contentHeight == 0)
            {
                float textWidth = position.width - WINDOW_PADDING * 2;
                float height = WINDOW_PADDING * 2 + _titleStyle.CalcHeight(new GUIContent(heading), textWidth) + TITLE_SPACING + FOOTER_SPACING * 2 + DIVIDER_HEIGHT + BUTTON_HEIGHT;

                if (_reviewPrompt) height += _bodyStyle.CalcHeight(new GUIContent(REVIEW_INTRO), textWidth) + SECTION_SPACING + _bodyStyle.CalcHeight(new GUIContent(REVIEW_REQUEST), textWidth);
                else
                {
                    for (int i = 0; i < _releases.Length; i++)
                    {
                        if (i > 0) height += TITLE_SPACING;

                        Release release = _releases[i];
                        height += _versionStyle.CalcHeight(new GUIContent(release.Heading), textWidth) + HEADING_SPACING;

                        for (int j = 0; j < release.Features.Length; j++)
                        {
                            if (j > 0) height += SECTION_SPACING;

                            Feature feature = release.Features[j];
                            height += _headingStyle.CalcHeight(new GUIContent(feature.Heading), textWidth) + HEADING_SPACING + _bodyStyle.CalcHeight(new GUIContent(feature.Body), textWidth);
                        }
                    }
                }

                int availableHeight = Mathf.Max(MIN_HEIGHT, Mathf.FloorToInt(EditorGUIUtility.GetMainWindowPosition().height * MAX_HEIGHT_RATIO));
                _needsScroll = Mathf.CeilToInt(height) > availableHeight;
                _contentHeight = Mathf.Min(Mathf.CeilToInt(height), availableHeight);
                Rect fittedBounds = position;
                fittedBounds.y += (fittedBounds.height - _contentHeight) * CENTER_RATIO;
                fittedBounds.height = _contentHeight;
                minSize = new Vector2(position.width, _contentHeight);
                maxSize = minSize;
                position = fittedBounds;
            }

            EditorGUI.DrawRect(new Rect(0, 0, position.width, ACCENT_HEIGHT), _accentColor);
            GUILayout.BeginVertical(_paddingStyle);
            GUILayout.Label(heading, _titleStyle);
            GUILayout.Space(TITLE_SPACING);

            if (_needsScroll) _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            if (_reviewPrompt)
            {
                GUILayout.Label(REVIEW_INTRO, _bodyStyle);
                GUILayout.Space(SECTION_SPACING);
                GUILayout.Label(REVIEW_REQUEST, _bodyStyle);
            }
            else
            {
                for (int i = 0; i < _releases.Length; i++)
                {
                    if (i > 0) GUILayout.Space(TITLE_SPACING);

                    Release release = _releases[i];
                    GUILayout.Label(release.Heading, _versionStyle);
                    GUILayout.Space(HEADING_SPACING);

                    for (int j = 0; j < release.Features.Length; j++)
                    {
                        if (j > 0) GUILayout.Space(SECTION_SPACING);

                        Feature feature = release.Features[j];
                        GUILayout.Label(feature.Heading, _headingStyle);
                        GUILayout.Space(HEADING_SPACING);
                        GUILayout.Label(feature.Body, _bodyStyle);
                    }
                }
            }

            if (_needsScroll) EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            GUILayout.Space(FOOTER_SPACING);
            Rect divider = GUILayoutUtility.GetRect(0, DIVIDER_HEIGHT, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(divider, new Color(_accentColor.r, _accentColor.g, _accentColor.b, DIVIDER_ALPHA));
            GUILayout.Space(FOOTER_SPACING);
            GUILayout.BeginHorizontal();

            if (_reviewPrompt)
            {
                Color previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = _buttonTint;
                bool openStore = GUILayout.Button(_reviewButton, _buttonStyle, GUILayout.Height(BUTTON_HEIGHT));
                GUI.backgroundColor = previousBackground;

                if (openStore)
                {
                    EditorPrefs.SetBool(PREF_REVIEW_DISMISSED, true);
                    Application.OpenURL(ASSET_STORE_REVIEW_URL);
                    Close();
                }

                GUILayout.Space(BUTTON_SPACING);
            }

            if (GUILayout.Button("Don't show again", _buttonStyle, GUILayout.Height(BUTTON_HEIGHT)))
            {
                if (_reviewPrompt) EditorPrefs.SetBool(PREF_REVIEW_DISMISSED, true);
                else EditorPrefs.SetString(PREF_NOTES_DISMISSED_VERSION, VERSION);

                Close();
            }

            GUILayout.Space(BUTTON_SPACING);

            if (GUILayout.Button("Close", _buttonStyle, GUILayout.Height(BUTTON_HEIGHT))) Close();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        #endregion UNITY

        #region OPENING LIFECYCLE

        [InitializeOnLoadMethod]
        private static void InitializePopups()
        {
            if (Application.isBatchMode) return;
            if (SessionState.GetString(SESSION_NOTES_VERSION, "") != VERSION)
            {
                SessionState.SetString(SESSION_NOTES_VERSION, VERSION);
                SessionState.SetBool(SESSION_NOTES_PENDING, EditorPrefs.GetString(PREF_NOTES_DISMISSED_VERSION, "") != VERSION);
            }

            QueuePendingWindow();
        }

        /// <summary> ENDS. Counts displayed editor openings without counting focus changes or script reloads, then queues an eligible review request. </summary>
        internal static void RecordEditorOpening(EditorWindow editorWindow)
        {
            if (Application.isBatchMode) return;

            _editorWindow = editorWindow;
            string instanceId = EndsUnityObjectReferenceED.EncodeTransientId(editorWindow).Text;

            if (SessionState.GetString(SESSION_EDITOR_INSTANCE, "") != instanceId)
            {
                SessionState.SetString(SESSION_EDITOR_INSTANCE, instanceId);
                int openings = Mathf.Min(EditorPrefs.GetInt(PREF_EDITOR_OPENINGS, 0) + 1, REVIEW_OPENING);
                EditorPrefs.SetInt(PREF_EDITOR_OPENINGS, openings);

                if (openings >= REVIEW_OPENING && !EditorPrefs.GetBool(PREF_REVIEW_DISMISSED, false)) SessionState.SetBool(SESSION_REVIEW_PENDING, true);
            }

            QueuePendingWindow();
        }

        private static void QueuePendingWindow()
        {
            EditorApplication.update -= ShowPendingWindow;

            if (Application.isBatchMode) return;
            if (!SessionState.GetBool(SESSION_NOTES_PENDING, false) && !SessionState.GetBool(SESSION_REVIEW_PENDING, false)) return;

            EditorApplication.update += ShowPendingWindow;
        }

        private static void ShowPendingWindow()
        {
            if (Application.isBatchMode)
            {
                EditorApplication.update -= ShowPendingWindow;

                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;

            EditorApplication.update -= ShowPendingWindow;

            if (_activeWindow != null) return;
            if (SessionState.GetBool(SESSION_NOTES_PENDING, false))
            {
                SessionState.SetBool(SESSION_NOTES_PENDING, false);

                if (EditorPrefs.GetString(PREF_NOTES_DISMISSED_VERSION, "") != VERSION)
                {
                    ShowWelcomeWindow(false);

                    return;
                }
            }
            if (_editorWindow != null && SessionState.GetBool(SESSION_REVIEW_PENDING, false))
            {
                SessionState.SetBool(SESSION_REVIEW_PENDING, false);

                if (!EditorPrefs.GetBool(PREF_REVIEW_DISMISSED, false)) ShowWelcomeWindow(true);
            }
        }

        /// <summary> ENDS. Reopens the version highlights without changing the automatic popup preferences. </summary>
        [MenuItem("Window/Easy Node Dialog System/What's New")]
        internal static void OpenReleaseNotes()
        {
            if (_activeWindow != null && !_activeWindow._reviewPrompt)
            {
                _activeWindow.Focus();

                return;
            }
            if (_activeWindow != null) _activeWindow.Close();

            ShowWelcomeWindow(false);
        }

        /// <summary> ENDS. Opens the optional review request without opening a browser until its review button is clicked. </summary>
        [MenuItem("Window/Easy Node Dialog System/Support the Developer")]
        internal static void OpenReviewRequest()
        {
            if (_activeWindow != null && _activeWindow._reviewPrompt)
            {
                _activeWindow.Focus();

                return;
            }
            if (_activeWindow != null) _activeWindow.Close();

            ShowWelcomeWindow(true);
        }

        private static void ShowWelcomeWindow(bool reviewPrompt)
        {
            EndsWelcomeED window = CreateInstance<EndsWelcomeED>();
            window._reviewPrompt = reviewPrompt;
            int width = NOTES_WIDTH;

            if (reviewPrompt) width = REVIEW_WIDTH;

            Rect editorBounds = EditorGUIUtility.GetMainWindowPosition();
            window.titleContent = new GUIContent("Easy Node Dialog System");
            window.minSize = new Vector2(width, MIN_HEIGHT);
            window.maxSize = new Vector2(width, Mathf.Max(INITIAL_HEIGHT, editorBounds.height));
            window.position = new Rect(editorBounds.center.x - width * CENTER_RATIO, editorBounds.center.y - INITIAL_HEIGHT * CENTER_RATIO, width, INITIAL_HEIGHT);
            window.ShowUtility();
            window._wasShown = true;

            if (reviewPrompt) SessionState.SetBool(SESSION_REVIEW_PENDING, false);
            else
            {
                SessionState.SetString(SESSION_NOTES_VERSION, VERSION);
                SessionState.SetBool(SESSION_NOTES_PENDING, false);
            }
        }

        #endregion OPENING LIFECYCLE
    }
}
#endif