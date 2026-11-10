#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsGraphCommandsED
    {
        #region NODES

        internal static DialogNode Add(DialogEntry dialog, NodeType type, string nodeId, Vector2 position, string redirectCategory, DialogDatabase database = null)
        {
            if (dialog == null) throw new InvalidOperationException("Dialog is missing.");
            if (dialog.nodes == null) dialog.nodes = new List<DialogNode>();
            if (string.IsNullOrEmpty(nodeId)) throw new InvalidOperationException("Node id is empty.");

            DialogNode existing = Find(dialog, nodeId);

            if (existing != null) return existing;
            if (type == NodeType.Start && Count(dialog, NodeType.Start) > 0) throw new InvalidOperationException("Dialog already has a Start node.");

            DialogNode node = EndsDialogRulesED.CreateNode(type, nodeId, position, redirectCategory);

            if (type == NodeType.Redirect && database != null) SetRedirectCategory(database, node.RedirectData, node.RedirectData.TargetCategory);

            dialog.nodes.Add(node);

            return node;
        }

        internal static Vector2 SuggestAddPosition(DialogEntry dialog)
        {
            const float width = 470f;
            const float height = 260f;
            const float gapX = 50f;
            const float gapY = 48f;
            const float originX = 120f;
            const float originY = 120f;
            const int rows = 3;

            for (int slot = 0; slot < 48; slot++)
            {
                int column = slot / rows;
                int row = slot % rows;
                Vector2 candidate = new Vector2(originX + column * (width + gapX), originY + row * (height + gapY));

                if (!Overlaps(dialog, candidate, width, height)) return candidate;
            }

            return new Vector2(originX, originY);
        }

        internal static Vector2 SuggestDuplicateOffset(DialogEntry dialog, DialogNode source)
        {
            if (source == null) return new Vector2(40f, 180f);

            return SuggestAddPosition(dialog) - source.Position;
        }

        private static bool Overlaps(DialogEntry dialog, Vector2 candidate, float width, float height)
        {
            if (dialog == null || dialog.nodes == null) return false;

            Rect area = new Rect(candidate.x - 12f, candidate.y - 12f, width + 24f, height + 24f);

            for (int i = 0; i < dialog.nodes.Count; i++)
            {
                DialogNode node = dialog.nodes[i];

                if (node == null) continue;
                if (area.Overlaps(new Rect(node.Position.x, node.Position.y, width, height))) return true;
            }

            return false;
        }

        internal static void Remove(DialogEntry dialog, DialogNode node)
        {
            if (dialog == null || node == null) throw new InvalidOperationException("Node is missing.");
            if (dialog.nodes == null) return;
            if (IsOnlyStart(dialog, node)) throw new InvalidOperationException("Start cannot be deleted.");

            ClearIncoming(dialog, node.nodeId);
            dialog.nodes.Remove(node);
        }

        internal static void RemoveById(DialogEntry dialog, string nodeId)
        {
            if (dialog == null) throw new InvalidOperationException("Dialog is missing.");
            if (string.IsNullOrEmpty(nodeId)) throw new InvalidOperationException("removeNodes id is empty.");
            if (dialog.nodes == null) return;
            if (IsOnlyStartId(dialog, nodeId)) throw new InvalidOperationException("Start cannot be deleted.");

            for (int i = dialog.nodes.Count - 1; i >= 0; i--)
            {
                DialogNode node = dialog.nodes[i];

                if (node != null && node.nodeId == nodeId) dialog.nodes.RemoveAt(i);
            }

            ClearIncoming(dialog, nodeId);
        }

        internal static DialogNode Duplicate(DialogEntry dialog, DialogNode source, string newNodeId, Vector2 offset)
        {
            if (dialog == null || source == null) throw new InvalidOperationException("Node is missing.");
            if (dialog.nodes == null) dialog.nodes = new List<DialogNode>();
            if (source.NodeType == NodeType.Start) throw new InvalidOperationException("Start cannot be duplicated.");
            if (string.IsNullOrEmpty(newNodeId)) throw new InvalidOperationException("Node id is empty.");

            DialogNode existing = Find(dialog, newNodeId);

            if (existing != null) return existing;

            DialogNode copy = CopyNode(source, newNodeId, offset, true);

            if (copy.NodeType == NodeType.Redirect && copy.RedirectData == null)
            {
                copy.RedirectData = EndsDialogRulesED.CreateRedirect(dialog.initiator);
            }

            dialog.nodes.Add(copy);

            return copy;
        }

        internal static bool IsOnlyStart(DialogEntry dialog, DialogNode node)
        {
            if (node == null || node.NodeType != NodeType.Start) return false;

            return Count(dialog, NodeType.Start) == 1;
        }

        /// <summary> ENDS. Copies a node with fresh nested block identities, whole-dialog copies preserve local IDs because their enclosing dialog scope is new. </summary>
        internal static DialogNode CopyNode(DialogNode source, string id, Vector2 offset, bool clearOutgoing, bool preserveBlockIds = false)
        {
            DialogNode copy = EndsDialogRulesED.Snapshot(source);
            copy.nodeId = id;
            copy.Position += offset;

            if (!preserveBlockIds) EndsBlockIdentityED.ClearCopiedIds(copy);

            if (clearOutgoing) ClearOutgoing(copy);

            return copy;
        }

        #endregion NODES

        #region EDGES

        internal static void ConnectPort(DialogNode sourceNode, string targetId, int choiceIndex)
        {
            if (sourceNode == null) return;
            if (sourceNode.NodeType == NodeType.Line && sourceNode.LineData == null) sourceNode.LineData = EndsDialogRulesED.CreateLine();
            if (sourceNode.NodeType == NodeType.Start && sourceNode.StartData == null) sourceNode.StartData = EndsDialogRulesED.CreateStart();

            EndsNodeReferencesED.Reference output = EndsNodeReferencesED.FindPort(sourceNode, choiceIndex);

            if (output != null) output.Value = targetId;
        }

        internal static void DisconnectPort(DialogNode node, int choiceIndex)
        {
            if (node == null) return;

            EndsNodeReferencesED.Reference output = EndsNodeReferencesED.FindPort(node, choiceIndex);

            if (output != null) output.Value = null;
        }

        internal static void SetPort(DialogDatabase database, DialogEntry dialog, DialogNode from, string fromId, string port, string toId, string targetCategory, string targetDialog)
        {
            if (from == null) throw new InvalidOperationException("Unknown from node " + fromId + ".");
            if (string.IsNullOrEmpty(port)) throw new InvalidOperationException("Unknown port " + port + ".");

            if (port == "redirect")
            {
                if (from.NodeType != NodeType.Redirect) throw new InvalidOperationException("redirect port is only on a redirect node.");
                if (from.RedirectData == null) from.RedirectData = EndsDialogRulesED.CreateRedirect(dialog.initiator);

                SetRedirectTarget(database, from.RedirectData, targetCategory, targetDialog);

                return;
            }

            if (!string.IsNullOrEmpty(toId) && Find(dialog, toId) == null) throw new InvalidOperationException("Unknown to node " + toId + ".");
            if (!string.IsNullOrEmpty(toId)) EndsDialogRulesED.RequireValidConnection(from, Find(dialog, toId), dialog.dialogName, port);

            if (port == "next")
            {
                if (from.NodeType == NodeType.Start && from.StartData == null) from.StartData = EndsDialogRulesED.CreateStart();
                if (from.NodeType == NodeType.Line && from.LineData == null) from.LineData = EndsDialogRulesED.CreateLine();
            }
            else if (port == "true" || port == "false")
            {
                if (from.NodeType == NodeType.Condition && from.ConditionData == null) from.ConditionData = EndsDialogRulesED.CreateConditionNode();
            }
            else if (port.StartsWith("choice:", StringComparison.Ordinal) && from.NodeType == NodeType.Choice)
            {
                if (from.ChoiceData == null) from.ChoiceData = new List<ChoiceNode>();
                if (!int.TryParse(port.Substring("choice:".Length), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int index) || index < 0) throw new InvalidOperationException("Bad choice port " + port + ".");
                if (index > from.ChoiceData.Count) throw new InvalidOperationException("choice index skips a slot on " + fromId + ".");
                if (index == from.ChoiceData.Count) from.ChoiceData.Add(EndsDialogRulesED.CreateChoice());
            }

            EndsNodeReferencesED.Reference output = EndsNodeReferencesED.FindPort(from, port);

            if (output == null) throw new InvalidOperationException("Unknown port " + port + " on " + from.NodeType + ".");
            if (string.IsNullOrEmpty(toId)) toId = null;

            output.Value = toId;
        }

        /// <summary> ENDS. Selects the first eligible dialog in the same ordinal order as the graph popup when a redirect category is chosen. </summary>
        internal static void SetRedirectCategory(DialogDatabase database, RedirectNode redirect, string category)
        {
            DialogEntry first = null;

            foreach (DialogEntry candidate in database.Dialogs)
            {
                if (!DialogEntryUtility.IsRedirectTargetCandidate(candidate) || candidate.initiator != category) continue;
                if (first == null || string.CompareOrdinal(candidate.dialogName, first.dialogName) < 0) first = candidate;
            }

            SetRedirectTarget(database, redirect, category, first?.dialogName ?? "");
        }

        internal static void SetRedirectTarget(DialogDatabase database, RedirectNode redirect, string category, string dialogName)
        {
            if (redirect == null) throw new InvalidOperationException("Redirect node has no data.");
            if (database == null || database.Categories == null || !database.Categories.Contains(category)) throw new InvalidOperationException("Category not found.");

            if (string.IsNullOrEmpty(dialogName))
            {
                redirect.TargetCategory = category;
                redirect.TargetDialogName = "";
                redirect.DialogReferenceState = DialogReferenceState.Valid;
                redirect.MissingDialogReferenceLabel = "";

                return;
            }

            if (!IsRedirectTarget(database, category, dialogName)) throw new InvalidOperationException("Dialog \"" + dialogName + "\" is not a redirect target in \"" + category + "\".");

            DialogReferenceEditorUtility.RestoreDialogReference(redirect, dialogName, category);
        }

        internal static void ClearIncoming(DialogEntry dialog, string removedNodeId)
        {
            if (dialog == null || string.IsNullOrEmpty(removedNodeId)) return;

            EndsNodeReferencesED.Visit(DialogSchema, dialog, reference =>
            {
                if (reference.Kind == NodeReferenceKind.Node && reference.Value == removedNodeId) reference.Value = null;
            }, NodeTraversalMode.Stored);
        }

        #endregion EDGES

        #region LOOKUP

        private static bool IsOnlyStartId(DialogEntry dialog, string nodeId)
        {
            if (Count(dialog, NodeType.Start) != 1) return false;
            if (dialog?.nodes == null) return false;

            for (int i = 0; i < dialog.nodes.Count; i++)
            {
                DialogNode node = dialog.nodes[i];

                if (node == null || node.nodeId != nodeId) continue;
                if (node.NodeType == NodeType.Start) return true;
            }

            return false;
        }

        private static int Count(DialogEntry dialog, NodeType type)
        {
            int count = 0;

            if (dialog?.nodes == null) return 0;

            for (int i = 0; i < dialog.nodes.Count; i++)
            {
                DialogNode node = dialog.nodes[i];

                if (node != null && node.NodeType == type) count++;
            }

            return count;
        }

        private static DialogNode Find(DialogEntry dialog, string nodeId)
        {
            if (dialog?.nodes == null || string.IsNullOrEmpty(nodeId)) return null;

            for (int i = 0; i < dialog.nodes.Count; i++)
            {
                DialogNode node = dialog.nodes[i];

                if (node != null && node.nodeId == nodeId) return node;
            }

            return null;
        }

        private static bool IsRedirectTarget(DialogDatabase database, string category, string dialogName)
        {
            if (database?.Dialogs == null) return false;

            for (int i = 0; i < database.Dialogs.Count; i++)
            {
                DialogEntry entry = database.Dialogs[i];

                if (entry == null || entry.dialogName != dialogName) continue;
                if (entry.initiator != category) continue;
                if (!DialogEntryUtility.IsRedirectTargetCandidate(entry)) continue;

                return true;
            }

            return false;
        }

        private static void ClearOutgoing(DialogNode node)
        {
            if (node == null) return;

            EndsNodeReferencesED.Visit(NodeSchema, node, reference =>
            {
                if (reference.Kind != NodeReferenceKind.Node) return;
                if (reference.Port == "true" || reference.Port == "false") reference.Value = null;
                else reference.Value = "";
            }, NodeTraversalMode.Stored);
        }



        #endregion LOOKUP
    }
}
#endif