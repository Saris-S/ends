#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public partial class DialogEditorWindowED
    {
        #region FIELDS

        private const int CHANGES_HEADER_HEIGHT = 54;
        private const int CHANGES_FOOTER_HEIGHT = 78;
        private const int CHANGES_CLOSE_WIDTH = 30;
        private const int CHANGES_SCROLLBAR_WIDTH = 16;
        private const int CHANGES_CARD_GAP = 6;
        private const int CHANGES_FIELD_GAP = 5;
        private const int CHANGES_ROW_GAP = 2;
        private const int CHANGES_FIELD_MIN_VALUE_WIDTH = 90;
        private const int CHANGES_CARD_BORDER = 1;
        private const int CHANGES_CARD_PADDING = 7;
        private const int CHANGES_ACCENT_WIDTH = 3;
        private const int CHANGES_TREE_INDENT = 10;
        private static readonly GUIContent _collapseChangesContent = new GUIContent("→", "Collapse the changes panel");
        private static readonly GUIContent _otherChangesContent = new GUIContent("Other unsaved areas", "Include other dialogs or shared data when saving dependencies together.");
        private static readonly GUIContent _changeHistoryContent = new GUIContent("Recent activity", "The latest 64 recorded operations in this session. This is history, not the list of unsaved differences.");
        private static readonly Color _addedChangeColor = new Color(0.45f, 0.8f, 0.55f);
        private static readonly Color _removedChangeColor = new Color(0.95f, 0.5f, 0.45f);
        private static readonly Color _modifiedChangeColor = new Color(0.9f, 0.75f, 0.4f);
        private static readonly Color _changesCardDarkColor = new Color(0.25f, 0.25f, 0.25f, 1);
        private static readonly Color _changesCardLightColor = new Color(0.78f, 0.78f, 0.78f, 1);
        private static GUIStyle _changesCardStyle;
        private readonly HashSet<string> _changesSelection = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _expandedChanges = new HashSet<string>(StringComparer.Ordinal);
        private bool _showChanges;
        private bool _showOtherChanges;
        private bool _showChangeHistory;
        private bool _changesPointerActive;
        private bool _changesKeyboardFocus;
        private bool _changesActionPending;
        private Vector2 _changesScroll;
        private DialogDatabase _changesDatabase;
        private string _changesDialogKey;
        private EndsMcpJsonED _pendingChanges;
        private readonly Dictionary<EndsMcpJsonED, EndsMcpJsonED> _presentedChanges = new Dictionary<EndsMcpJsonED, EndsMcpJsonED>();
        private EndsMcpJsonED _changesCollaboration;
        private int _changesRevision = -1;
        private string _changesError;
        private GUIStyle _changesFieldLabelStyle;
        private GUIStyle _changesFieldCaptionStyle;
        private GUIStyle _changesFieldValueStyle;
        private GUIStyle _changesHeaderStatusStyle;

        #endregion FIELDS

        #region CHANGES PANEL

        private void DrawChangesPanel(Rect panelRect)
        {
            if (!_showChanges) return;

            EndsSessionED session = EndsSessionED.ForDatabase(database);
            string dialogKey = selectedDialog.NodeIdentityKey;

            if (_pendingChanges == null || Event.current.type == EventType.Layout)
            {
                int revision = session.ObservedRevision;

                if (_changesDatabase != database || _changesDialogKey != dialogKey)
                {
                    _changesDatabase = database;
                    _changesDialogKey = dialogKey;
                    _changesSelection.Clear();
                    _expandedChanges.Clear();
                    _pendingChanges = null;
                    _changesRevision = -1;
                    _changesScroll = Vector2.zero;
                    _changesError = null;
                }

                if (_pendingChanges == null || revision != _changesRevision)
                {
                    bool currentWasDirty = false;

                    foreach (EndsMcpJsonED scope in _pendingChanges?.Items ?? new List<EndsMcpJsonED>())
                    {
                        if (scope.Get("key").Text == dialogKey) currentWasDirty = true;
                    }

                    _pendingChanges = session.ReadPendingChanges();
                    _presentedChanges.Clear();
                    _changesCollaboration = session.ReadCollaboration(true);
                    _changesRevision = revision;
                    HashSet<string> available = new HashSet<string>(StringComparer.Ordinal);

                    foreach (EndsMcpJsonED scope in _pendingChanges.Items)
                    {
                        string key = scope.Get("key").Text;
                        bool blocked = IsChangeScopeBlocked(scope.Get("name").Text);

                        if (blocked) continue;

                        available.Add(key);

                        if (key == dialogKey && !currentWasDirty) _changesSelection.Add(key);
                    }

                    _changesSelection.IntersectWith(available);
                }
            }

            bool wasEnabled = GUI.enabled;
            Color oldBackground = GUI.backgroundColor;
            GUI.enabled = true;
            GUI.BeginGroup(panelRect);

            try
            {
                GUI.backgroundColor = LINE_COLOR_2;
                GUI.Box(new Rect(Vector2.zero, panelRect.size), GUIContent.none, GetWindowStyles().ConditionsBackground);
                GUI.backgroundColor = oldBackground;
                float width = panelRect.width - 2 * CONDITIONS_WINDOW_BORDER;
                GUI.Label(new Rect(CONDITIONS_WINDOW_BORDER, CONDITIONS_WINDOW_BORDER, width - CHANGES_CLOSE_WIDTH, FIELD_ROW_HEIGHT), "Changes", EditorStyles.boldLabel);

                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    CollapseChangesPanel();
                    Event.current.Use();
                }

                if (GUI.Button(new Rect(panelRect.width - CONDITIONS_WINDOW_BORDER - CHANGES_CLOSE_WIDTH, CONDITIONS_WINDOW_BORDER, CHANGES_CLOSE_WIDTH, FIELD_ROW_HEIGHT), _collapseChangesContent, EditorStyles.miniButton)) CollapseChangesPanel();

                GUI.Label(new Rect(CONDITIONS_WINDOW_BORDER, CONDITIONS_WINDOW_BORDER + FIELD_ROW_HEIGHT, width, FIELD_ROW_HEIGHT), selectedDialogName, EditorStyles.miniLabel);
                Rect contentRect = new Rect(CONDITIONS_WINDOW_BORDER, CHANGES_HEADER_HEIGHT, width, Mathf.Max(0, panelRect.height - CHANGES_HEADER_HEIGHT - CHANGES_FOOTER_HEIGHT));
                GUILayout.BeginArea(contentRect);
                _changesScroll = EditorGUILayout.BeginScrollView(_changesScroll);
                EndsMcpJsonED current = null;
                int otherCount = 0;

                foreach (EndsMcpJsonED scope in _pendingChanges.Items)
                {
                    if (scope.Get("key").Text == dialogKey) current = scope;
                    else otherCount++;
                }

                if (!string.IsNullOrWhiteSpace(_changesError)) EditorGUILayout.HelpBox(_changesError, MessageType.Warning);
                if (current != null) DrawChangeScope(current, true, width - CHANGES_SCROLLBAR_WIDTH);
                else EditorGUILayout.HelpBox("This dialog has no unsaved changes.", MessageType.None);

                if (otherCount > 0)
                {
                    EditorGUILayout.Space(CHANGES_CARD_GAP);
                    _showOtherChanges = EditorGUILayout.Foldout(_showOtherChanges, _otherChangesContent, true);

                    if (_showOtherChanges)
                    {
                        foreach (EndsMcpJsonED scope in _pendingChanges.Items)
                        {
                            if (scope == current) continue;

                            DrawChangeScope(scope, false, width - CHANGES_SCROLLBAR_WIDTH);
                        }
                    }
                }

                EditorGUILayout.Space(CHANGES_CARD_GAP);
                _showChangeHistory = EditorGUILayout.Foldout(_showChangeHistory, _changeHistoryContent, true);

                if (_showChangeHistory)
                {
                    bool hasHistory = false;

                    foreach (EndsMcpJsonED record in _changesCollaboration.Get("recentChanges").Items)
                    {
                        foreach (EndsMcpJsonED edit in record.Get("edits").Items)
                        {
                            if (edit.Get("scopeKey")?.Text != dialogKey && edit.Get("scope").Text != selectedDialogName) continue;

                            hasHistory = true;
                            BeginChangesCard();
                            GUILayout.Label(record.Get("actor").Text, EditorStyles.boldLabel);
                            GUILayout.Label(record.Get("operation").Text, EditorStyles.wordWrappedLabel);
                            string time = record.Get("utc").Text;

                            if (DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset timestamp)) time = timestamp.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);

                            GUILayout.Label(time, EditorStyles.miniLabel);
                            EditorGUILayout.EndVertical();
                        }
                    }

                    if (!hasHistory) GUILayout.Label("No recorded activity for this dialog.", EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
                Rect footerRect = new Rect(CONDITIONS_WINDOW_BORDER, Mathf.Max(CHANGES_HEADER_HEIGHT, panelRect.height - CHANGES_FOOTER_HEIGHT), width, CHANGES_FOOTER_HEIGHT - CONDITIONS_WINDOW_BORDER);
                GUILayout.BeginArea(footerRect);
                GUILayout.Label("Selected areas: " + _changesSelection.Count, EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();

                using (new EditorGUI.DisabledScope(_changesSelection.Count == 0 || _changesActionPending))
                {
                    if (GUILayout.Button("Save selected", GUILayout.Height(FIELD_ROW_HEIGHT))) QueueChangesAction(true, session);
                    if (GUILayout.Button("Discard selected", GUILayout.Height(FIELD_ROW_HEIGHT))) QueueChangesAction(false, session);
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
            finally
            {
                GUI.EndGroup();
                GUI.backgroundColor = oldBackground;
                GUI.enabled = wasEnabled;
            }
        }

        private bool IsChangeScopeBlocked(string scopeName)
        {
            if (!EndsSessionED.ExclusiveEditing) return false;

            foreach (EndsMcpJsonED lease in _changesCollaboration.Get("leases").Items)
            {
                if (lease.Get("scope").Text == scopeName && lease.Get("sessionId").Text != EndsSessionED.EDITOR_ACTOR) return true;
            }

            return false;
        }

        private void DrawChangeScope(EndsMcpJsonED scope, bool current, float contentWidth)
        {
            string key = scope.Get("key").Text;
            string name = scope.Get("name").Text;
            bool blocked = IsChangeScopeBlocked(name);
            EditorGUILayout.BeginVertical();

            using (new EditorGUI.DisabledScope(blocked || _changesActionPending))
            {
                bool selected = EditorGUILayout.ToggleLeft(name, _changesSelection.Contains(key), EditorStyles.boldLabel);

                if (selected) _changesSelection.Add(key);
                else _changesSelection.Remove(key);
            }

            string summary = "Nodes: +" + scope.Get("nodesAdded").Text + ", -" + scope.Get("nodesRemoved").Text + ", changed " + scope.Get("nodesChanged").Text;

            if (key == EndsSessionED.SHARED_SCOPE) summary = "Shared catalogs and settings";
            else if (scope.Get("nodesAdded").Text == "0" && scope.Get("nodesRemoved").Text == "0" && scope.Get("nodesChanged").Text == "0") summary = "Dialog settings changed";

            if (scope.Get("status")?.Text == "Added") summary = "Dialog added. " + summary;
            else if (scope.Get("status")?.Text == "Removed") summary = "Dialog removed. " + summary;
            if (scope.Get("internalOnly")?.Bool == true) summary = "No content changes";

            GUILayout.Label(summary, EditorStyles.miniLabel);

            if (scope.Get("internalOnly")?.Bool == true) GUILayout.Label("Only internal bookkeeping differs, for example reserved node ID numbers after adding and removing a node. Save or discard this area to resolve it.", EditorStyles.wordWrappedMiniLabel);

            if (blocked)
            {
                foreach (EndsMcpJsonED lease in _changesCollaboration.Get("leases").Items)
                {
                    if (lease.Get("scope").Text == name) GUILayout.Label("Read only - " + lease.Get("owner").Text, EditorStyles.wordWrappedMiniLabel);
                }
            }

            HashSet<string> contributors = new HashSet<string>(StringComparer.Ordinal);

            foreach (EndsMcpJsonED record in _changesCollaboration.Get("recentChanges").Items)
            {
                foreach (EndsMcpJsonED edit in record.Get("edits").Items)
                {
                    if (edit.Get("scopeKey")?.Text == key || edit.Get("scope").Text == name) contributors.Add(record.Get("actor").Text);
                }
            }

            if (contributors.Count > 0) GUILayout.Label(new GUIContent("Recent contributors: " + contributors.Count, string.Join(", ", contributors) + "\nNames from retained activity history. This does not attribute every unsaved field to an author."), EditorStyles.miniLabel);

            string scopeExpansion = key + "/details";
            bool expanded = current || _expandedChanges.Contains(scopeExpansion);

            if (!current)
            {
                expanded = EditorGUILayout.Foldout(expanded, "Review differences", true);

                if (expanded) _expandedChanges.Add(scopeExpansion);
                else _expandedChanges.Remove(scopeExpansion);
            }

            if (expanded)
            {
                foreach (EndsMcpJsonED group in scope.Get("groups").Items)
                {
                    EndsMcpJsonED sourceTree = group.Get("tree") ?? group.Get("fields");

                    if (!_presentedChanges.TryGetValue(sourceTree, out EndsMcpJsonED presentedTree))
                    {
                        DialogEntry dialog = database.Dialogs?.Find(entry => entry.NodeIdentityKey == key);
                        DialogNode node = dialog?.nodes?.Find(entry => entry.nodeId == group.Get("name").Text);
                        presentedTree = sourceTree;

                        if (group.Get("nodeType") != null) presentedTree = EndsNodeDrawersED.PresentChanges(sourceTree, node);

                        _presentedChanges.Add(sourceTree, presentedTree);
                    }

                    EditorGUILayout.Space(CHANGES_CARD_GAP);
                    BeginChangesCard();
                    string groupKey = key + "/" + group.Get("name").Text;
                    string status = group.Get("status").Text;
                    Color accent = _modifiedChangeColor;

                    if (status == "Added") accent = _addedChangeColor;
                    else if (status == "Removed") accent = _removedChangeColor;

                    string caption = group.Get("name").Text;
                    string nodeType = group.Get("nodeType")?.Text;

                    if (!string.IsNullOrEmpty(nodeType)) caption = nodeType + " " + caption;

                    string collapsedKey = "collapsed:" + groupKey;
                    bool details = EditorGUILayout.BeginFoldoutHeaderGroup(!_expandedChanges.Contains(collapsedKey), new GUIContent(caption, status));

                    if (!string.IsNullOrEmpty(nodeType) && (status == "Added" || status == "Removed"))
                    {
                        Rect headerRect = GUILayoutUtility.GetLastRect();
                        float titleWidth = EditorStyles.foldoutHeader.CalcSize(new GUIContent(caption)).x - EditorStyles.foldoutHeader.padding.right;
                        headerRect.xMin += titleWidth;
                        DrawChangesHeaderStatus(headerRect, status);
                    }

                    if (details) _expandedChanges.Remove(collapsedKey);
                    else _expandedChanges.Add(collapsedKey);

                    if (details)
                    {
                        bool wholeNodeChange = !string.IsNullOrEmpty(nodeType) && (status == "Added" || status == "Removed");
                        if (presentedTree.Items.Count == 0) GUILayout.Label("Only hidden fields changed.", EditorStyles.wordWrappedMiniLabel);
                        else DrawChangesTree(presentedTree, contentWidth - 2 * CHANGES_CARD_PADDING, nodeType, showObjectStatus: !wholeNodeChange);
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                    EditorGUILayout.EndVertical();
                    Rect card = GUILayoutUtility.GetLastRect();

                    if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(new Rect(card.x, card.y, CHANGES_ACCENT_WIDTH, card.height), accent);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(CHANGES_CARD_GAP);
        }

        private void DrawChangesTree(EndsMcpJsonED tree, float contentWidth, string payloadLabel = null, bool contentOnly = false, bool showObjectStatus = true, string headerField = null, string ownerStatus = null)
        {
            _changesFieldLabelStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel) { fontStyle = FontStyle.Bold, margin = new RectOffset(EditorStyles.wordWrappedLabel.margin.left, 0, EditorStyles.wordWrappedLabel.margin.top, EditorStyles.wordWrappedLabel.margin.bottom) };
            _changesFieldCaptionStyle ??= new GUIStyle(EditorStyles.label) { alignment = TextAnchor.UpperLeft, wordWrap = false, margin = new RectOffset(), padding = new RectOffset(_changesFieldLabelStyle.padding.left, 0, 0, 0) };
            _changesFieldValueStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel) { margin = new RectOffset(), padding = new RectOffset() };
            List<EndsMcpJsonED> orderedItems = new List<EndsMcpJsonED>(tree.Items.Count);
            List<EndsMcpJsonED> contentBlocks = new List<EndsMcpJsonED>();

            foreach (EndsMcpJsonED item in tree.Items)
            {
                if (headerField != null && item.Get("path").Text.EndsWith("/" + headerField, StringComparison.Ordinal)) continue;

                EndsMcpJsonED contents = item.Get("contents");
                bool contentBlock = item.Get("container")?.Bool == true || (contents != null && contents.Items.Count > 0);

                if (contentBlock) contentBlocks.Add(item);
                else orderedItems.Add(item);
            }

            orderedItems.AddRange(contentBlocks);
            float labelWidth = 0;

            foreach (EndsMcpJsonED field in orderedItems)
            {
                string fieldStatus = field.Get("status")?.Text;

                if (field.Get("container")?.Bool == true || fieldStatus == "Added" || fieldStatus == "Removed") continue;

                string fieldLabel = field.Get("label")?.Text ?? FormatChangePath(field.Get("path").Text);
                float captionWidth = Mathf.Ceil(_changesFieldCaptionStyle.CalcSize(new GUIContent(fieldLabel + ":")).x);
                labelWidth = Mathf.Max(labelWidth, captionWidth);
            }

            foreach (EndsMcpJsonED item in orderedItems)
            {
                string path = item.Get("path").Text;
                string label = item.Get("displayLabel")?.Text ?? item.Get("label")?.Text ?? FormatChangePath(path);
                string status = item.Get("status")?.Text ?? "Changed";
                bool container = item.Get("container")?.Bool == true;
                bool row = item.Get("row")?.Bool == true;
                EndsMcpJsonED children = item.Get("children");
                bool hasChildren = children != null && children.Items.Count > 0;
                string tooltip = path;

                if (row && item.Get("id")?.Text is string identity) tooltip += "\nID: " + identity;

                if (container && hasChildren && (label == payloadLabel || item.Get("collection")?.Bool == true))
                {
                    DrawChangesTree(children, contentWidth, contentOnly: contentOnly, showObjectStatus: showObjectStatus, ownerStatus: ownerStatus);

                    continue;
                }

                if (item.Get("meaningChanged")?.Bool == true) tooltip += "\nThe source changed how this value is interpreted. This does not imply a separate write to the field.";

                bool objectChange = status == "Added" || status == "Removed";
                bool changedRow = container && row && !contentOnly;
                bool card = container || hasChildren || objectChange;
                float innerWidth = contentWidth;
                string headerStatus = status;

                if (contentOnly && ownerStatus != null) headerStatus = ownerStatus;

                if (card)
                {
                    BeginChangesCard();
                    innerWidth = Mathf.Max(1, contentWidth - 2 * CHANGES_CARD_PADDING);
                }

                if (container) DrawChangesHeader(new GUIContent(label, tooltip), row, headerStatus);
                else
                {
                    string before = item.Get("beforeText")?.Text ?? FormatChangeValue(item.Get("before"), path);
                    string after = item.Get("afterText")?.Text ?? FormatChangeValue(item.Get("after"), path);
                    string value = item.Get("valueText")?.Text;

                    if (!contentOnly) value = before + " → " + after;

                    if (objectChange)
                    {
                        if (row || label != "Content") DrawChangesHeader(new GUIContent(label, tooltip), row, headerStatus);

                        if (status == "Added") value = after;
                        else value = before;

                        EndsMcpJsonED contents = item.Get("contents");

                        if (contents != null && contents.Items.Count > 0) DrawChangesTree(contents, innerWidth, payloadLabel, contentOnly: true, showObjectStatus: showObjectStatus, headerField: item.Get("headerField")?.Text, ownerStatus: status);
                        else DrawChangesValue(value, tooltip);
                    }
                    else DrawChangesField(new GUIContent(label + ":", tooltip), value, labelWidth, innerWidth);
                }

                if (hasChildren)
                {
                    if (container) DrawChangesTree(children, innerWidth, contentOnly: contentOnly, showObjectStatus: showObjectStatus, headerField: item.Get("headerField")?.Text, ownerStatus: ownerStatus);
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(CHANGES_TREE_INDENT);
                        EditorGUILayout.BeginVertical();
                        DrawChangesTree(children, Mathf.Max(1, innerWidth - CHANGES_TREE_INDENT), contentOnly: contentOnly, showObjectStatus: showObjectStatus, ownerStatus: ownerStatus);
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.EndHorizontal();
                    }
                }

                if (card)
                {
                    EditorGUILayout.EndVertical();
                    Rect objectCard = GUILayoutUtility.GetLastRect();

                    if (showObjectStatus && (objectChange || changedRow) && Event.current.type == EventType.Repaint)
                    {
                        Color accent = _modifiedChangeColor;

                        if (status == "Added") accent = _addedChangeColor;
                        else if (status == "Removed") accent = _removedChangeColor;

                        EditorGUI.DrawRect(new Rect(objectCard.x, objectCard.y, CHANGES_ACCENT_WIDTH, objectCard.height), accent);
                    }

                    GUILayout.Space(CHANGES_CARD_GAP);
                }
                else GUILayout.Space(CHANGES_ROW_GAP);
            }
        }

        private void DrawChangesHeader(GUIContent caption, bool showStatus, string status)
        {
            if (!showStatus)
            {
                GUILayout.Label(caption, _changesFieldLabelStyle);

                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(caption, _changesFieldLabelStyle, GUILayout.ExpandWidth(false));
            GUIContent suffix = new GUIContent("(" + status.ToLowerInvariant() + ")");
            GUIStyle statusStyle = GetChangesHeaderStatusStyle();
            Rect suffixRect = GUILayoutUtility.GetRect(suffix, statusStyle, GUILayout.Width(statusStyle.CalcSize(suffix).x));
            DrawChangesHeaderStatus(suffixRect, status);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawChangesHeaderStatus(Rect rect, string status)
        {
            Color oldContent = GUI.contentColor;
            Color accent = _modifiedChangeColor;

            if (status == "Added") accent = _addedChangeColor;
            else if (status == "Removed") accent = _removedChangeColor;

            GUI.contentColor = oldContent * accent;
            GUI.Label(rect, "(" + status.ToLowerInvariant() + ")", GetChangesHeaderStatusStyle());
            GUI.contentColor = oldContent;
        }

        private GUIStyle GetChangesHeaderStatusStyle()
        {
            if (_changesHeaderStatusStyle != null) return _changesHeaderStatusStyle;

            GUIStyle source = EditorStyles.wordWrappedLabel;
            _changesHeaderStatusStyle = new GUIStyle(source) { fontStyle = FontStyle.Normal, alignment = TextAnchor.UpperLeft, wordWrap = false, margin = new RectOffset(0, 0, source.margin.top, source.margin.bottom) };
            _changesHeaderStatusStyle.normal.textColor = Color.white;

            return _changesHeaderStatusStyle;
        }

        private void DrawChangesField(GUIContent caption, string value, float labelWidth, float contentWidth)
        {
            bool stacked = labelWidth + CHANGES_FIELD_GAP + CHANGES_FIELD_MIN_VALUE_WIDTH > contentWidth;

            if (!stacked) EditorGUILayout.BeginHorizontal();

            if (stacked) GUILayout.Label(caption, _changesFieldCaptionStyle);
            else
            {
                GUILayout.Label(caption, _changesFieldCaptionStyle, GUILayout.Width(labelWidth), GUILayout.Height(FIELD_ROW_HEIGHT));
                GUILayout.Space(CHANGES_FIELD_GAP);
            }

            DrawChangesValue(value, caption.tooltip);

            if (!stacked) EditorGUILayout.EndHorizontal();
        }

        private void DrawChangesValue(string value, string tooltip)
        {
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(value), _changesFieldValueStyle, GUILayout.ExpandWidth(true), GUILayout.MinWidth(1));
            EditorGUI.SelectableLabel(rect, value, _changesFieldValueStyle);
            GUI.Label(rect, new GUIContent("", tooltip));
        }

        private static void BeginChangesCard()
        {
            _changesCardStyle ??= new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(CHANGES_CARD_PADDING, CHANGES_CARD_PADDING, 3, 3) };
            Rect card = EditorGUILayout.BeginVertical(_changesCardStyle);

            if (Event.current.type != EventType.Repaint) return;

            card.xMin += CHANGES_CARD_BORDER;
            card.xMax -= CHANGES_CARD_BORDER;
            card.yMin += CHANGES_CARD_BORDER;
            card.yMax -= CHANGES_CARD_BORDER;
            Color background = _changesCardLightColor;

            if (EditorGUIUtility.isProSkin) background = _changesCardDarkColor;

            EditorGUI.DrawRect(card, background);
        }

        private void CollapseChangesPanel()
        {
            _showChanges = false;
            _changesPointerActive = false;
            _changesKeyboardFocus = false;
            GUI.FocusControl(null);
            Repaint();
        }

        private static string FormatChangePath(string path)
        {
            List<string> parts = new List<string>();

            foreach (string encoded in path.Split('/'))
            {
                string part = encoded.Replace("~1", "/").Replace("~0", "~");
                string label = part.TrimStart('_');

                if (string.IsNullOrEmpty(part)) continue;
                if (part == "$order") label = "Order";
                else if (part == "$nodeOrder") label = "Node order";
                else if (part == "$dialogOrder") label = "Dialog order";
                else if (int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int index)) label = "#" + (index + 1);
                else label = Regex.Replace(label, "([a-z])([A-Z])", "$1 $2");

                parts.Add(label);
            }

            return string.Join(" / ", parts);
        }

        private static string FormatChangeValue(EndsMcpJsonED value, string path)
        {
            if (value == null || value.Type == EndsMcpJsonED.Kind.Null) return "Not set";

            string leaf = path.Substring(path.LastIndexOf('/') + 1);
            Type enumType = null;

            if (leaf == "VariableType" || leaf == "variableType") enumType = typeof(ConditionVariableType);

            if (enumType != null && int.TryParse(value.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int enumValue)) return Enum.GetName(enumType, enumValue) ?? value.Text;

            if (value.Type == EndsMcpJsonED.Kind.Bool)
            {
                if (leaf == "isAndLogic" || leaf == "IsAndLogic")
                {
                    if (value.Bool) return "All conditions (AND)";

                    return "Any condition (OR)";
                }

                if (value.Bool) return "Yes";

                return "No";
            }

            if (value.Type == EndsMcpJsonED.Kind.String && string.IsNullOrEmpty(value.Text))
            {
                if (leaf.EndsWith("NodeId", StringComparison.Ordinal)) return "Not connected";
                if (leaf == "VariableName" || leaf == "CharacterName" || leaf == "EmotionName" || leaf == "TargetDialogName" || leaf == "TargetCategory") return "Not selected";

                return "Empty text";
            }

            if (value.Type == EndsMcpJsonED.Kind.String) return value.Text;
            if (value.Type == EndsMcpJsonED.Kind.Number)
            {
                if ((leaf == "x" || leaf == "y") && path.Contains("/Position/") && float.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float coordinate)) return coordinate.ToString("0.###", CultureInfo.InvariantCulture);
                if (value.Text.IndexOf('.') >= 0 && float.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)) return number.ToString("G7", CultureInfo.InvariantCulture);

                return value.Text;
            }

            if (value.Type == EndsMcpJsonED.Kind.Array)
            {
                if (value.Items.Count == 0) return "No entries";

                List<string> entries = new List<string>();

                foreach (EndsMcpJsonED item in value.Items)
                {
                    entries.Add(FormatChangeValue(item, path));
                }

                return string.Join("\n", entries);
            }

            if (value.Type == EndsMcpJsonED.Kind.Object)
            {
                if (value.Get("x") != null && value.Get("y") != null) return FormatChangeValue(value.Get("x"), path + "/x") + "/" + FormatChangeValue(value.Get("y"), path + "/y");

                if (value.Get("instanceID") != null)
                {
                    if (!EndsUnityObjectReferenceED.TryResolveTransientId(value.Get("instanceID"), out UnityEngine.Object asset)) return "Not assigned";

                    if (asset == null) return "Missing asset (" + value.Get("instanceID").Text + ")";

                    return asset.name + " (" + AssetDatabase.GetAssetPath(asset) + ")";
                }

                if (value.Get("r") != null && value.Get("g") != null && value.Get("b") != null)
                {
                    Color color = new Color(ReadChangeColorComponent(value, "r"), ReadChangeColorComponent(value, "g"), ReadChangeColorComponent(value, "b"), ReadChangeColorComponent(value, "a", 1));

                    return "#" + ColorUtility.ToHtmlStringRGBA(color);
                }

                List<string> entries = new List<string>();

                foreach (string key in value.Keys)
                {
                    EndsMcpJsonED item = value.Get(key);
                    bool reference = key == "VariableName" || key == "CharacterName" || key == "EmotionName" || key == "TargetDialogName" || key.EndsWith("NodeId", StringComparison.Ordinal);

                    if (item.Type == EndsMcpJsonED.Kind.Null || item.Type == EndsMcpJsonED.Kind.String && string.IsNullOrEmpty(item.Text) && !reference || item.Type == EndsMcpJsonED.Kind.Array && item.Items.Count == 0) continue;
                    if (key.EndsWith("ReferenceState", StringComparison.Ordinal) && item.Text == "0") continue;

                    string label = FormatChangePath(path + "/" + key);
                    int separator = label.LastIndexOf(" / ", StringComparison.Ordinal);

                    if (separator >= 0) label = label.Substring(separator + 3);

                    entries.Add(label + ": " + FormatChangeValue(item, path + "/" + key));
                }

                return string.Join("\n", entries);
            }

            return value.Write();
        }

        private static float ReadChangeColorComponent(EndsMcpJsonED value, string key, float fallback = 0)
        {
            if (float.TryParse(value.Get(key)?.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float component)) return component;

            return fallback;
        }

        private void QueueChangesAction(bool save, EndsSessionED session)
        {
            HashSet<string> scopes = new HashSet<string>(_changesSelection, StringComparer.Ordinal);
            int revision = _changesRevision;
            DialogDatabase targetDatabase = database;
            _changesActionPending = true;
            _changesError = null;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (this == null || database != targetDatabase) return;

                    if (!save && !EditorUtility.DisplayDialog("Discard selected changes?", "Restore the saved version of " + scopes.Count + " selected areas? Other working changes remain pending.", "Discard", "Cancel")) return;

                    EndsSessionED.Result result;

                    if (save) result = session.SaveScopes(scopes, revision);
                    else result = session.DiscardScopes(scopes, revision);

                    if (!result.Success)
                    {
                        _changesError = result.Error;
                        _changesScroll = Vector2.zero;
                    }

                    _changesRevision = -1;
                }
                catch (InvalidOperationException exception)
                {
                    _changesError = exception.Message;
                    _changesScroll = Vector2.zero;
                }
                finally
                {
                    _changesActionPending = false;

                    if (this != null) Repaint();
                }
            };
        }

        #endregion CHANGES PANEL
    }
}
#endif