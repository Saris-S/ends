#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsNodeIdentityED
    {
        #region FIELDS

        internal const int VERSION = 2;
        /// <summary> ENDS. Stores choice-option reservations after the existing node-type counters without changing NodeType values or node ID prefixes. </summary>
        internal static int OPTION_NUMBER_INDEX => EndsBlockIdentityED.Option.Counter;
        /// <summary> ENDS. Distinguishes a readable option identity from CHN, which identifies its enclosing choice node. </summary>
        internal static string OPTION_PREFIX => EndsBlockIdentityED.Option.Prefix;
        private static readonly Dictionary<NodeType, string> _prefixes = new Dictionary<NodeType, string>
        {
            { NodeType.Start, "STN" }, { NodeType.Line, "LNN" }, { NodeType.Choice, "CHN" }, { NodeType.Condition, "CDN" }, { NodeType.Redirect, "RDN" }
        };
        internal static string IdPattern => "^(" + string.Join("|", _prefixes.Values) + ")\\.[1-9][0-9]*$";

        internal sealed class DialogPlan
        {
            internal string Key;
            internal string Name;
            internal readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal);
            internal readonly Dictionary<string, string> Replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            /// <summary> ENDS. Maps legacy owner-node and block identities to readable option IDs consistently across saved and working snapshots. </summary>
            internal readonly Dictionary<string, string> ChoiceReplacements = new Dictionary<string, string>(StringComparer.Ordinal);
            /// <summary> ENDS. Maps legacy structural block addresses to shared readable IDs consistently across saved and working snapshots. </summary>
            internal readonly Dictionary<string, string> BlockReplacements = new Dictionary<string, string>(StringComparer.Ordinal);
            internal int[] NextNumbers = NewNumbers();
        }

        internal sealed class MigrationPlan
        {
            internal readonly Dictionary<string, DialogPlan> Dialogs = new Dictionary<string, DialogPlan>(StringComparer.Ordinal);
            internal readonly Dictionary<string, string> Legacy = new Dictionary<string, string>(StringComparer.Ordinal);

            internal int ReplacementCount
            {
                get
                {
                    int count = 0;

                    foreach (DialogPlan dialog in Dialogs.Values)
                    {
                        foreach (KeyValuePair<string, string> replacement in dialog.Replacements)
                        {
                            if (replacement.Key != replacement.Value) count++;
                        }
                    }

                    return count;
                }
            }
        }

        #endregion FIELDS

        #region IDENTIFIERS

        internal static string Prefix(NodeType type)
        {
            if (!_prefixes.TryGetValue(type, out string prefix)) throw new InvalidOperationException("Unknown node type.");

            return prefix;
        }

        internal static bool TryReadNumber(string id, out int number)
        {
            number = 0;

            if (string.IsNullOrEmpty(id)) return false;

            int separator = id.IndexOf('.');

            if (separator < 0 || !_prefixes.ContainsValue(id.Substring(0, separator))) return false;

            string digits = id.Substring(separator + 1);

            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number <= 0) return false;

            return digits == number.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool IsReadable(string id, NodeType type)
        {
            return TryReadNumber(id, out _) && id.StartsWith(Prefix(type) + ".", StringComparison.Ordinal);
        }

        /// <summary> ENDS. Reads a canonical OPT.N option identity through the shared block policy, node references retain their existing prefixes. </summary>
        internal static bool TryReadOptionNumber(string id, out int number)
        {
            return EndsBlockIdentityED.Option.TryReadNumber(id, out number);
        }

        internal static string Format(NodeType type, int number)
        {
            return Prefix(type) + "." + number.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool IsTypedReference(string reference)
        {
            if (reference == null) return false;

            foreach (string prefix in _prefixes.Values)
            {
                if (reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        internal static int[] NewNumbers()
        {
            int[] numbers = new int[EndsBlockIdentityED.CounterCount];

            for (int i = 0; i < numbers.Length; i++)
            {
                numbers[i] = 1;
            }

            return numbers;
        }

        internal static int[] ReadNumbers(DialogEntry dialog)
        {
            int[] numbers = NewNumbers();

            if (dialog.NextNodeNumbers != null)
            {
                for (int i = 0; i < numbers.Length && i < dialog.NextNodeNumbers.Count; i++)
                {
                    numbers[i] = Math.Max(numbers[i], dialog.NextNodeNumbers[i]);
                }
            }

            foreach (DialogNode node in dialog.nodes ?? new List<DialogNode>())
            {
                if (node != null && TryReadNumber(node.nodeId, out int number)) numbers[(int)node.NodeType] = Math.Max(numbers[(int)node.NodeType], checked(number + 1));

                foreach (ChoiceNode choice in node?.ChoiceData ?? new List<ChoiceNode>())
                {
                    if (choice != null && TryReadOptionNumber(choice.BlockId, out int choiceNumber)) numbers[OPTION_NUMBER_INDEX] = Math.Max(numbers[OPTION_NUMBER_INDEX], checked(choiceNumber + 1));
                }
            }

            foreach (EndsBlockIdentityED.Entry entry in EndsBlockIdentityED.ReadEntries(dialog))
            {
                if (entry.Kind.TryReadNumber(entry.Block.BlockId, out int blockNumber)) numbers[entry.Kind.Counter] = Math.Max(numbers[entry.Kind.Counter], checked(blockNumber + 1));
                if (entry.Kind == EndsBlockIdentityED.Option && TryReadLegacyOptionNumber(entry.Block.BlockId, out int legacy)) numbers[OPTION_NUMBER_INDEX] = Math.Max(numbers[OPTION_NUMBER_INDEX], checked(legacy + 1));
            }

            return numbers;
        }

        internal static void InitializeDialog(DialogEntry dialog, bool newScope = false)
        {
            string key = dialog.NodeIdentityKey;

            if (newScope || string.IsNullOrEmpty(key)) key = Guid.NewGuid().ToString("N");

            dialog.SetNodeIdentityState(key, ReadNumbers(dialog));
        }

        internal static string ReservationKey(DialogEntry dialog, string id)
        {
            return dialog.NodeIdentityKey + "/" + id;
        }

        internal static string Allocate(DialogDatabase database, DialogEntry dialog, NodeType type)
        {
            if (EndsSessionED.TryGetSession(database, out EndsSessionED session)) return session.ReserveNodeId(dialog, type);

            InitializeDialog(dialog);
            int[] numbers = ReadNumbers(dialog);
            int number = numbers[(int)type];
            numbers[(int)type] = checked(number + 1);
            dialog.SetNodeIdentityState(dialog.NodeIdentityKey, numbers);

            return Format(type, number);
        }

        /// <summary> ENDS. Reserves a fresh option number within its dialog using the same persisted counters and session reservations as node identities. </summary>
        internal static string AllocateOption(DialogDatabase database, DialogEntry dialog)
        {
            return EndsBlockIdentityED.Allocate(database, dialog, EndsBlockIdentityED.Option);
        }

        internal static void RequireFresh(DialogDatabase database, DialogEntry dialog, NodeType type, string id)
        {
            if (!IsReadable(id, type)) throw new InvalidOperationException("Node id must use " + Prefix(type) + ". followed by a positive number without leading zeros.");

            InitializeDialog(dialog);
            TryReadNumber(id, out int number);
            int[] numbers = ReadNumbers(dialog);

            if (number < numbers[(int)type] && !EndsSessionED.IsReservedNodeId(database, dialog, id)) throw new InvalidOperationException("Node number was already allocated for this type in this dialog. Omit id to allocate a fresh identity.");

            numbers[(int)type] = Math.Max(numbers[(int)type], checked(number + 1));
            dialog.SetNodeIdentityState(dialog.NodeIdentityKey, numbers);
        }

        #endregion IDENTIFIERS

        #region MIGRATION

        internal static MigrationPlan BuildPlan(DialogDatabase saved, DialogDatabase working, MigrationPlan previous)
        {
            MigrationPlan plan = new MigrationPlan();

            if (previous != null)
            {
                ReadPlan(WritePlan(previous, true), plan);

                foreach (KeyValuePair<string, string> legacy in previous.Legacy)
                {
                    plan.Legacy.Add(legacy.Key, legacy.Value);
                }
            }
            AppendPlan(working, saved, plan, true);
            AppendPlan(saved, working, plan, false);

            return plan;
        }

        /// <summary>ENDS. Aligns legacy saved dialog keys with their migration plan without changing saved content or node IDs.</summary>
        internal static string NormalizeDialogKeys(string snapshot, MigrationPlan plan)
        {
            if (plan.Dialogs.Count == 0) return snapshot;

            EndsMcpJsonED root = EndsMcpJsonED.Parse(snapshot);
            EndsMcpJsonED fields = root.Get("MonoBehaviour") ?? root;
            bool changed = false;

            foreach (EndsMcpJsonED dialog in fields.Get("_dialogs")?.Items ?? new List<EndsMcpJsonED>())
            {
                if (!string.IsNullOrEmpty(dialog.Get("_nodeIdentityKey")?.Text)) continue;

                string name = dialog.Get("dialogName")?.Text ?? "";
                DialogPlan match = null;

                foreach (DialogPlan candidate in plan.Dialogs.Values)
                {
                    if (!candidate.Names.Contains(name)) continue;
                    if (match != null && match != candidate) throw new InvalidOperationException("Ambiguous legacy dialog identity: " + name + ".");

                    match = candidate;
                }

                if (match == null) continue;

                dialog.Set("_nodeIdentityKey", EndsMcpJsonED.Str(match.Key));
                changed = true;
            }

            if (changed) return root.Write();

            return snapshot;
        }

        private static DialogPlan FindPlan(DialogEntry dialog, MigrationPlan plan)
        {
            if (!string.IsNullOrEmpty(dialog.NodeIdentityKey))
            {
                plan.Dialogs.TryGetValue(dialog.NodeIdentityKey, out DialogPlan exact);

                return exact;
            }

            DialogPlan found = null;

            foreach (DialogPlan candidate in plan.Dialogs.Values)
            {
                if (candidate.Names.Contains(dialog.dialogName ?? "")) return candidate;

                foreach (DialogNode node in dialog.nodes ?? new List<DialogNode>())
                {
                    if (node == null || string.IsNullOrEmpty(node.nodeId)) continue;

                    string oldId = node.nodeId;

                    if (plan.Legacy.TryGetValue(oldId, out string legacy)) oldId = legacy;
                    if (!candidate.Replacements.ContainsKey(oldId) && !candidate.Replacements.ContainsKey(node.nodeId)) continue;
                    if (found != null && found != candidate) return null;

                    found = candidate;

                    break;
                }
            }

            return found;
        }

        private static void AppendPlan(DialogDatabase database, DialogDatabase other, MigrationPlan plan, bool working)
        {
            if (database.NodeIdentityVersion > VERSION) throw new InvalidOperationException("The database uses a newer node identity format.");

            foreach (DialogEntry dialog in database.Dialogs ?? new List<DialogEntry>())
            {
                if (dialog == null) continue;

                DialogPlan scope = null;

                if (!string.IsNullOrEmpty(dialog.NodeIdentityKey)) plan.Dialogs.TryGetValue(dialog.NodeIdentityKey, out scope);
                else if (!working) scope = FindPlan(dialog, plan);
                else
                {
                    foreach (DialogPlan candidate in plan.Dialogs.Values)
                    {
                        if (candidate.Names.Contains(dialog.dialogName ?? "")) scope = candidate;
                    }
                }

                if (scope == null)
                {
                    string key = dialog.NodeIdentityKey;

                    if (string.IsNullOrEmpty(key)) key = Guid.NewGuid().ToString("N");

                    scope = new DialogPlan { Key = key, Name = dialog.dialogName };
                    plan.Dialogs.Add(key, scope);
                }

                scope.Names.Add(dialog.dialogName ?? "");

                if (working) scope.Name = dialog.dialogName;

                HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

                foreach (DialogNode node in dialog.nodes ?? new List<DialogNode>())
                {
                    if (node == null) continue;
                    if (database.NodeIdentityVersion >= VERSION) continue;
                    if (string.IsNullOrWhiteSpace(node.nodeId) || !ids.Add(node.nodeId)) throw new InvalidOperationException("Cannot migrate empty or duplicate node ids within a dialog. Repair ambiguous identities first.");

                    string oldId = node.nodeId;
                    string alias = oldId;

                    if (plan.Legacy.TryGetValue(oldId, out string previousId)) alias = previousId;

                    if (!scope.Replacements.TryGetValue(alias, out string id))
                    {
                        id = Format(node.NodeType, scope.NextNumbers[(int)node.NodeType]);

                        if (!IsReadable(id, node.NodeType)) throw new InvalidOperationException("Migrated node id does not match its type.");

                        TryReadNumber(id, out int number);
                        scope.NextNumbers[(int)node.NodeType] = Math.Max(scope.NextNumbers[(int)node.NodeType], checked(number + 1));
                        scope.Replacements[alias] = id;
                    }

                    scope.Replacements[oldId] = id;
                }

                if (database.NodeIdentityVersion >= VERSION)
                {
                    int[] counters = ReadNumbers(dialog);

                    for (int i = 0; i < counters.Length; i++)
                    {
                        scope.NextNumbers[i] = Math.Max(scope.NextNumbers[i], counters[i]);
                    }
                }

                DialogEntry counterpart = other.Dialogs?.Find(candidate => candidate != null && !string.IsNullOrEmpty(dialog.NodeIdentityKey) && candidate.NodeIdentityKey == dialog.NodeIdentityKey);

                if (counterpart == null && string.IsNullOrEmpty(dialog.NodeIdentityKey)) counterpart = other.Dialogs?.Find(candidate => candidate != null && candidate.dialogName == dialog.dialogName);

                if (counterpart != null)
                {
                    int[] counters = ReadNumbers(counterpart);

                    for (int i = OPTION_NUMBER_INDEX; i < counters.Length; i++)
                    {
                        scope.NextNumbers[i] = Math.Max(scope.NextNumbers[i], counters[i]);
                    }
                }

                AppendBlockPlan(dialog, scope);

                if (database.NodeIdentityVersion >= VERSION) continue;

                foreach (KeyValuePair<string, string> legacy in plan.Legacy)
                {
                    if (scope.Replacements.TryGetValue(legacy.Value, out string target)) scope.Replacements[legacy.Key] = target;
                }
            }
        }

        internal static void ApplyPlan(DialogDatabase database, MigrationPlan plan)
        {
            bool remap = database.NodeIdentityVersion < VERSION;

            foreach (DialogEntry dialog in database.Dialogs ?? new List<DialogEntry>())
            {
                if (dialog == null) continue;

                DialogPlan scope = FindPlan(dialog, plan);

                if (scope == null)
                {
                    InitializeDialog(dialog);

                    continue;
                }

                foreach (EndsBlockIdentityED.Entry entry in EndsBlockIdentityED.ReadEntries(dialog))
                {
                    if (scope.BlockReplacements.TryGetValue(BlockMigrationKey(entry, scope), out string replacement)) entry.Block.BlockId = replacement;
                    else if (entry.Block is ChoiceNode && scope.ChoiceReplacements.TryGetValue(LegacyChoiceMigrationKey(entry), out string legacy)) entry.Block.BlockId = legacy;
                }

                if (remap)
                {
                    foreach (DialogNode node in dialog.nodes ?? new List<DialogNode>())
                    {
                        if (node == null) continue;

                        node.nodeId = Remap(node.nodeId, scope.Replacements);
                        RemapEdges(node, scope.Replacements);
                    }
                }

                int[] numbers = ReadNumbers(dialog);

                for (int i = 0; i < numbers.Length && i < scope.NextNumbers.Length; i++)
                {
                    numbers[i] = Math.Max(numbers[i], scope.NextNumbers[i]);
                }

                dialog.SetNodeIdentityState(scope.Key, numbers);
            }

            database.SetNodeIdentityState(1, VERSION);
        }

        private static void AppendBlockPlan(DialogEntry dialog, DialogPlan scope)
        {
            int[] counters = ReadNumbers(dialog);

            for (int i = 0; i < counters.Length; i++)
            {
                scope.NextNumbers[i] = Math.Max(scope.NextNumbers[i], counters[i]);
            }

            var retained = new HashSet<string>(StringComparer.Ordinal);

            foreach (EndsBlockIdentityED.Entry entry in EndsBlockIdentityED.ReadEntries(dialog))
            {
                string key = BlockMigrationKey(entry, scope);

                if (scope.BlockReplacements.TryGetValue(key, out string existing))
                {
                    if (!retained.Add(existing)) throw new InvalidOperationException("Cannot migrate ambiguous duplicate block identities within one owner.");

                    continue;
                }

                if (entry.Kind.TryReadNumber(entry.Block.BlockId, out _) && retained.Add(entry.Block.BlockId)) continue;

                string target = null;

                if (entry.Kind == EndsBlockIdentityED.Option)
                {
                    scope.ChoiceReplacements.TryGetValue(LegacyChoiceMigrationKey(entry), out target);

                    if (TryReadLegacyOptionNumber(target, out int previousNumber)) target = EndsBlockIdentityED.Option.Prefix + "." + previousNumber.ToString(CultureInfo.InvariantCulture);
                    if (target == null && TryReadLegacyOptionNumber(entry.Block.BlockId, out int legacyNumber)) target = EndsBlockIdentityED.Option.Prefix + "." + legacyNumber.ToString(CultureInfo.InvariantCulture);
                }

                if (target == null || !retained.Add(target))
                {
                    int number = scope.NextNumbers[entry.Kind.Counter];
                    scope.NextNumbers[entry.Kind.Counter] = checked(number + 1);
                    target = entry.Kind.Prefix + "." + number.ToString(CultureInfo.InvariantCulture);
                    retained.Add(target);
                }

                scope.BlockReplacements.Add(key, target);
            }
        }

        private static string BlockMigrationKey(EndsBlockIdentityED.Entry entry, DialogPlan scope)
        {
            var segments = new Stack<NodeAddress>();

            for (NodeAddress address = entry.Address; address.Kind != NodeAddressKind.Root; address = address.Parent)
            {
                segments.Push(address);
            }

            NodeAddress normalized = NodeAddress.Root;
            string nodeKey = null;

            foreach (NodeAddress segment in segments)
            {
                if (segment.Kind == NodeAddressKind.Member)
                {
                    normalized = normalized.Member(segment.Name);

                    continue;
                }

                string key = segment.Key;
                bool node = segment.Parent.Name == DialogSchema.Nodes.Name && segment.Parent.Parent.Kind == NodeAddressKind.Root;

                if (node)
                {
                    nodeKey = key;
                    key = Remap(key, scope.Replacements);
                }
                else if (!ReferenceEquals(segment, entry.Address))
                {
                    string source = normalized.Item(segment.Index, key).ToString();

                    if (scope.BlockReplacements.TryGetValue(source, out string mapped)) key = mapped;
                    else if (segment.Parent.Name == NodeSchema.Options.Name && scope.ChoiceReplacements.TryGetValue(nodeKey + "/" + key, out string choice)) key = choice;

                    if (segment.Parent.Name == NodeSchema.Options.Name && TryReadLegacyOptionNumber(key, out int legacy)) key = EndsBlockIdentityED.Option.Prefix + "." + legacy.ToString(CultureInfo.InvariantCulture);
                }

                normalized = normalized.Item(segment.Index, key);
            }

            return normalized.ToString();
        }

        private static string LegacyChoiceMigrationKey(EndsBlockIdentityED.Entry entry)
        {
            string identity = entry.Block.BlockId;

            if (string.IsNullOrEmpty(identity)) identity = "#" + entry.Address.Index.ToString(CultureInfo.InvariantCulture);

            return entry.Address.Parent.Parent.Key + "/" + identity;
        }


        private static string NormalizeBlockMigrationPath(string path)
        {
            string[] segments = path.Split('/');

            for (int index = 1; index < segments.Length - 1; index++)
            {
                string segment = segments[index];

                if (segments[index - 1] != NodeSchema.Options.Name || !segment.StartsWith("[key:", StringComparison.Ordinal) || !segment.EndsWith("]", StringComparison.Ordinal)) continue;

                string key = Uri.UnescapeDataString(segment.Substring(5, segment.Length - 6));

                if (TryReadLegacyOptionNumber(key, out int number)) segments[index] = "[key:" + OPTION_PREFIX + "." + number.ToString(CultureInfo.InvariantCulture) + "]";
            }

            return string.Join("/", segments);
        }

        private static bool TryReadLegacyOptionNumber(string id, out int number)
        {
            string prefix = "CH.";
            number = 0;

            if (string.IsNullOrEmpty(id)) return false;
            if (id.StartsWith("Choice.", StringComparison.Ordinal)) prefix = "Choice.";
            if (!id.StartsWith(prefix, StringComparison.Ordinal)) return false;

            string digits = id.Substring(prefix.Length);

            return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0 && digits == number.ToString(CultureInfo.InvariantCulture);
        }


        internal static void RemapEdges(DialogNode node, IReadOnlyDictionary<string, string> replacements)
        {
            EndsNodeReferencesED.Visit(NodeSchema, node, reference =>
            {
                if (reference.Kind == NodeReferenceKind.Node) reference.Value = Remap(reference.Value, replacements);
            }, NodeTraversalMode.Stored);
        }

        private static string Remap(string id, IReadOnlyDictionary<string, string> replacements)
        {
            if (!string.IsNullOrEmpty(id) && replacements.TryGetValue(id, out string replacement)) return replacement;

            return id;
        }

        internal static EndsMcpJsonED WritePlan(MigrationPlan plan, bool session = false)
        {
            EndsMcpJsonED root = EndsMcpJsonED.Obj();

            foreach (DialogPlan scope in plan.Dialogs.Values)
            {
                EndsMcpJsonED entry = EndsMcpJsonED.Obj();
                EndsMcpJsonED replacements = EndsMcpJsonED.Obj();

                foreach (KeyValuePair<string, string> replacement in scope.Replacements)
                {
                    replacements.Set(replacement.Key, EndsMcpJsonED.Str(replacement.Value));
                }

                entry.Set("dialog", EndsMcpJsonED.Str(scope.Name));
                entry.Set("replacements", replacements);
                EndsMcpJsonED choices = EndsMcpJsonED.Obj();

                foreach (KeyValuePair<string, string> replacement in scope.ChoiceReplacements)
                {
                    choices.Set(replacement.Key, EndsMcpJsonED.Str(replacement.Value));
                }

                entry.Set("choiceReplacements", choices);
                EndsMcpJsonED blocks = EndsMcpJsonED.Obj();

                foreach (KeyValuePair<string, string> replacement in scope.BlockReplacements)
                {
                    blocks.Set(replacement.Key, EndsMcpJsonED.Str(replacement.Value));
                }

                entry.Set("blockReplacements", blocks);

                if (session)
                {
                    entry.Set("key", EndsMcpJsonED.Str(scope.Key));
                    EndsMcpJsonED names = EndsMcpJsonED.Arr();

                    foreach (string name in scope.Names)
                    {
                        names.Add(EndsMcpJsonED.Str(name));
                    }

                    entry.Set("names", names);
                    root.Set(scope.Key, entry);
                }
                else root.Set(scope.Name ?? "", entry);
            }

            return root;
        }

        internal static void ReadPlan(EndsMcpJsonED json, MigrationPlan plan)
        {
            foreach (string key in json.Keys)
            {
                EndsMcpJsonED entry = json.Get(key);

                if (entry.Type == EndsMcpJsonED.Kind.String)
                {
                    plan.Legacy[key] = EndsDialogGraphContractED.String(entry);

                    continue;
                }

                DialogPlan scope = new DialogPlan { Key = entry.Get("key").Text, Name = entry.Get("dialog").Text };

                foreach (string source in entry.Get("replacements").Keys)
                {
                    scope.Replacements[source] = entry.Get("replacements").Get(source).Text;
                }

                EndsMcpJsonED choices = entry.Get("choiceReplacements");

                if (choices != null)
                {
                    foreach (string source in choices.Keys)
                    {
                        string target = choices.Get(source).Text;

                        if (TryReadLegacyOptionNumber(target, out int legacy)) target = OPTION_PREFIX + "." + legacy.ToString(CultureInfo.InvariantCulture);
                        if (!TryReadOptionNumber(target, out int number)) throw new InvalidOperationException("The choice identity migration contains an invalid target ID.");

                        scope.ChoiceReplacements.Add(source, target);
                        scope.NextNumbers[OPTION_NUMBER_INDEX] = Math.Max(scope.NextNumbers[OPTION_NUMBER_INDEX], checked(number + 1));
                    }
                }

                EndsMcpJsonED blocks = entry.Get("blockReplacements");

                if (blocks != null)
                {
                    foreach (string source in blocks.Keys)
                    {
                        string target = blocks.Get(source).Text;

                        if (TryReadLegacyOptionNumber(target, out int legacy)) target = OPTION_PREFIX + "." + legacy.ToString(CultureInfo.InvariantCulture);
                        if (!EndsBlockIdentityED.TryReadIdentity(target, out EndsBlockIdentityED.IdentityKind kind, out int number)) throw new InvalidOperationException("The block identity migration contains an invalid target ID.");

                        scope.BlockReplacements.Add(NormalizeBlockMigrationPath(source), target);
                        scope.NextNumbers[kind.Counter] = Math.Max(scope.NextNumbers[kind.Counter], checked(number + 1));
                    }
                }

                foreach (EndsMcpJsonED name in entry.Get("names").Items)
                {
                    scope.Names.Add(name.Text);
                }

                foreach (string target in scope.Replacements.Values)
                {
                    if (!TryReadNumber(target, out int number)) continue;

                    foreach (NodeType type in _prefixes.Keys)
                    {
                        if (IsReadable(target, type)) scope.NextNumbers[(int)type] = Math.Max(scope.NextNumbers[(int)type], checked(number + 1));
                    }
                }

                plan.Dialogs.Add(scope.Key, scope);
            }
        }

        internal static string RemapEditorKey(string key, MigrationPlan plan)
        {
            if (string.IsNullOrEmpty(key)) return key;

            foreach (DialogPlan scope in plan.Dialogs.Values)
            {
                foreach (KeyValuePair<string, string> replacement in scope.Replacements)
                {
                    foreach (string name in scope.Names)
                    {
                        string marker = "/" + name + "/" + replacement.Key;
                        int position = key.IndexOf(marker, StringComparison.Ordinal);

                        if (position < 0) continue;

                        int end = position + marker.Length;

                        if (end == key.Length || key[end] == '/' || key[end] == '.') return key.Substring(0, position) + "/" + name + "/" + replacement.Value + key.Substring(end);
                    }
                }
            }

            return key;
        }

        #endregion MIGRATION
    }
}
#endif