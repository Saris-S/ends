#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public partial class DialogEditorWindowED
    {
        #region FIELDS

        [NonSerialized] private DialogEntry _graphDraft;
        [NonSerialized] private DialogEntry _graphBefore;
        [NonSerialized] private DialogEntry _graphView;
        [NonSerialized] private int _graphViewRevision = -1;
        [NonSerialized] private int _graphExpectedRevision;
        [NonSerialized] private bool _graphGestureActive;
        [NonSerialized] private bool _graphEditRequested;
        [NonSerialized] private bool _graphDataDirty;
        [NonSerialized] private bool _registryDrawing;
        [NonSerialized] private string _graphLastInput;
        [NonSerialized] private string _graphLastStatus;
        [NonSerialized] private string _graphLastError;
        [NonSerialized] private bool _graphLastInputEdited;
        [NonSerialized] private Vector2 _graphLastPointer;
        [NonSerialized] private Vector2 _graphGuiOrigin;
        private readonly List<EndsMcpJsonED> _registryCommands = new List<EndsMcpJsonED>();
        private readonly List<EndsMcpJsonED> _queuedEditorCommands = new List<EndsMcpJsonED>();

        #endregion FIELDS

        #region SESSION INTEGRATION

        private IDisposable OpenEditorReadOnlyView()
        {
            // UI edits detached drafts or submits commands, input events do not require a full database comparison.
            return EndsSessionED.ForDatabase(database).OpenReadOnlyView(validateWrites: false);
        }

        internal static EndsMcpJsonED ReadEditorState(DialogDatabase database)
        {
            EndsMcpJsonED views = EndsMcpJsonED.Arr();

            foreach (DialogEditorWindowED window in Resources.FindObjectsOfTypeAll<DialogEditorWindowED>())
            {
                if (window == null || window.database != database) continue;

                EndsMcpJsonED view = EndsMcpJsonED.Obj();
                view.Set("title", EndsMcpJsonED.Str(window.titleContent.text));
                view.Set("dialog", EndsMcpJsonED.Str(window.selectedDialogName));
                view.Set("editing", EndsMcpJsonED.Boolean(window.isEditingDialog));
                view.Set("tab", EndsMcpJsonED.Num(window.currentMainTab));
                view.Set("gesture", EndsMcpJsonED.Boolean(window._graphGestureActive));
                view.Set("pressedHeader", EndsMcpJsonED.Num(window.pressedHeaderWindow));
                view.Set("selectedNodes", EndsMcpJsonED.Num(window.selectedNodes.Count));
                view.Set("pendingPort", EndsMcpJsonED.Boolean(window.pendingPort != null));
                view.Set("boxSelecting", EndsMcpJsonED.Boolean(window.isBoxSelecting));
                view.Set("pointer", EndsDialogGraphContractED.Point(window._graphLastPointer.x, window._graphLastPointer.y));
                view.Set("screenOrigin", EndsDialogGraphContractED.Point(window._graphGuiOrigin.x, window._graphGuiOrigin.y));
                view.Set("draftDirty", EndsMcpJsonED.Boolean(window._graphDataDirty));
                view.Set("queuedCommands", EndsMcpJsonED.Num(window._queuedEditorCommands.Count));
                view.Set("lastInput", EndsMcpJsonED.Str(window._graphLastInput));
                view.Set("lastInputEdited", EndsMcpJsonED.Boolean(window._graphLastInputEdited));
                view.Set("lastStatus", EndsMcpJsonED.Str(window._graphLastStatus));
                view.Set("lastError", EndsMcpJsonED.Str(window._graphLastError));
                views.Add(view);
            }

            return views;
        }

        private void HandleSessionChanged(DialogDatabase changedDatabase)
        {
            if (database != changedDatabase) return;

            DialogEditorWindowED window = this;
            HashSet<string> selectedIds = new HashSet<string>();

            foreach (DialogNode node in window.selectedNodes)
            {
                if (node != null) selectedIds.Add(node.nodeId);
            }

            window.selectedNodes.Clear();

            if (window.selectedDialog?.nodes != null)
            {
                foreach (DialogNode node in window.selectedDialog.nodes)
                {
                    if (node != null && selectedIds.Contains(node.nodeId)) window.selectedNodes.Add(node);
                }
            }

            window.RemapGraphSelection(window.selectedDialog);
            window.InvalidateReferenceIndex();
            window.RefreshSavedDialogReferenceIndex();
            window.Repaint();
        }

        internal EndsSessionED.Result SubmitConditionEdit(string dialogName, string nodeId, ConditionNode snapshot, bool replaceConditions, bool? andLogic, int expectedRevision)
        {
            if (_graphDraft != null)
            {
                if (!EndsNodeRefED.TryResolveInDialog(_graphDraft, nodeId, out DialogNode node, out string error)) throw new InvalidOperationException(error);
                if (node.ConditionData == null) node.ConditionData = EndsDialogRulesED.CreateConditionNode();
                if (replaceConditions) node.ConditionData.DialogConditions = snapshot.DialogConditions;
                if (andLogic.HasValue) node.ConditionData.IsAndLogic = andLogic.Value;

                _graphPortsDirty = true;
                _graphEditRequested = true;
                _graphDataDirty = true;

                return new EndsSessionED.Result { Status = "applied" };
            }

            EndsSessionED.Result result = EndsDialogConditionContractED.SetNodeConditions(database, dialogName, nodeId, snapshot.DialogConditions, replaceConditions, andLogic, expectedRevision);

            if (!result.Success) Debug.LogError("ENDS condition edit failed: " + result.Error);

            return result;
        }

        #endregion SESSION INTEGRATION

        #region GRAPH VIEW AND COMMANDS

        internal IReadOnlyList<CharacterActionData> ReadPreviewActions(DialogDatabase previewDatabase, string dialogKey, string nodeId)
        {
            if (previewDatabase == null) return null;

            DialogEntry previewDialog = null;
            DialogEntry view = _graphDraft ?? _graphView;

            if (this != null && database == previewDatabase && view != null && view.NodeIdentityKey == dialogKey && (_graphDraft != null || _graphGestureActive || _graphViewRevision == EndsSessionED.ForDatabase(previewDatabase).DialogRevision(dialogKey))) previewDialog = view;

            if (previewDialog == null && previewDatabase.Dialogs != null)
            {
                foreach (DialogEntry dialog in previewDatabase.Dialogs)
                {
                    if (dialog != null && dialog.NodeIdentityKey == dialogKey) previewDialog = dialog;
                }
            }

            if (previewDialog?.nodes == null) return null;

            foreach (DialogNode node in previewDialog.nodes)
            {
                if (node != null && node.nodeId == nodeId && node.NodeType == NodeType.Line) return node.LineData?.CharacterActions;
            }

            return null;
        }

        private void DrawDialogEditor()
        {
            if (database == null)
            {
                DrawDialogEditorView();

                return;
            }

            using (OpenEditorReadOnlyView())
            {
                DrawDialogEditorView();
            }
        }

        private void DrawDialogEditorView()
        {
            DialogEntry live = GetSelectedDialog();

            if (live == null)
            {
                ResetDialogEditorSessionState();
                DrawDialogList();

                return;
            }

            EndsSessionED session = EndsSessionED.ForDatabase(database);
            bool readOnly = EndsSessionED.ExclusiveEditing && session.DialogOwner(live.dialogName) != null;
            DrawCollaborationStatus(session, live, readOnly);

            if (!isEditingDialog) return;

            EventType eventType = Event.current.rawType;
            bool input = eventType != EventType.Layout && eventType != EventType.Repaint;
            if (input) _graphLastInput = eventType.ToString();

            int revision = session.ObservedRevision;
            int dialogRevision = session.DialogRevision(live.NodeIdentityKey);

            if (_graphView == null || _graphView.dialogName != live.dialogName || (!_graphGestureActive && _graphViewRevision != dialogRevision))
            {
                _graphView = EndsDialogGraphContractED.Snapshot(live);
                _graphBefore = EndsDialogGraphContractED.Snapshot(live);
                _graphViewRevision = dialogRevision;
                _graphExpectedRevision = revision;
                _graphDataDirty = false;
            }

            if (!_graphGestureActive && !_graphDataDirty) _graphExpectedRevision = revision;

            _graphDraft = _graphView;
            _graphEditRequested = false;
            RemapGraphSelection(_graphDraft);
            bool completed = false;

            try
            {
                using (new EditorGUI.DisabledScope(readOnly)) DrawDialogEditorContent();
                completed = true;
            }
            catch (ExitGUIException)
            {
                completed = true;

                throw;
            }
            finally
            {
                if (input) _graphLastInputEdited = _graphEditRequested;

                if (!_graphGestureActive && (pressedHeaderWindow >= 0 || isDraggingSelection || (_graphEditRequested && GUIUtility.hotControl != 0 && (eventType == EventType.MouseDown || eventType == EventType.MouseDrag)))) _graphGestureActive = true;

                bool finishGesture = _graphGestureActive && (eventType == EventType.MouseUp || eventType == EventType.Ignore || (input && GUIUtility.hotControl == 0 && pressedHeaderWindow < 0 && !isDraggingSelection));
                bool flush = completed && input && (_graphEditRequested || finishGesture) && (!_graphGestureActive || finishGesture);
                _graphDraft = null;

                if (finishGesture) _graphGestureActive = false;
                if (eventType == EventType.MouseUp) pressedHeaderWindow = -1;
                if (flush && !readOnly) FlushGraphEdits();

                if (readOnly)
                {
                    _queuedEditorCommands.Clear();

                    if (_graphDataDirty) _graphViewRevision = -1;

                    _graphDataDirty = false;
                }

                RemapGraphSelection(GetSelectedDialog());
            }
        }

        private void RecordEditorIntent(string label)
        {
            if (_graphDraft == null) throw new InvalidOperationException("Editor data changes must use a registered operation: " + label);

            _graphPortsDirty = true;
            _graphEditRequested = true;
            _graphDataDirty = true;
        }

        private void SubmitEditorCommand(EndsMcpJsonED command)
        {
            if (_graphDraft != null)
            {
                _queuedEditorCommands.Add(command);
                _graphEditRequested = true;
                _graphDataDirty = true;

                return;
            }

            if (_registryDrawing)
            {
                _registryCommands.Add(command);

                return;
            }

            EndsSessionED.Result result = EndsDialogGraphContractED.Execute(database, command);

            if (!result.Success) Debug.LogError("ENDS editor operation failed: " + result.Error);
        }

        private bool FlushGraphEdits()
        {
            if (database == null) return false;

            if (_graphBefore == null || _graphView == null) return true;
            if (!_graphDataDirty && _queuedEditorCommands.Count == 0) return true;

            bool success = true;

            EndsMcpJsonED commands = BuildGraphEdits(_graphBefore, _graphView);

            foreach (EndsMcpJsonED command in _queuedEditorCommands)
            {
                commands.Add(command);
            }

            _queuedEditorCommands.Clear();

            if (commands.Items.Count > 0)
            {
                EndsSessionED.Result result = EndsDialogGraphContractED.ExecuteBatch(database, commands, _graphExpectedRevision, false, out _);

                success = result.Success;
                _graphLastStatus = result.Status;
                _graphLastError = result.Error;

                if (!success) Debug.LogError("ENDS graph edit failed: " + result.Error);
            }

            _graphView = null;
            _graphBefore = null;
            _graphViewRevision = -1;
            _graphDataDirty = false;

            return success;
        }

        private GenericMenu.MenuFunction GraphCallback(GenericMenu.MenuFunction action)
        {
            if (_graphDraft == null)
            {
                DialogDatabase callbackDatabase = database;

                return () =>
                {
                    if (database != callbackDatabase) return;
                    if (callbackDatabase == null)
                    {
                        action();

                        return;
                    }

                    using (EndsSessionED.ForDatabase(callbackDatabase).OpenReadOnlyView())
                    {
                        action();
                    }
                };
            }

            DialogEntry view = _graphDraft;
            DialogEntry before = EndsDialogGraphContractED.Snapshot(view);
            DialogDatabase capturedDatabase = database;
            int revision = _graphExpectedRevision;

            return () =>
            {
                if (database != capturedDatabase) return;

                _graphDraft = view;
                _graphView = view;
                _graphBefore = before;
                _graphExpectedRevision = revision;

                try
                {
                    using (EndsSessionED.ForDatabase(capturedDatabase).OpenReadOnlyView())
                    {
                        action();
                        FlushGraphEdits();
                    }
                }
                finally
                {
                    _graphDraft = null;
                    _graphView = null;
                    _graphBefore = null;
                    _graphDataDirty = false;
                    _graphEditRequested = false;
                    _queuedEditorCommands.Clear();
                    RemapGraphSelection(GetSelectedDialog());
                    Repaint();
                }
            };
        }

        private void DrawRegistryEditor(Action draw)
        {
            if (database == null)
            {
                draw();

                return;
            }

            using (OpenEditorReadOnlyView())
            {
                DrawRegistryEditorView(draw);
            }
        }

        private void DrawRegistryEditorView(Action draw)
        {
            int revision = -1;

            if (database != null) revision = EndsSessionED.ForDatabase(database).ObservedRevision;

            _registryDrawing = true;
            bool completed = false;

            try
            {
                draw();
                completed = true;
            }
            catch (ExitGUIException)
            {
                completed = true;

                throw;
            }
            finally
            {
                _registryDrawing = false;

                if (completed && _registryCommands.Count > 0)
                {
                    EndsMcpJsonED commands = EndsMcpJsonED.Arr();

                    foreach (EndsMcpJsonED command in _registryCommands)
                    {
                        commands.Add(command);
                    }

                    _registryCommands.Clear();
                    EndsSessionED.Result result = EndsDialogGraphContractED.ExecuteBatch(database, commands, revision, false, out _);

                    if (!result.Success) Debug.LogError("ENDS registry edit failed: " + result.Error);
                }
                else _registryCommands.Clear();
            }
        }

        private void AddGraphNode(NodeType type, Vector2 position)
        {
            if (_graphDraft != null)
            {
                RecordEditorIntent("Add graph node");
                EndsGraphCommandsED.Add(_graphDraft, type, EndsNodeIdentityED.Allocate(database, _graphDraft, type), position, _graphDraft.initiator, database);

                return;
            }

            EndsMcpJsonED command = EndsDialogGraphContractED.Command("node.add");
            command.Set("dialog", EndsMcpJsonED.Str(selectedDialogName));
            command.Set("type", EndsMcpJsonED.Str(type.ToString()));
            command.Set("position", EndsDialogGraphContractED.Point(position.x, position.y));
            SubmitEditorCommand(command);
        }

        private void SubmitNamedEditorCommand(string operation, string name, string newName = null)
        {
            EndsMcpJsonED command = EndsDialogGraphContractED.Command(operation);
            string key = "name";

            if (operation.StartsWith("dialog.", StringComparison.Ordinal) && operation != "dialog.add") key = "dialog";

            command.Set(key, EndsMcpJsonED.Str(name));

            if (newName != null) command.Set("newName", EndsMcpJsonED.Str(newName));

            SubmitEditorCommand(command);
        }

        private void SubmitDialogPatch(string dialog, EndsMcpJsonED patch)
        {
            EndsMcpJsonED command = EndsDialogGraphContractED.Command("dialog.patch");
            command.Set("dialog", EndsMcpJsonED.Str(dialog));
            command.Set("patch", patch);
            SubmitEditorCommand(command);
        }

        private static EndsMcpJsonED BuildGraphEdits(DialogEntry before, DialogEntry after)
        {
            EndsMcpJsonED commands = EndsMcpJsonED.Arr();
            EndsMcpJsonED patches = EndsMcpJsonED.Arr();
            EndsMcpJsonED removals = EndsMcpJsonED.Arr();
            Dictionary<string, DialogNode> oldNodes = new Dictionary<string, DialogNode>();
            HashSet<string> newIds = new HashSet<string>();

            if (before.nodes != null)
            {
                foreach (DialogNode node in before.nodes)
                {
                    if (node != null) oldNodes.Add(node.nodeId, node);
                }
            }

            if (after.nodes != null)
            {
                foreach (DialogNode node in after.nodes)
                {
                    if (node == null) continue;
                    if (!newIds.Add(node.nodeId)) throw new InvalidOperationException("Duplicate node id in the graph view.");

                    EndsMcpJsonED patch;

                    if (!oldNodes.TryGetValue(node.nodeId, out DialogNode original))
                    {
                        EndsMcpJsonED add = EndsDialogGraphContractED.Command("node.add");
                        add.Set("dialog", EndsMcpJsonED.Str(before.dialogName));
                        add.Set("type", EndsMcpJsonED.Str(node.NodeType.ToString()));
                        add.Set("id", EndsMcpJsonED.Str(node.nodeId));
                        add.Set("position", EndsDialogGraphContractED.Point(node.Position.x, node.Position.y));
                        commands.Add(add);
                        patch = EndsDialogGraphContractED.ReadModel(node);
                        patch.Remove(nameof(DialogNode.nodeId));
                        patch.Remove(nameof(DialogNode.NodeType));
                        patch.Remove(nameof(DialogNode.Position));
                        string activeField = EndsDialogRulesED.GetNodeDataField(node.NodeType);

                        foreach (string data in new[] { nameof(DialogNode.StartData), nameof(DialogNode.LineData), nameof(DialogNode.ChoiceData), nameof(DialogNode.ConditionData), nameof(DialogNode.RedirectData) })
                        {
                            if (data != activeField) patch.Remove(data);
                        }
                    }
                    else patch = EndsDialogGraphContractED.Difference(original, node);

                    if (patch.Keys.Count == 0) continue;

                    EndsMcpJsonED change = EndsDialogGraphContractED.NodeCommand("node.patch", before.dialogName, node.nodeId);
                    change.Set("patch", patch);
                    patches.Add(change);
                }
            }

            foreach (string id in oldNodes.Keys)
            {
                if (!newIds.Contains(id)) removals.Add(EndsDialogGraphContractED.NodeCommand("node.remove", before.dialogName, id));
            }

            foreach (EndsMcpJsonED patch in patches.Items)
            {
                commands.Add(patch);
            }

            foreach (EndsMcpJsonED removal in removals.Items)
            {
                commands.Add(removal);
            }

            EndsMcpJsonED dialogPatch = EndsDialogGraphContractED.Difference(before, after, field => !field.Identity && !field.Diagnostic && field.Name != EndsNodeStructureED.DialogSchema.Nodes.Name);

            if (dialogPatch.Keys.Count > 0)
            {
                EndsMcpJsonED change = EndsDialogGraphContractED.Command("dialog.patch");
                change.Set("dialog", EndsMcpJsonED.Str(before.dialogName));
                change.Set("patch", dialogPatch);
                commands.Add(change);
            }

            return commands;
        }

        private void RemapGraphSelection(DialogEntry dialog)
        {
            if (selectedNodes.Count == 0 && selectedPorts.Count == 0 && pendingPort == null) return;

            HashSet<string> ids = new HashSet<string>();

            foreach (DialogNode node in selectedNodes)
            {
                if (node != null) ids.Add(node.nodeId);
            }

            selectedNodes.Clear();

            if (dialog?.nodes != null)
            {
                foreach (DialogNode node in dialog.nodes)
                {
                    if (node != null && ids.Contains(node.nodeId)) selectedNodes.Add(node);
                }
            }

            for (int i = selectedPorts.Count - 1; i >= 0; i--)
            {
                SelectedPort port = selectedPorts[i];

                if (dialog == null || port.Node == null || !EndsNodeRefED.TryResolveInDialog(dialog, port.Node.nodeId, out DialogNode current, out _)) selectedPorts.RemoveAt(i);
                else selectedPorts[i] = new SelectedPort(current, port.IsInput, port.ChoiceIndex);
            }

            if (pendingPort != null)
            {
                if (dialog == null || pendingPort.Node == null || !EndsNodeRefED.TryResolveInDialog(dialog, pendingPort.Node.nodeId, out DialogNode current, out _)) pendingPort = null;
                else pendingPort = new SelectedPort(current, pendingPort.IsInput, pendingPort.ChoiceIndex);
            }
        }

        #endregion GRAPH VIEW AND COMMANDS
    }
}
#endif