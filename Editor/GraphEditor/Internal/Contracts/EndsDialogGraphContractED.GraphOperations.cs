#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogGraphContractED
    {
        #region GRAPH

        private static DialogEntry FindDialog(DialogDatabase database, EndsMcpJsonED command)
        {
            return FindDialog(database, String(command.Get("dialog")));
        }

        internal static DialogEntry FindDialog(DialogDatabase database, string name)
        {
            DialogEntry found = null;

            if (database.Dialogs == null) throw new InvalidOperationException("The database has no dialogs.");

            foreach (DialogEntry dialog in database.Dialogs)
            {
                if (dialog == null || dialog.dialogName != name) continue;
                if (found != null) throw new InvalidOperationException("The dialog name is ambiguous.");

                found = dialog;
            }

            if (found == null) throw new InvalidOperationException("Dialog not found.");

            return found;
        }

        private static DialogNode FindNode(DialogDatabase database, EndsMcpJsonED command)
        {
            DialogEntry dialog = FindDialog(database, command);

            if (!EndsNodeRefED.TryResolveInDialog(dialog, String(command.Get("node")), out DialogNode node, out string error)) throw new InvalidOperationException(error);

            return node;
        }

        private static Vector2 Position(EndsMcpJsonED command, string name, Vector2 fallback)
        {
            EndsMcpJsonED value = command.Get(name);

            if (value == null) return fallback;

            return (Vector2)ReadInput(typeof(Vector2), value);
        }

        private static void EnsureNodeIdAvailable(DialogEntry dialog, string id)
        {
            foreach (DialogNode node in dialog.nodes ?? new List<DialogNode>())
            {
                if (node != null && node.nodeId == id) throw new InvalidOperationException("Node id already exists in this dialog.");
            }
        }

        private static string PrepareNodeId(DialogDatabase database, DialogEntry dialog, NodeType type, EndsMcpJsonED requested)
        {
            string id;

            if (requested == null) id = EndsNodeIdentityED.Allocate(database, dialog, type);
            else
            {
                id = String(requested);
                EndsNodeIdentityED.RequireFresh(database, dialog, type, id);
            }

            EnsureNodeIdAvailable(dialog, id);

            return id;
        }

        private static void ApplyNodeAdd(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogEntry dialog = FindDialog(database, command);
            NodeType type = (NodeType)ReadInput(typeof(NodeType), command.Get("type"));
            string id = PrepareNodeId(database, dialog, type, command.Get("id"));

            if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("Node id is empty.");


            DialogNode node = EndsGraphCommandsED.Add(dialog, type, id, Position(command, "position", EndsGraphCommandsED.SuggestAddPosition(dialog)), dialog.initiator, database);

            if (command.Get("patch") != null) PatchNode(database, dialog, node, command.Get("patch"));

            response.Set("nodeId", EndsMcpJsonED.Str(node.nodeId));
        }

        private static void ApplyNodeRemove(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogNode node = FindNode(database, command);
            EndsGraphCommandsED.RemoveById(FindDialog(database, command), node.nodeId);
            response.Set("nodeId", EndsMcpJsonED.Str(node.nodeId));
        }

        private static void ApplyNodeDuplicate(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogEntry dialog = FindDialog(database, command);
            DialogNode source = FindNode(database, command);
            string id = PrepareNodeId(database, dialog, source.NodeType, command.Get("id"));
            DialogNode copy = EndsGraphCommandsED.Duplicate(dialog, source, id, Position(command, "offset", EndsGraphCommandsED.SuggestDuplicateOffset(dialog, source)));
            response.Set("nodeId", EndsMcpJsonED.Str(copy.nodeId));
        }

        private static void ApplyNodePatch(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogNode node = FindNode(database, command);
            PatchNode(database, FindDialog(database, command), node, command.Get("patch"));
            response.Set("nodeId", EndsMcpJsonED.Str(node.nodeId));
        }

        private static void PatchNode(DialogDatabase database, DialogEntry dialog, DialogNode node, EndsMcpJsonED patch)
        {
            RequireObject(patch);
            DialogNode before = Snapshot(node);
            EndsDialogRulesED.RequireNodeFields(node.NodeType, patch.Keys);

            ApplyModelPatch(node, patch, database);
            ValidateChangedEdges(dialog, before, node);

            if (node.NodeType == NodeType.Redirect && node.RedirectData != null && node.RedirectData.DialogReferenceState == DialogReferenceState.Valid)
            {
                RedirectNode previous = before.RedirectData;
                RedirectNode current = node.RedirectData;

                EndsMcpJsonED redirectPatch = patch.Get(nameof(DialogNode.RedirectData));

                if ((previous == null || previous.TargetCategory != current.TargetCategory) && redirectPatch?.Get(nameof(RedirectNode.TargetDialogName)) == null) EndsGraphCommandsED.SetRedirectCategory(database, current, current.TargetCategory);
                else if (previous == null || previous.TargetCategory != current.TargetCategory || previous.TargetDialogName != current.TargetDialogName) EndsGraphCommandsED.SetRedirectTarget(database, current, current.TargetCategory, current.TargetDialogName);
            }
        }

        private static void ValidateChangedEdges(DialogEntry dialog, DialogNode before, DialogNode after) => ValidateChangedEdges(dialog, after, NodeSchema, before, after);

        private static void ValidateChangedEdges<TModel>(DialogEntry dialog, DialogNode owner, NodeModelDefinition<TModel> schema, TModel before, TModel after, string prefix = "") where TModel : class
        {
            var previous = new Dictionary<NodeAddress, string>();
            EndsNodeReferencesED.Visit(schema, before, reference =>
            {
                previous[reference.Address] = reference.Value;
            }, NodeTraversalMode.Stored, kind: NodeReferenceKind.Node);
            EndsNodeReferencesED.Visit(schema, after, reference =>
            {
                previous.TryGetValue(reference.Address, out string oldTarget);
                CheckEdge(dialog, owner, oldTarget, reference.Value, prefix + reference.Path);
            }, NodeTraversalMode.Stored, kind: NodeReferenceKind.Node);
        }

        private static void CheckEdge(DialogEntry dialog, DialogNode source, string before, string after, string field)
        {
            if (before == after || string.IsNullOrEmpty(after)) return;
            if (!EndsNodeRefED.TryResolveInDialog(dialog, after, out DialogNode target, out string error)) throw new InvalidOperationException(error);
            if (target.nodeId != after) throw new InvalidOperationException("Stored graph edges require full node ids.");

            RequireValidConnection(source, target, dialog.dialogName, field);
        }

        private static void ApplyEdgeSet(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogEntry dialog = FindDialog(database, command);
            DialogNode node = FindNode(database, command);
            string targetId = Optional(command, "to");

            if (!string.IsNullOrEmpty(targetId))
            {
                if (!EndsNodeRefED.TryResolveInDialog(dialog, targetId, out DialogNode target, out string error)) throw new InvalidOperationException(error);

                targetId = target.nodeId;
            }

            EndsGraphCommandsED.SetPort(database, dialog, node, node.nodeId, String(command.Get("port")), targetId, Optional(command, "category"), Optional(command, "targetDialog"));
            response.Set("nodeId", EndsMcpJsonED.Str(node.nodeId));
        }

        private static void ApplyDisconnectInput(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogNode node = FindNode(database, command);
            EndsGraphCommandsED.ClearIncoming(FindDialog(database, command), node.nodeId);
            response.Set("nodeId", EndsMcpJsonED.Str(node.nodeId));
        }

        #endregion GRAPH

        #region GRAPH PLACEMENT

        private static void ApplyGraphLayout(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            EndsMcpJsonED placement = EndsMcpJsonED.Arr();
            GraphPlacement.Arrange(database, FindDialog(database, command), placement);
            response.Set("placement", placement);
        }

        private sealed class GraphPlacement
        {
            private const float HORIZONTAL_GAP = 180f;
            private const float VERTICAL_GAP = 120f;
            private const float TERMINAL_VERTICAL_GAP = 40f;
            private const float DIAGONAL_PORT_ADVANCE = 40f;

            private sealed class Layout
            {
                internal readonly Dictionary<string, Rect> Before = new Dictionary<string, Rect>(StringComparer.Ordinal);
                internal readonly HashSet<string> Additions = new HashSet<string>(StringComparer.Ordinal);
                internal readonly HashSet<string> Automatic = new HashSet<string>(StringComparer.Ordinal);
            }

            private readonly Dictionary<DialogEntry, Layout> _layouts = new Dictionary<DialogEntry, Layout>();
            private DialogEditorWindowED.ExpandedNodeGeometryED _beforeGeometry;

            internal static DialogEntry FindAffectedDialog(DialogDatabase database, string operation, EndsMcpJsonED command)
            {
                bool nodeOperation = operation == "node.add" || operation == "node.duplicate" || operation == "node.patch"
                    || operation.StartsWith("choice.", StringComparison.Ordinal) || operation.StartsWith("action.", StringComparison.Ordinal);

                if (nodeOperation) return FindDialog(database, command);
                if (operation != "localization.text" || !EndsDialogLocalizationCsvED.TryParseKey(String(command.Get("key")), out string nodeAddress, out _, out _)) return null;
                if (EndsNodeRefED.TryResolveAddress(database, nodeAddress, out DialogEntry dialog, out _, out _)) return dialog;

                return null;
            }

            internal void Capture(DialogDatabase database, DialogEntry dialog)
            {
                if (_layouts.ContainsKey(dialog)) return;

                _beforeGeometry ??= new DialogEditorWindowED.ExpandedNodeGeometryED(database);
                var layout = new Layout();
                _layouts.Add(dialog, layout);

                if (dialog.nodes == null) return;

                foreach (DialogNode node in dialog.nodes)
                {
                    if (node != null && !string.IsNullOrEmpty(node.nodeId)) layout.Before[node.nodeId] = _beforeGeometry.Bounds(node);
                }
            }

            internal void Record(DialogEntry dialog, string operation, EndsMcpJsonED command, EndsMcpJsonED response)
            {
                string nodeId = response.Get("nodeId")?.Text;

                if (string.IsNullOrEmpty(nodeId)) return;

                Layout layout = _layouts[dialog];
                bool explicitPosition = command.Get("patch")?.Get(nameof(DialogNode.Position)) != null;

                if (operation == "node.add" || operation == "node.duplicate")
                {
                    layout.Additions.Add(nodeId);

                    if (!explicitPosition && command.Get("position") == null && command.Get("offset") == null) layout.Automatic.Add(nodeId);
                }

                if (explicitPosition) layout.Automatic.Remove(nodeId);
            }

            internal void Forget(DialogEntry dialog) => _layouts.Remove(dialog);

            internal void Resolve(DialogDatabase database, EndsMcpJsonED output)
            {
                if (_layouts.Count == 0) return;

                var geometry = new DialogEditorWindowED.ExpandedNodeGeometryED(database);

                foreach (KeyValuePair<DialogEntry, Layout> entry in _layouts)
                {
                    if (database.Dialogs.Contains(entry.Key)) Place(entry.Key, geometry, entry.Value, output, false);
                }
            }

            internal static void Arrange(DialogDatabase database, DialogEntry dialog, EndsMcpJsonED output)
            {
                Place(dialog, new DialogEditorWindowED.ExpandedNodeGeometryED(database), new Layout(), output, true);
            }

            private static void Place(DialogEntry dialog, DialogEditorWindowED.ExpandedNodeGeometryED geometry, Layout layout, EndsMcpJsonED output, bool arrangeAll)
            {
                if (dialog.nodes == null) return;

                var occupied = new Dictionary<string, Rect>(StringComparer.Ordinal);
                var pending = new List<DialogNode>();
                var pendingIds = new HashSet<string>(StringComparer.Ordinal);
                var measured = new Dictionary<DialogNode, Rect>();

                foreach (DialogNode node in dialog.nodes)
                {
                    if (node == null || string.IsNullOrEmpty(node.nodeId)) continue;

                    Rect bounds = geometry.Bounds(node);
                    measured[node] = bounds;
                    bool changed = !layout.Before.TryGetValue(node.nodeId, out Rect before) || before.position != bounds.position || bounds.height > before.height;

                    if ((arrangeAll && node.NodeType != NodeType.Start) || (!arrangeAll && changed))
                    {
                        pending.Add(node);
                        pendingIds.Add(node.nodeId);
                    }
                    else occupied[node.nodeId] = bounds;
                }

                if (pending.Count == 0) return;

                var incoming = new Dictionary<string, List<EndsNodeReferencesED.Reference>>(StringComparer.Ordinal);

                EndsNodeReferencesED.Visit(DialogSchema, dialog, edge =>
                {
                    if (string.IsNullOrEmpty(edge.Value) || edge.Value == edge.Node.nodeId) return;

                    if (!incoming.TryGetValue(edge.Value, out List<EndsNodeReferencesED.Reference> sources))
                    {
                        sources = new List<EndsNodeReferencesED.Reference>();
                        incoming.Add(edge.Value, sources);
                    }

                    if (!sources.Exists(source => source.Node == edge.Node)) sources.Add(edge);
                }, kind: NodeReferenceKind.Node);

                while (pending.Count > 0)
                {
                    int index = -1;
                    int root = -1;
                    int cycle = -1;
                    EndsNodeReferencesED.Reference anchorEdge = null;
                    EndsNodeReferencesED.Reference cycleEdge = null;

                    for (int i = 0; i < pending.Count; i++)
                    {
                        incoming.TryGetValue(pending[i].nodeId, out List<EndsNodeReferencesED.Reference> sources);
                        EndsNodeReferencesED.Reference bestEdge = null;
                        float rightEdge = float.NegativeInfinity;
                        bool waiting = false;

                        if (sources != null)
                        {
                            foreach (EndsNodeReferencesED.Reference source in sources)
                            {
                                string sourceId = source.Node.nodeId;

                                if (pendingIds.Contains(sourceId)) waiting = true;
                                if (!occupied.TryGetValue(sourceId, out Rect bounds) || bounds.xMax <= rightEdge) continue;

                                rightEdge = bounds.xMax;
                                bestEdge = source;
                            }
                        }

                        if (waiting)
                        {
                            if (cycle < 0 && bestEdge != null)
                            {
                                cycle = i;
                                cycleEdge = bestEdge;
                            }

                            continue;
                        }

                        if (bestEdge == null)
                        {
                            if (root < 0) root = i;

                            continue;
                        }

                        anchorEdge = bestEdge;
                        index = i;
                        break;
                    }

                    if (index < 0)
                    {
                        if (root >= 0) index = root;
                        else if (cycle >= 0)
                        {
                            index = cycle;
                            anchorEdge = cycleEdge;
                        }
                        else index = 0;
                    }

                    DialogNode nodeToPlace = pending[index];
                    pending.RemoveAt(index);
                    pendingIds.Remove(nodeToPlace.nodeId);
                    Rect requested = measured[nodeToPlace];
                    Rect candidate = requested;
                    bool automatic = arrangeAll || layout.Automatic.Contains(nodeToPlace.nodeId);
                    bool followsFlow = automatic || layout.Additions.Contains(nodeToPlace.nodeId);
                    incoming.TryGetValue(nodeToPlace.nodeId, out List<EndsNodeReferencesED.Reference> predecessors);
                    bool singleSource = predecessors?.Count == 1;
                    string compactParent = null;
                    var conflicts = new Dictionary<string, bool>(StringComparer.Ordinal);

                    if (singleSource && anchorEdge != null && anchorEdge.PortIndex > 0 && nodeToPlace.NodeType == NodeType.Line && string.IsNullOrEmpty(nodeToPlace.LineData?.NextNodeId)) compactParent = anchorEdge.Node.nodeId;
                    if (!automatic) CollectConflicts(requested, occupied, conflicts, compactParent);

                    if (followsFlow && anchorEdge != null)
                    {
                        Rect anchor = occupied[anchorEdge.Node.nodeId];
                        float x = anchor.xMax + HORIZONTAL_GAP;
                        float y = anchor.yMin;

                        if (automatic)
                        {
                            Vector2 input = DialogEditorWindowED.ExpandedNodeGeometryED.InputOffset(nodeToPlace);
                            Vector2 port = geometry.OutputOffset(anchorEdge, anchor.height);
                            bool afterLine = anchorEdge.PortKind == NodePortKind.Next && anchorEdge.Node.NodeType == NodeType.Line && (nodeToPlace.NodeType != NodeType.Line || anchor.height >= anchor.width);

                            if (!singleSource && TryGetBottomJoin(predecessors, occupied, geometry, input, out Vector2 join))
                            {
                                x = join.x;
                                y = join.y;
                            }
                            else if (singleSource && (afterLine || anchorEdge.PortIndex > 0))
                            {
                                x = anchor.xMin + Mathf.Max(HORIZONTAL_GAP, port.x - input.x + DIAGONAL_PORT_ADVANCE);
                                y = anchor.yMax + VERTICAL_GAP;

                                if (compactParent != null) y = anchor.yMax + TERMINAL_VERTICAL_GAP;
                            }
                            else if (anchorEdge.PortKind != NodePortKind.Next) y += port.y - input.y;

                            candidate.position = new Vector2(x, y);
                        }
                        else
                        {
                            float gapBelow = VERTICAL_GAP;

                            if (compactParent != null) gapBelow = TERMINAL_VERTICAL_GAP;
                            if (candidate.yMin >= anchor.yMax + gapBelow || candidate.yMax <= anchor.yMin - VERTICAL_GAP) x = anchor.xMin + DIAGONAL_PORT_ADVANCE;

                            candidate.x = Mathf.Max(candidate.x, x);
                        }
                    }
                    else if (automatic)
                    {
                        candidate.position = Vector2.zero;
                        Rect? rightmost = null;

                        foreach (Rect bounds in occupied.Values)
                        {
                            if (!rightmost.HasValue || bounds.xMax > rightmost.Value.xMax) rightmost = bounds;
                        }

                        if (rightmost.HasValue) candidate.position = new Vector2(rightmost.Value.xMax + HORIZONTAL_GAP, rightmost.Value.yMin);
                    }

                    bool flowAdjusted = candidate.position != requested.position;

                    while (true)
                    {
                        float nextY = CollectConflicts(candidate, occupied, conflicts, compactParent);

                        if (nextY == candidate.y) break;

                        candidate.y = nextY;
                    }

                    nodeToPlace.Position = candidate.position;
                    occupied[nodeToPlace.nodeId] = candidate;
                    var item = EndsMcpJsonED.Obj();
                    item.Set("dialog", EndsMcpJsonED.Str(dialog.dialogName));
                    item.Set("nodeId", EndsMcpJsonED.Str(nodeToPlace.nodeId));
                    item.Set("position", Point(candidate.x, candidate.y));
                    item.Set("expandedSize", Point(candidate.width, candidate.height));
                    item.Set("flowAdjusted", EndsMcpJsonED.Boolean(flowAdjusted));

                    if (!automatic) item.Set("requestedPosition", Point(requested.x, requested.y));

                    var blockers = EndsMcpJsonED.Arr();

                    foreach (KeyValuePair<string, bool> conflict in conflicts)
                    {
                        var blocker = EndsMcpJsonED.Obj();
                        blocker.Set("nodeId", EndsMcpJsonED.Str(conflict.Key));
                        string reason = "clearance";

                        if (conflict.Value) reason = "overlap";

                        blocker.Set("reason", EndsMcpJsonED.Str(reason));
                        blockers.Add(blocker);
                    }

                    item.Set("conflicts", blockers);
                    output.Add(item);
                }
            }

            private static bool TryGetBottomJoin(List<EndsNodeReferencesED.Reference> sources, Dictionary<string, Rect> occupied, DialogEditorWindowED.ExpandedNodeGeometryED geometry, Vector2 input, out Vector2 position)
            {
                position = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

                foreach (EndsNodeReferencesED.Reference source in sources)
                {
                    if (source.PortKind != NodePortKind.Next || source.Node.NodeType != NodeType.Line || !occupied.TryGetValue(source.Node.nodeId, out Rect bounds)) return false;

                    float outputX = bounds.xMin + geometry.OutputOffset(source, bounds.height).x;
                    position.x = Mathf.Max(position.x, Mathf.Max(bounds.xMin + DIAGONAL_PORT_ADVANCE, outputX - input.x));
                    position.y = Mathf.Max(position.y, bounds.yMax + VERTICAL_GAP);
                }

                return true;
            }

            private static float CollectConflicts(Rect candidate, Dictionary<string, Rect> occupied, Dictionary<string, bool> conflicts, string compactParent)
            {
                float nextY = candidate.y;

                foreach (KeyValuePair<string, Rect> obstacle in occupied)
                {
                    Rect bounds = obstacle.Value;
                    float gap = VERTICAL_GAP;

                    if (obstacle.Key == compactParent && candidate.yMin >= bounds.yMax) gap = TERMINAL_VERTICAL_GAP;
                    if (candidate.xMin - HORIZONTAL_GAP >= bounds.xMax || candidate.xMax + HORIZONTAL_GAP <= bounds.xMin) continue;
                    if (candidate.yMin - gap >= bounds.yMax || candidate.yMax + VERTICAL_GAP <= bounds.yMin) continue;

                    bool overlaps = candidate.Overlaps(bounds);

                    if (overlaps || !conflicts.ContainsKey(obstacle.Key)) conflicts[obstacle.Key] = overlaps;

                    nextY = Mathf.Max(nextY, bounds.yMax + gap);
                }

                return nextY;
            }
        }

        #endregion GRAPH PLACEMENT

        #region DIALOGS

        private static void ApplyDialogAdd(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogEntry dialog = EndsDialogCommandsED.Create(database, String(command.Get("name")), Optional(command, "category"), Optional(command, "id"), Position(command, "position", Vector2.zero));
            string id = dialog.nodes[0].nodeId;
            response.Set("dialog", EndsMcpJsonED.Str(dialog.dialogName));
            response.Set("nodeId", EndsMcpJsonED.Str(id));
        }

        private static void ApplyDialogRemove(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            EndsDialogCommandsED.Delete(database, FindDialog(database, command));
        }

        private static void ApplyDialogRename(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogEntry dialog = FindDialog(database, command);
            EndsDialogCommandsED.Rename(database, dialog, String(command.Get("newName")));
            response.Set("dialog", EndsMcpJsonED.Str(dialog.dialogName));
        }

        private static void ApplyDialogDuplicate(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogEntry source = FindDialog(database, command);
            EndsMcpJsonED ids = command.Get("ids");
            List<string> newIds = EndsDialogCommandsED.ReadNodeIds(source);

            if (ids != null)
            {
                RequireArray(ids);
                newIds.Clear();

                foreach (EndsMcpJsonED id in ids.Items)
                {
                    newIds.Add(String(id));
                }
            }

            string name = Optional(command, "newName");

            if (string.IsNullOrEmpty(name)) name = EndsDialogCommandsED.GetUniqueName(database, source.dialogName);

            DialogEntry duplicate = EndsDialogCommandsED.Duplicate(database, source, name, newIds);
            response.Set("dialog", EndsMcpJsonED.Str(duplicate.dialogName));
            DialogNode start = duplicate.nodes.Find(node => node != null && node.NodeType == NodeType.Start);

            if (start != null) response.Set("nodeId", EndsMcpJsonED.Str(start.nodeId));
        }

        private static void ApplyDialogPatch(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogEntry dialog = FindDialog(database, command);
            EndsMcpJsonED patch = command.Get("patch");
            RequireObject(patch);

            foreach (string key in patch.Keys)
            {
                if (key != nameof(DialogEntry.initiator) && key != nameof(DialogEntry.isLocked) && key != nameof(DialogEntry.conditions) && key != nameof(DialogEntry.isAndLogic)) throw new InvalidOperationException("Use a dedicated operation to change dialog " + key + ".");
            }

            if (patch.Get(nameof(DialogEntry.isLocked)) != null) EndsDialogCommandsED.SetLocked(dialog, Boolean(patch.Get(nameof(DialogEntry.isLocked))));
            if (dialog.isLocked && (patch.Get(nameof(DialogEntry.conditions)) != null || patch.Get(nameof(DialogEntry.isAndLogic)) != null)) throw new InvalidOperationException("Unlock the dialog before editing start conditions.");

            if (patch.Get(nameof(DialogEntry.initiator)) != null) EndsDialogCommandsED.SetCategory(database, dialog, String(patch.Get(nameof(DialogEntry.initiator))), true);

            ApplyModelPatch(dialog, patch, database);
        }

        private static void ApplyDialogMove(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            DialogEntry dialog = FindDialog(database, command);
            int index = Integer(command.Get("index"));

            if (index < 0 || index >= database.Dialogs.Count) throw new InvalidOperationException("Dialog position is out of range.");

            database.Dialogs.Remove(dialog);
            database.Dialogs.Insert(index, dialog);
        }

        #endregion DIALOGS

        #region GRAPH REPAIR

        private static void ApplyGraphRepair(DialogDatabase database, EndsMcpJsonED command, EndsMcpJsonED response)
        {
            List<string> issues = InspectGraph(database, FindDialog(database, command), true);
            response.Set("fixed", EndsMcpJsonED.Num(issues.Count));
        }

        private static List<string> InspectGraph(DialogDatabase database, DialogEntry dialog, bool repair)
        {
            List<string> issues = new List<string>();

            if (dialog?.nodes == null) return issues;

            Dictionary<string, DialogNode> ids = new Dictionary<string, DialogNode>();

            foreach (DialogNode node in dialog.nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.nodeId)) ids[node.nodeId] = node;
            }

            foreach (DialogNode node in dialog.nodes)
            {
                if (node == null) continue;

                string label = node.NodeType + " (" + EndsNodeRefED.ShortRef(node.nodeId) + ")";

                EndsNodeReferencesED.Visit(NodeSchema, node, reference =>
                {
                    if (reference.Kind == NodeReferenceKind.Node) reference.Value = InspectEdge(ids, node, reference.Value, label + " " + reference.Port, repair, issues);
                    else if (reference.Kind == NodeReferenceKind.Dialog && reference.Active) InspectRedirectReference(database, reference, label, repair, issues);
                }, NodeTraversalMode.Stored);

            }

            return issues;
        }

        private static void InspectRedirectReference(DialogDatabase database, EndsNodeReferencesED.Reference reference, string label, bool repair, List<string> issues)
        {
            if (!(reference.Model is RedirectNode redirect)) return;

            if (reference.State == DialogReferenceState.Missing) return;

            DialogEntry target = null;

            if (database.Dialogs != null)
            {
                foreach (DialogEntry candidate in database.Dialogs)
                {
                    if (candidate != null && candidate.dialogName == reference.Value) target = candidate;
                }
            }

            if (string.IsNullOrEmpty(reference.Value))
            {
                foreach (DialogEntry candidate in database.Dialogs)
                {
                    if (candidate == null || candidate.initiator != reference.Qualifier || !DialogEntryUtility.IsRedirectTargetCandidate(candidate)) continue;

                    issues.Add(label + " has no redirect target.");

                    if (repair) EndsGraphCommandsED.SetRedirectTarget(database, redirect, reference.Qualifier, candidate.dialogName);

                    break;
                }
            }
            else if (target == null)
            {
                issues.Add(label + " references a missing dialog.");

                if (repair) reference.MarkMissing(reference.Value);
            }
            else if (target.initiator != reference.Qualifier || !DialogEntryUtility.IsRedirectTargetCandidate(target))
            {
                issues.Add(label + " has an invalid redirect target.");

                if (repair) EndsGraphCommandsED.SetRedirectTarget(database, redirect, reference.Qualifier, "");
            }
        }



        private static string InspectEdge(Dictionary<string, DialogNode> ids, DialogNode source, string target, string label, bool repair, List<string> issues)
        {
            if (string.IsNullOrEmpty(target)) return target;

            ids.TryGetValue(target, out DialogNode targetNode);
            string error = GetConnectionError(source, targetNode);

            if (error == null) return target;

            issues.Add(label + ": " + error);

            if (repair) return null;

            return target;
        }

        #endregion GRAPH REPAIR
    }
}
#endif