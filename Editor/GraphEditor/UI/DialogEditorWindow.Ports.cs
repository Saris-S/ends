#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public partial class DialogEditorWindowED
    {
        #region FIELDS

        [NonSerialized] private DialogEntry _graphPortsDialog;
        [NonSerialized] private bool _graphPortsDirty = true;
        private readonly Dictionary<DialogNode, List<EndsNodeReferencesED.Reference>> _graphNodeOutputs = new Dictionary<DialogNode, List<EndsNodeReferencesED.Reference>>();
        private readonly Dictionary<string, DialogNode> _graphPortTargets = new Dictionary<string, DialogNode>(StringComparer.Ordinal);
        private readonly HashSet<string> _graphConnectedInputs = new HashSet<string>(StringComparer.Ordinal);

        #endregion FIELDS

        #region PORT DISCOVERY

        private void ResetGraphPorts()
        {
            _graphPortsDialog = null;
            _graphPortsDirty = true;
            _graphNodeOutputs.Clear();
            _graphPortTargets.Clear();
            _graphConnectedInputs.Clear();
        }

        private void EnsureGraphPorts()
        {
            DialogEntry dialog = selectedDialog;

            if (ReferenceEquals(_graphPortsDialog, dialog) && !_graphPortsDirty) return;

            ResetGraphPorts();
            _graphPortsDialog = dialog;
            _graphPortsDirty = false;

            if (dialog?.nodes == null) return;

            foreach (DialogNode node in dialog.nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.nodeId) || _graphPortTargets.ContainsKey(node.nodeId)) continue;

                _graphPortTargets.Add(node.nodeId, node);
            }

            EndsNodeReferencesED.Visit(DialogSchema, dialog, output =>
            {
                if (!_graphNodeOutputs.TryGetValue(output.Node, out List<EndsNodeReferencesED.Reference> outputs))
                {
                    outputs = new List<EndsNodeReferencesED.Reference>();
                    _graphNodeOutputs.Add(output.Node, outputs);
                }

                outputs.Add(output);

                if (!string.IsNullOrEmpty(output.Value)) _graphConnectedInputs.Add(output.Value);
            }, kind: NodeReferenceKind.Node);
        }

        private List<EndsNodeReferencesED.Reference> ReadGraphOutputs(DialogNode node)
        {
            EnsureGraphPorts();
            _graphNodeOutputs.TryGetValue(node, out List<EndsNodeReferencesED.Reference> outputs);

            return outputs;
        }

        private string GetCurrentConnection(DialogNode node, int index)
        {
            List<EndsNodeReferencesED.Reference> outputs = ReadGraphOutputs(node);

            if (outputs == null) return null;

            foreach (EndsNodeReferencesED.Reference output in outputs)
            {
                if (output.PortIndex == index) return output.Value;
            }

            return null;
        }

        private bool IsAlreadyConnected(DialogNode source, string targetId, int index) => GetCurrentConnection(source, index) == targetId;

        private bool IsNodeConnected(string nodeId)
        {
            EnsureGraphPorts();

            return !string.IsNullOrEmpty(nodeId) && _graphConnectedInputs.Contains(nodeId);
        }

        #endregion PORT DISCOVERY

        #region PORT GEOMETRY AND DRAWING

        private Rect GetGraphPortRect(Vector2 position) => new Rect(position.x - PORT_HALF_SIZE * zoomScale, position.y - PORT_HALF_SIZE * zoomScale, NODE_PORT_SIZE * zoomScale, NODE_PORT_SIZE * zoomScale);

        private Vector2 GetGraphOutputPosition(EndsNodeReferencesED.Reference output, ref float optionY)
        {
            float nodeHeight = 0;

            if (output.PortKind == NodePortKind.Next) nodeHeight = Mathf.Max(GetNodeHeight(output.Node), MIN_NODE_HEIGHT);

            Vector2 offset = GetNodeOutputOffset(output, nodeHeight, optionY);

            if (output.PortKind == NodePortKind.Choice) optionY += GetChoiceHeight((ChoiceNode)output.Model, output.Node.nodeId);

            return (output.Node.Position - editorOffset + offset) * zoomScale;
        }

        private static Vector2 GetNodeOutputOffset(EndsNodeReferencesED.Reference output, float nodeHeight, float optionY)
        {
            switch (output.PortKind)
            {
                case NodePortKind.Next:
                    if (output.Node.NodeType == NodeType.Start) return new Vector2(INPUT_OFFSET_START, nodeHeight);

                    return new Vector2(INPUT_OFFSET_LINE, nodeHeight);
                case NodePortKind.True:
                    return new Vector2(BASE_NODE_WIDTH, CONDITION_NODE_EXIT_SPACING);
                case NodePortKind.False:
                    return new Vector2(BASE_NODE_WIDTH, CONDITION_NODE_EXIT_SPACING + NODE_SPACE_OFFSET + NODE_PORT_SIZE);
                case NodePortKind.Choice:
                    return new Vector2(BASE_NODE_WIDTH, optionY);
                default:
                    throw new InvalidOperationException("No graph geometry is registered for output " + output.Port + ".");
            }
        }

        private void DrawGraphConnections(Rect viewport)
        {
            if (Event.current.type != EventType.Repaint && !Event.current.alt) return;

            EnsureGraphPorts();

            foreach (KeyValuePair<DialogNode, List<EndsNodeReferencesED.Reference>> group in _graphNodeOutputs)
            {
                float optionY = CHOICE_NODE_EXIT_SPACING;

                foreach (EndsNodeReferencesED.Reference output in group.Value)
                {
                    Vector2 position = GetGraphOutputPosition(output, ref optionY);

                    if (string.IsNullOrEmpty(output.Value) || !_graphPortTargets.TryGetValue(output.Value, out DialogNode target)) continue;

                    Vector2 end = GetNodeInputPosition(target);
                    float minX = Mathf.Min(position.x, end.x) - BEZIER_THICKNESS;
                    float maxX = Mathf.Max(position.x, end.x) + BEZIER_THICKNESS;
                    float minY = Mathf.Min(position.y, end.y - BEZIER_OFFSET) - BEZIER_THICKNESS;
                    float maxY = Mathf.Max(position.y + BEZIER_OFFSET, end.y) + BEZIER_THICKNESS;

                    if (Rect.MinMaxRect(minX, minY, maxX, maxY).Overlaps(viewport)) DrawNodeConnection(position, end, EndsAiHighlightED.StructureKey(selectedDialog.dialogName, output.Address));
                }
            }
        }

        private void DrawGraphOutputPorts(DialogNode node)
        {
            List<EndsNodeReferencesED.Reference> outputs = ReadGraphOutputs(node);

            if (outputs == null) return;

            float optionY = CHOICE_NODE_EXIT_SPACING;

            foreach (EndsNodeReferencesED.Reference output in outputs)
            {
                Rect rect = GetGraphPortRect(GetGraphOutputPosition(output, ref optionY));
                bool selected = !string.IsNullOrEmpty(output.Value) || selectedPorts.Exists(port => port.Node == node && !port.IsInput && port.ChoiceIndex == output.PortIndex) || (pendingPort != null && pendingPort.Node == node && !pendingPort.IsInput && pendingPort.ChoiceIndex == output.PortIndex);
                Texture2D texture = nodeInactiveOutputTexture;

                if (selected) texture = nodeActiveOutputTexture;
                if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
            }
        }

        #endregion PORT GEOMETRY AND DRAWING

        #region PORT INPUT

        private void HandleGraphOutputPortInput(DialogNode node, Vector2 mouse, ref bool handled)
        {
            Event input = Event.current;

            if (input.type != EventType.MouseDown || input.button != 0) return;

            List<EndsNodeReferencesED.Reference> outputs = ReadGraphOutputs(node);

            if (outputs == null) return;

            float optionY = CHOICE_NODE_EXIT_SPACING;

            foreach (EndsNodeReferencesED.Reference output in outputs)
            {
                if (!GetGraphPortRect(GetGraphOutputPosition(output, ref optionY)).Contains(mouse)) continue;

                handled = true;

                if (TryDeselectSamePortClick(node, false, output.PortIndex)) GUI.changed = true;
                else if (!TryCompleteConnectionByClick(node, false, output.PortIndex))
                {
                    if (!string.IsNullOrEmpty(output.Value))
                    {
                        RecordEditorIntent("Disconnect Output Port");
                        DisconnectPort(node, output.PortIndex);
                        selectedPorts.Clear();
                        pendingPort = null;
                    }
                    else
                    {
                        selectedPorts.Clear();
                        pendingPort = new SelectedPort(node, false, output.PortIndex);
                        selectedPorts.Add(pendingPort);
                    }

                    GUI.changed = true;
                }

                input.Use();

                return;
            }
        }

        #endregion PORT INPUT
    }
}
#endif