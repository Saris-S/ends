#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsDialogCommandsED
    {
        #region DIALOG LIST

        internal static DialogEntry Create(DialogDatabase database, string name, string category, string startNodeId, Vector2 startPosition)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (database.Dialogs == null) database.Dialogs = new List<DialogEntry>();
            if (database.Categories == null) database.Categories = new List<string>();
            name = GetUniqueName(database, name);
            EndsDialogRulesED.RequireDialogName(name);

            if (string.IsNullOrEmpty(category)) category = EndsDatabaseMaintenanceED.DefaultCategoryName;

            if (!database.Categories.Contains(category)) database.Categories.Add(category);

            DialogEntry entry = new DialogEntry
            {
                dialogName = name,
                initiator = category,
                conditions = new List<DialogCondition>(),
                nodes = new List<DialogNode>()
            };

            EndsNodeIdentityED.InitializeDialog(entry);

            if (string.IsNullOrEmpty(startNodeId)) startNodeId = EndsNodeIdentityED.Allocate(database, entry, NodeType.Start);
            else EndsNodeIdentityED.RequireFresh(database, entry, NodeType.Start, startNodeId);

            entry.nodes.Add(EndsDialogRulesED.CreateNode(NodeType.Start, startNodeId, startPosition, category));

            database.Dialogs.Add(entry);

            return entry;
        }

        internal static void Rename(DialogDatabase database, DialogEntry dialog, string newName)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (dialog == null) throw new InvalidOperationException("Dialog is missing.");
            newName = GetUniqueName(database, newName, dialog.dialogName);
            EndsDialogRulesED.RequireDialogName(newName);

            if (dialog.dialogName == newName) return;

            string oldName = dialog.dialogName;
            dialog.dialogName = newName;
            DialogReferenceRename.RenameDialogInRedirectNodes(database, oldName, newName);
        }

        internal static void Delete(DialogDatabase database, DialogEntry dialog)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (dialog == null) throw new InvalidOperationException("Dialog is missing.");
            if (database.Dialogs == null) return;
            if (!database.Dialogs.Contains(dialog)) return;

            DialogReferenceCleanup.MarkDialogMissingInDialogs(database, dialog.dialogName);
            database.Dialogs.Remove(dialog);
        }

        internal static void SetCategory(DialogDatabase database, DialogEntry dialog, string category, bool addIfMissing)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (dialog == null) throw new InvalidOperationException("Dialog is missing.");

            if (string.IsNullOrEmpty(category))
            {
                dialog.initiator = category ?? "";

                return;
            }

            if (database.Categories == null) database.Categories = new List<string>();

            if (!database.Categories.Contains(category))
            {
                if (!addIfMissing) throw new InvalidOperationException("Category not found.");

                database.Categories.Add(category);
            }

            dialog.initiator = category;
        }

        internal static void SetLocked(DialogEntry dialog, bool locked)
        {
            if (dialog == null) throw new InvalidOperationException("Dialog is missing.");

            dialog.isLocked = locked;
        }

        #endregion DIALOG LIST

        #region DUPLICATE

        internal static List<string> ReadNodeIds(DialogEntry original)
        {
            List<string> ids = new List<string>();

            if (original?.nodes == null) return ids;

            for (int i = 0; i < original.nodes.Count; i++)
            {
                if (original.nodes[i] != null) ids.Add(original.nodes[i].nodeId);
            }

            return ids;
        }

        internal static DialogEntry Duplicate(DialogDatabase database, DialogEntry original, string newName, IReadOnlyList<string> newNodeIds)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (original == null) throw new InvalidOperationException("Dialog is missing.");
            if (database.Dialogs == null) database.Dialogs = new List<DialogEntry>();
            EndsDialogRulesED.RequireDialogName(newName);

            if (Find(database, newName) != null) throw new InvalidOperationException("Dialog already exists.");
            if (newNodeIds == null) throw new InvalidOperationException("Node id list is missing.");

            Dictionary<string, string> nodeIdMap = new Dictionary<string, string>();
            HashSet<string> allocatedIds = new HashSet<string>();
            int sourceNodeCount = 0;

            if (original.nodes != null)
            {
                foreach (DialogNode sourceNode in original.nodes)
                {
                    if (sourceNode == null) continue;
                    if (sourceNodeCount >= newNodeIds.Count) throw new InvalidOperationException("Node id list does not match the source nodes.");

                    string newNodeId = newNodeIds[sourceNodeCount];

                    if (string.IsNullOrEmpty(sourceNode.nodeId) || nodeIdMap.ContainsKey(sourceNode.nodeId)) throw new InvalidOperationException("Source node ids must be nonempty and unique.");
                    if (string.IsNullOrEmpty(newNodeId) || !allocatedIds.Add(newNodeId)) throw new InvalidOperationException("New node ids must be nonempty and unique.");
                    if (!EndsNodeIdentityED.IsReadable(newNodeId, sourceNode.NodeType)) throw new InvalidOperationException("Copied node id must match its node type.");

                    nodeIdMap.Add(sourceNode.nodeId, newNodeId);
                    sourceNodeCount++;
                }
            }

            if (sourceNodeCount != newNodeIds.Count) throw new InvalidOperationException("Node id list does not match the source nodes.");

            DialogEntry duplicate = EndsDialogRulesED.Snapshot(original);
            duplicate.dialogName = newName;
            EndsNodeIdentityED.InitializeDialog(duplicate, true);
            duplicate.nodes = new List<DialogNode>();

            foreach (DialogNode originalNode in original.nodes ?? new List<DialogNode>())
            {
                if (originalNode == null)
                {
                    duplicate.nodes.Add(null);

                    continue;
                }

                duplicate.nodes.Add(EndsGraphCommandsED.CopyNode(originalNode, nodeIdMap[originalNode.nodeId], Vector2.zero, false, preserveBlockIds: true));
            }

            foreach (DialogNode node in duplicate.nodes)
            {
                if (node == null) continue;

                EndsNodeIdentityED.RemapEdges(node, nodeIdMap);
            }

            EndsNodeIdentityED.InitializeDialog(duplicate);
            database.Dialogs.Add(duplicate);

            return duplicate;
        }

        internal static string GetUniqueName(DialogDatabase database, string baseName, string excludedName = null)
        {
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "New Dialog";
            else baseName = baseName.Trim();

            string candidate = baseName;
            int number = 1;

            while (candidate != excludedName && Find(database, candidate) != null)
            {
                candidate = baseName + " (" + number + ")";
                number++;
            }

            return candidate;
        }

        #endregion DUPLICATE

        #region START CONDITIONS

        internal static void AddStartCondition(DialogEntry dialog, DialogCondition condition)
        {
            if (dialog == null) throw new InvalidOperationException("Dialog is missing.");
            if (condition == null) throw new InvalidOperationException("Condition is missing.");
            if (dialog.isLocked) throw new InvalidOperationException("Dialog is locked. Unlock it before editing start conditions.");
            if (dialog.conditions == null) dialog.conditions = new List<DialogCondition>();

            dialog.conditions.Add(condition);
        }

        internal static void RemoveStartCondition(DialogEntry dialog, int index)
        {
            if (dialog == null) throw new InvalidOperationException("Dialog is missing.");
            if (dialog.isLocked) throw new InvalidOperationException("Dialog is locked. Unlock it before editing start conditions.");
            if (dialog.conditions == null) throw new InvalidOperationException("Condition index is out of range.");
            if (index < 0 || index >= dialog.conditions.Count) throw new InvalidOperationException("Condition index is out of range.");

            dialog.conditions.RemoveAt(index);
        }

        internal static void ToggleStartLogic(DialogEntry dialog)
        {
            if (dialog == null) throw new InvalidOperationException("Dialog is missing.");
            if (dialog.isLocked) throw new InvalidOperationException("Dialog is locked. Unlock it before editing start conditions.");

            dialog.isAndLogic = !dialog.isAndLogic;
        }

        #endregion START CONDITIONS

        #region LOOKUP

        private static DialogEntry Find(DialogDatabase database, string name)
        {
            if (database?.Dialogs == null || string.IsNullOrEmpty(name)) return null;

            for (int i = 0; i < database.Dialogs.Count; i++)
            {
                DialogEntry dialog = database.Dialogs[i];

                if (dialog != null && dialog.dialogName == name) return dialog;
            }

            return null;
        }

        #endregion LOOKUP
    }
}
#endif