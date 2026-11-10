#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public partial class DialogEditorWindowED
    {
        #region FIELDS

        private const double GRAPH_FPS_SAMPLE_SECONDS = 0.5;
        private const int GRAPH_FPS_LABEL_WIDTH = 100;
        private const int GRAPH_FPS_LABEL_GAP = 8;

        private readonly Dictionary<DialogNode, int> _graphNodeHeights = new Dictionary<DialogNode, int>();
        private readonly Dictionary<ChoiceNode, int> _graphChoiceHeights = new Dictionary<ChoiceNode, int>();
        private readonly HashSet<DialogNode> _graphLayoutNodes = new HashSet<DialogNode>();
        private Label _graphFpsLabel;
        private double _graphFpsSampleStart;
        private int _graphFpsRepaints;
        private bool _graphFpsLabelRepaint;

        #endregion FIELDS

        #region GRAPH VIEWPORT

        private Rect GetGraphNodeRect(DialogNode node)
        {
            Vector2 position = (node.Position - editorOffset) * zoomScale;
            return new Rect(position.x, position.y, GetScaledNodeWidth(node), Mathf.Max(GetNodeHeight(node), MIN_NODE_HEIGHT) * zoomScale);
        }

        #endregion GRAPH VIEWPORT

        #region TEMPORARY GRAPH PERFORMANCE

        private void CreateGUI()
        {
            EndsWelcomeED.RecordEditorOpening(this);
            var graph = new IMGUIContainer(DrawEditorGUI);
            graph.style.flexGrow = 1;
            rootVisualElement.Add(graph);
        }

        private void UpdateGraphPerformance()
        {
            if (_graphFpsLabel == null) return;

            bool visible = isEditingDialog && currentMainTab == 0 && dialogSubTab == 0;
            DisplayStyle display = DisplayStyle.None;

            if (visible) display = DisplayStyle.Flex;
            if (_graphFpsLabel.style.display.value != display) _graphFpsLabel.style.display = display;

            if (!visible)
            {
                _graphFpsSampleStart = 0;
                _graphFpsRepaints = 0;
                _graphFpsLabel.text = "FPS: 0";

                return;
            }

            double now = EditorApplication.timeSinceStartup;

            if (_graphFpsSampleStart == 0) _graphFpsSampleStart = now;

            double elapsed = now - _graphFpsSampleStart;

            if (elapsed < GRAPH_FPS_SAMPLE_SECONDS) return;

            string text = "FPS: " + (_graphFpsRepaints / elapsed).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            if (_graphFpsRepaints == 0) text = "FPS: 0";
            if (_graphFpsLabel.text != text)
            {
                _graphFpsLabelRepaint = true;
                _graphFpsLabel.text = text;
            }

            _graphFpsSampleStart = now;
            _graphFpsRepaints = 0;
        }

        private void DrawGraphPerformance(Rect scaleLabelRect, GUIStyle labelStyle)
        {
            if (_graphFpsLabel == null)
            {
                _graphFpsLabel = new Label("FPS: 0") { pickingMode = PickingMode.Ignore, tooltip = "Temporary diagnostic, actual graph repaints per second. The counter never requests graph repaints." };
                _graphFpsLabel.style.position = Position.Absolute;
                _graphFpsLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                _graphFpsLabel.style.fontSize = labelStyle.fontSize;
                _graphFpsLabel.style.color = labelStyle.normal.textColor;
                _graphFpsLabel.style.paddingLeft = 0;
                _graphFpsLabel.style.paddingRight = 0;
                rootVisualElement.Add(_graphFpsLabel);
            }

            if (Event.current.rawType != EventType.Layout && Event.current.rawType != EventType.Repaint) _graphFpsLabelRepaint = false;
            if (Event.current.type != EventType.Repaint) return;
            if (!_graphFpsLabelRepaint) _graphFpsRepaints++;

            _graphFpsLabelRepaint = false;
            Vector2 location = new Vector2(scaleLabelRect.x - GRAPH_FPS_LABEL_WIDTH - GRAPH_FPS_LABEL_GAP, scaleLabelRect.y);
            _graphFpsLabel.style.left = location.x;
            _graphFpsLabel.style.top = location.y;
            _graphFpsLabel.style.width = GRAPH_FPS_LABEL_WIDTH;
            _graphFpsLabel.style.height = scaleLabelRect.height;
        }

        #endregion TEMPORARY GRAPH PERFORMANCE
    }
}
#endif