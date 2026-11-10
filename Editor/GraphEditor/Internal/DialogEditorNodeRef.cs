#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsNodeRefED
    {
        internal const int ShortLength = 8;

        internal static string ShortRef(string fullNodeId)
        {
            if (string.IsNullOrEmpty(fullNodeId)) return "";
            if (EndsNodeIdentityED.IsTypedReference(fullNodeId)) return fullNodeId;
            if (fullNodeId.Length >= ShortLength) return fullNodeId.Substring(0, ShortLength);

            return fullNodeId;
        }

        internal static bool TryGetDialog(DialogDatabase database, string dialogName, out DialogEntry dialog)
        {
            dialog = null;

            if (database?.Dialogs == null || string.IsNullOrEmpty(dialogName)) return false;

            int count = 0;

            for (int i = 0; i < database.Dialogs.Count; i++)
            {
                DialogEntry entry = database.Dialogs[i];

                if (entry == null || entry.dialogName != dialogName) continue;

                dialog = entry;
                count++;
            }

            if (count != 1) dialog = null;

            return dialog != null;
        }

        internal static bool TryResolveInDialog(DialogEntry dialog, string reference, out DialogNode node, out string error)
        {
            node = null;
            error = null;

            if (dialog?.nodes == null || string.IsNullOrEmpty(reference))
            {
                error = "Node reference is empty.";

                return false;
            }

            for (int i = 0; i < dialog.nodes.Count; i++)
            {
                DialogNode candidate = dialog.nodes[i];

                if (candidate == null || candidate.nodeId != reference) continue;

                node = candidate;

                return true;
            }

            if (EndsNodeIdentityED.IsTypedReference(reference))
            {
                error = "Unknown exact node id " + reference + ". Read the dialog again.";

                return false;
            }

            List<DialogNode> prefixMatches = new List<DialogNode>();

            for (int i = 0; i < dialog.nodes.Count; i++)
            {
                DialogNode candidate = dialog.nodes[i];

                if (candidate == null || string.IsNullOrEmpty(candidate.nodeId)) continue;
                if (!candidate.nodeId.StartsWith(reference, StringComparison.OrdinalIgnoreCase)) continue;

                prefixMatches.Add(candidate);
            }

            if (prefixMatches.Count == 0)
            {
                error = "Unknown node " + reference + ".";

                return false;
            }

            if (prefixMatches.Count > 1)
            {
                error = "Ambiguous node ref " + reference + ". Include the URI-escaped dialog name followed by / and its node id.";

                return false;
            }

            node = prefixMatches[0];

            return true;
        }

        internal static string Address(DialogEntry dialog, string nodeId)
        {
            return Uri.EscapeDataString(dialog.dialogName ?? "") + "/" + nodeId;
        }

        internal static bool TryResolveAddress(DialogDatabase database, string reference, out DialogEntry dialog, out DialogNode node, out string error)
        {
            dialog = null;
            node = null;

            if (!TryResolveInDatabase(database, reference, out string address, out error)) return false;

            int separator = address.LastIndexOf('/');

            if (!TryGetDialog(database, Uri.UnescapeDataString(address.Substring(0, separator)), out dialog)) return false;

            return TryResolveInDialog(dialog, address.Substring(separator + 1), out node, out error);
        }

        internal static bool TryResolveInDatabase(DialogDatabase database, string reference, out string fullNodeId, out string error)
        {
            fullNodeId = null;
            error = null;

            if (database?.Dialogs == null || string.IsNullOrEmpty(reference))
            {
                error = "Node reference is empty.";

                return false;
            }

            int separator = reference.LastIndexOf('/');

            if (separator >= 0)
            {
                string name = Uri.UnescapeDataString(reference.Substring(0, separator));

                if (!TryGetDialog(database, name, out DialogEntry scope))
                {
                    error = "Unknown or ambiguous dialog " + name + ".";

                    return false;
                }

                if (!TryResolveInDialog(scope, reference.Substring(separator + 1), out DialogNode scoped, out error)) return false;

                fullNodeId = Address(scope, scoped.nodeId);

                return true;
            }

            DialogNode found = null;
            DialogEntry foundDialog = null;
            DialogEntry exactDialog = null;
            DialogNode exactMatch = null;
            int matchCount = 0;
            int exactCount = 0;

            for (int d = 0; d < database.Dialogs.Count; d++)
            {
                DialogEntry dialog = database.Dialogs[d];

                if (dialog?.nodes == null) continue;

                for (int n = 0; n < dialog.nodes.Count; n++)
                {
                    DialogNode node = dialog.nodes[n];

                    if (node == null || string.IsNullOrEmpty(node.nodeId)) continue;

                    if (node.nodeId == reference)
                    {
                        exactMatch = node;
                        exactDialog = dialog;
                        exactCount++;
                    }

                    if (EndsNodeIdentityED.IsTypedReference(reference)) continue;

                    if (!node.nodeId.StartsWith(reference, StringComparison.OrdinalIgnoreCase)) continue;

                    found = node;
                    foundDialog = dialog;
                    matchCount++;
                }
            }

            if (exactCount > 0)
            {
                found = exactMatch;
                foundDialog = exactDialog;
                matchCount = exactCount;
            }

            if (matchCount == 0)
            {
                error = "Unknown node " + reference + ".";

                return false;
            }

            if (matchCount > 1)
            {
                error = "Ambiguous node ref " + reference + ". Include the URI-escaped dialog name followed by / and its node id.";

                return false;
            }

            fullNodeId = Address(foundDialog, found.nodeId);

            return true;
        }
    }
}
#endif
