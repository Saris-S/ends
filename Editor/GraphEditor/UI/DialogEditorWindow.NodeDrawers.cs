#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public partial class DialogEditorWindowED
    {
        #region NODE DRAWER COMPOSITION

        private EndsNodeDrawersED.ControlAddress<DialogEntry> DialogControls => EndsNodeDrawersED.Root<DialogEntry>(selectedDialog.dialogName);

        private EndsNodeDrawersED.ControlAddress<DialogNode> NodeControls(DialogNode node) => DialogControls.Item(DialogSchema.Nodes, node, 0);

        private EndsNodeDrawersED.ControlAddress<ChoiceNode> OptionControls(DialogNode node, int index) => NodeControls(node).Item(NodeSchema.Options, node.ChoiceData[index], index);


        private readonly NodeDrawContext _nodeLayoutContext = new NodeDrawContext();
        private readonly NodeDrawContext _nodeRenderContext = new NodeDrawContext();
        private EndsNodeDrawersED.Registry<DialogNode, NodeDrawContext> _nodePayloadDrawers;
        private EndsNodeDrawersED.Registry<LineNode, NodeDrawContext> _lineBlockDrawers;
        private EndsNodeDrawersED.Registry<ChoiceNode, NodeDrawContext> _choiceOptionDrawers;

        private sealed class NodeDrawContext
        {
            public DialogEditorWindowED Window;
            public IReadOnlyList<string> Languages;
            public bool VoiceClipsEnabled;
            public NodeTextMetrics TextMetrics;
            public int WindowId;
            public DialogNode Node;
            public string NodeId;
            public GraphStyles Styles;
            public Event CurrentEvent;
            public bool BlockInteraction;
            public float NodeWidth;
            public float ContentWidth;
            public float CursorY;
            public int ChoiceIndex;
            public List<int> ChoicesToRemove;
        }

        private void EnsureNodeDrawers()
        {
            if (_nodePayloadDrawers != null) return;

            _lineBlockDrawers = new EndsNodeDrawersED.Registry<LineNode, NodeDrawContext>();
            _lineBlockDrawers.Register(LineNodeSchema.Animations, new EndsNodeDrawersED.Drawer<LineNode, NodeDrawContext>(MeasureLineAnimations, DrawLineAnimations));
            _lineBlockDrawers.Register(LineNodeSchema.NameColor, new EndsNodeDrawersED.Drawer<LineNode, NodeDrawContext>(line => 0, DrawLineSpeaker));
            _lineBlockDrawers.Register(LineNodeSchema.Translations, new EndsNodeDrawersED.Drawer<LineNode, NodeDrawContext>(MeasureLineTranslations, DrawLineTranslations));

            _choiceOptionDrawers = new EndsNodeDrawersED.Registry<ChoiceNode, NodeDrawContext>();
            _choiceOptionDrawers.Register(ChoiceOptionSchema, new EndsNodeDrawersED.Drawer<ChoiceNode, NodeDrawContext>(MeasureChoiceOption, DrawChoiceOption));
            _choiceOptionDrawers.Register(ChoiceOptionSchema.Conditions, new EndsNodeDrawersED.Drawer<ChoiceNode, NodeDrawContext>(MeasureChoiceConditions, DrawChoiceConditions));
            _choiceOptionDrawers.Register(ChoiceOptionSchema.Consequences, new EndsNodeDrawersED.Drawer<ChoiceNode, NodeDrawContext>(MeasureChoiceConsequences, DrawChoiceConsequences));
            _choiceOptionDrawers.Register(ChoiceOptionSchema.Translations, new EndsNodeDrawersED.Drawer<ChoiceNode, NodeDrawContext>(MeasureChoiceTranslations, DrawChoiceTranslations));

            _nodePayloadDrawers = new EndsNodeDrawersED.Registry<DialogNode, NodeDrawContext>();
            _nodePayloadDrawers.Register(NodeSchema.Start, new EndsNodeDrawersED.Drawer<DialogNode, NodeDrawContext>(node => START_NODE_HEIGHT, (bounds, node, context) => { }));
            _nodePayloadDrawers.Register(NodeSchema.Line, new EndsNodeDrawersED.Drawer<DialogNode, NodeDrawContext>(MeasureLinePayload, DrawLinePayload));
            _nodePayloadDrawers.Register(NodeSchema.Condition, new EndsNodeDrawersED.Drawer<DialogNode, NodeDrawContext>(MeasureConditionPayload, DrawConditionPayload));
            _nodePayloadDrawers.Register(NodeSchema.Options, new EndsNodeDrawersED.Drawer<DialogNode, NodeDrawContext>(MeasureChoicePayload, DrawChoicePayload));
            _nodePayloadDrawers.Register(NodeSchema.Redirect, new EndsNodeDrawersED.Drawer<DialogNode, NodeDrawContext>(node => GetRedirectNodeContentHeight() + NODE_TOP_PADDING + NODE_BOTTOM_PADDING, DrawRedirectPayload));
        }

        private EndsNodeDrawersED.Drawer<DialogNode, NodeDrawContext> FindNodePayloadDrawer(DialogNode node)
        {
            for (int i = 0; i < NodeSchema.Members.Count; i++)
            {
                NodeElementDefinition<DialogNode> member = NodeSchema.Members[i];

                if (member.IsVisible(node) && _nodePayloadDrawers.TryGet(member, out EndsNodeDrawersED.Drawer<DialogNode, NodeDrawContext> drawer)) return drawer;
            }

            return null;
        }

        private int GetNodeHeight(DialogNode node)
        {
            if (_graphNodeHeights.TryGetValue(node, out int height)) return height;

            EnsureNodeDrawers();
            EndsNodeDrawersED.Drawer<DialogNode, NodeDrawContext> drawer = FindNodePayloadDrawer(node);

            if (drawer == null) return LINE_NODE_BASE_HEIGHT;

            _nodeLayoutContext.Window = this;
            _nodeLayoutContext.Languages = availableLanguages;
            _nodeLayoutContext.VoiceClipsEnabled = database != null && database.VoiceClipsEnabled;
            _nodeLayoutContext.TextMetrics = GetNodeTextMetrics();
            _nodeLayoutContext.Node = node;
            _nodeLayoutContext.NodeId = node.nodeId;

            height = Mathf.RoundToInt(drawer.Measure(node, _nodeLayoutContext));
            _graphNodeHeights[node] = height;
            return height;
        }

        private int GetChoiceHeight(ChoiceNode choice, string nodeId)
        {
            if (_graphChoiceHeights.TryGetValue(choice, out int height)) return height;

            EnsureNodeDrawers();

            _nodeLayoutContext.Window = this;
            _nodeLayoutContext.Languages = availableLanguages;
            _nodeLayoutContext.TextMetrics = GetNodeTextMetrics();
            _nodeLayoutContext.Node = null;
            _nodeLayoutContext.NodeId = nodeId;

            height = Mathf.RoundToInt(_choiceOptionDrawers.Get(ChoiceOptionSchema).Measure(choice, _nodeLayoutContext));
            _graphChoiceHeights[choice] = height;
            return height;
        }

        private float GetNodeTextAreaHeight(string text) => GetNodeTextMetrics().Height(text);

        private NodeTextMetrics GetNodeTextMetrics()
        {
            if (Event.current != null) return GetGraphStyles(zoomScale).TextMetrics;

            GraphStyles styles = _graphStyles;

            if (zoomScale == DEFAULT_ZOOM_SCALE) styles = _unitGraphStyles;
            if (styles != null && styles.Zoom == zoomScale) return styles.TextMetrics;

            return NodeTextMetrics.FromInspectorSkin(zoomScale);
        }

        private sealed class NodeTextMetrics
        {
            private const int MAX_CACHED_TEXTS = 512;

            private readonly GUIStyle _style;
            private readonly float _width;
            private readonly float _zoom;
            private readonly float _singleLineHeight;
            private readonly float _lineHeight;
            private readonly GUIContent _content = new GUIContent();
            private readonly Dictionary<string, float> _heights = new Dictionary<string, float>(StringComparer.Ordinal);

            public NodeTextMetrics(GUIStyle style, float width, float zoom)
            {
                _style = style;
                _width = width;
                _zoom = zoom;
                _singleLineHeight = style.CalcHeight(new GUIContent("M"), width);
                _lineHeight = style.CalcHeight(new GUIContent("M\nM"), width) - _singleLineHeight;
            }

            public static NodeTextMetrics FromInspectorSkin(float zoom)
            {
                GUISkin skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
                int controlPadding = (int)(GRAPH_CONTROL_PADDING * zoom);
                int textPadding = (int)(GRAPH_TEXT_PADDING * zoom);
                var textArea = new GUIStyle(skin.textArea)
                {
                    font = skin.textArea.font ?? skin.font,
                    fontSize = (int)(BASE_FONT_SIZE * zoom),
                    padding = new RectOffset(textPadding, textPadding, controlPadding, controlPadding)
                };
                float width = BASE_NODE_WIDTH * zoom - Mathf.Max(NODE_LEFT_RIGHT_PADDING * zoom, skin.window.overflow.left) - Mathf.Max(NODE_LEFT_RIGHT_PADDING * zoom, skin.window.overflow.right);
                return new NodeTextMetrics(textArea, width, zoom);
            }

            public float Height(string text)
            {
                if (string.IsNullOrEmpty(text) || _lineHeight <= 0) return LINE_TEXT_AREA_HEIGHT;
                if (_heights.TryGetValue(text, out float height)) return height;

                _content.text = text;
                float textHeight = _style.CalcHeight(_content, _width);
                int lineCount = 1 + Mathf.RoundToInt((textHeight - _singleLineHeight) / _lineHeight);
                height = LINE_TEXT_AREA_HEIGHT + Mathf.Max(0, lineCount - NODE_TEXT_MIN_LINES) * _lineHeight / _zoom;

                if (_heights.Count >= MAX_CACHED_TEXTS) _heights.Clear();

                _heights.Add(text, height);
                return height;
            }
        }

        /// <summary>Measures a graph node in graph units with every content foldout expanded.</summary>
        internal sealed class ExpandedNodeGeometryED
        {
            private readonly NodeDrawContext _context;

            internal ExpandedNodeGeometryED(DialogDatabase database)
            {
                _context = new NodeDrawContext
                {
                    Languages = database.AvailableLanguages ?? new List<string>(),
                    VoiceClipsEnabled = database.VoiceClipsEnabled,
                    TextMetrics = NodeTextMetrics.FromInspectorSkin(DEFAULT_ZOOM_SCALE)
                };
            }

            internal Rect Bounds(DialogNode node)
            {
                float width = BASE_NODE_WIDTH;
                float height = LINE_NODE_BASE_HEIGHT;

                switch (node.NodeType)
                {
                    case NodeType.Start:
                        width = START_NODE_WIDTH;
                        height = START_NODE_HEIGHT;
                        break;
                    case NodeType.Line:
                        height = MeasureLinePayload(node, _context);
                        break;
                    case NodeType.Choice:
                        height = MeasureChoicePayload(node, _context);
                        break;
                    case NodeType.Condition:
                        height = MeasureConditionPayload(node, _context);
                        break;
                    case NodeType.Redirect:
                        height = GetRedirectNodeContentHeight() + NODE_TOP_PADDING + NODE_BOTTOM_PADDING;
                        break;
                }

                return new Rect(node.Position, new Vector2(width, Mathf.Max(Mathf.RoundToInt(height), MIN_NODE_HEIGHT)));
            }

            internal static Vector2 InputOffset(DialogNode node) => GetNodeInputOffset(node.NodeType);

            internal Vector2 OutputOffset(EndsNodeReferencesED.Reference edge, float nodeHeight)
            {
                float optionY = CHOICE_NODE_EXIT_SPACING;

                if (edge.PortKind == NodePortKind.Choice)
                {
                    for (int i = 0; i < edge.PortIndex; i++)
                    {
                        ChoiceNode choice = edge.Node.ChoiceData[i];

                        if (choice != null) optionY += Mathf.RoundToInt(MeasureChoiceOption(choice, _context));
                    }
                }

                return GetNodeOutputOffset(edge, nodeHeight, optionY);
            }
        }

        #endregion NODE DRAWER COMPOSITION

    }
}
#endif