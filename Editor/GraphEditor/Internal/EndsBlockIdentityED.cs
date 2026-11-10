#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary> ENDS. Assigns list-block identities through the stored schema, legacy snapshots receive repeatable keys and subsequent insertions receive fresh keys. </summary>
    internal static class EndsBlockIdentityED
    {
        #region FIELDS

        /// <summary> ENDS. Defines one permanent prefix and counter slot shared by compatible block models, adding a kind never renumbers existing slots. </summary>
        internal sealed class IdentityKind
        {
            /// <summary> ENDS. Returns the readable block prefix, distinct from graph-node prefixes and independent of the block's position or contents. </summary>
            internal string Prefix { get; }
            /// <summary> ENDS. Returns the permanent slot in the dialog's shared next-number array, existing slots must never be reordered or reused. </summary>
            internal int Counter { get; }

            /// <summary> ENDS. Registers the format and storage slot of one block family, allocating an instance does not allocate a block ID. </summary>
            internal IdentityKind(string prefix, int counter)
            {
                Prefix = prefix;
                Counter = counter;
            }

            /// <summary> ENDS. Accepts only the exact prefix and a positive decimal number without signs, whitespace or leading zeros. </summary>
            internal bool TryReadNumber(string identity, out int number)
            {
                number = 0;

                if (string.IsNullOrEmpty(identity) || !identity.StartsWith(Prefix + ".", StringComparison.Ordinal)) return false;

                string digits = identity.Substring(Prefix.Length + 1);

                if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number <= 0) return false;

                return digits == number.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary> ENDS. Identifies choice options, CHN remains the prefix of their enclosing graph nodes. </summary>
        internal static readonly IdentityKind Option = new IdentityKind("OPT", 5);
        /// <summary> ENDS. Identifies start conditions, choice conditions and condition-node rows through one dialog-wide condition counter. </summary>
        internal static readonly IdentityKind Condition = new IdentityKind("CND", 6);
        /// <summary> ENDS. Identifies individual consequences of a choice option. </summary>
        internal static readonly IdentityKind Consequence = new IdentityKind("CNS", 7);
        /// <summary> ENDS. Identifies individual character animations in line nodes. </summary>
        internal static readonly IdentityKind Animation = new IdentityKind("ANM", 8);

        private static readonly Dictionary<Type, IdentityKind> _kinds = new Dictionary<Type, IdentityKind>
        {
            { typeof(ChoiceNode), Option }, { typeof(DialogCondition), Condition }, { typeof(ChoiceCondition), Condition }, { typeof(ChoiceConsequence), Consequence }, { typeof(CharacterActionData), Animation }
        };

        /// <summary> ENDS. Describes one stored block and its structural address captured before migration, no metadata is assigned by reading entries. </summary>
        internal sealed class Entry
        {
            /// <summary> ENDS. Returns the concrete model whose BlockId is read or assigned by identity operations. </summary>
            internal DialogBlockData Block { get; }
            /// <summary> ENDS. Returns the shared number policy registered for this model type. </summary>
            internal IdentityKind Kind { get; }
            /// <summary> ENDS. Returns the schema address, keyed ancestors distinguish legacy IDs copied into different owners. </summary>
            internal NodeAddress Address { get; }

            /// <summary> ENDS. Captures a discovered block, policy and immutable address without copying its mutable model. </summary>
            internal Entry(DialogBlockData block, IdentityKind kind, NodeAddress address)
            {
                Block = block;
                Kind = kind;
                Address = address;
            }
        }

        /// <summary> ENDS. Returns the required shared array length from registered permanent slots, the first five slots remain reserved for graph node types. </summary>
        internal static int CounterCount
        {
            get
            {
                int count = 0;

                foreach (IdentityKind kind in _kinds.Values)
                {
                    count = Math.Max(count, kind.Counter + 1);
                }

                return count;
            }
        }

        /// <summary> ENDS. Resolves the shared identity policy of one registered model, new block families are registered once in this owner. </summary>
        internal static IdentityKind KindFor(DialogBlockData block)
        {
            if (_kinds.TryGetValue(block.GetType(), out IdentityKind kind)) return kind;

            throw new InvalidOperationException("No identity family is registered for " + block.GetType().Name + ".");
        }

        /// <summary> ENDS. Reads a canonical identity from any registered family, migration and validation use the same formats as allocation. </summary>
        internal static bool TryReadIdentity(string identity, out IdentityKind kind, out int number)
        {
            foreach (IdentityKind candidate in _kinds.Values)
            {
                if (!candidate.TryReadNumber(identity, out number)) continue;

                kind = candidate;

                return true;
            }

            kind = null;
            number = 0;

            return false;
        }

        #endregion FIELDS

        #region DISCOVERY AND ALLOCATION

        /// <summary> ENDS. Reads every registered block through the stored schema, including hidden payloads, no per-node switch or list index is used as its identity. </summary>
        internal static IReadOnlyList<Entry> ReadEntries(DialogEntry dialog)
        {
            var visitor = new BlockVisitor();
            DialogSchema.Traverse(dialog, visitor, NodeTraversalMode.Stored);

            return visitor.Entries;
        }

        private sealed class BlockVisitor : NodeTreeVisitor
        {
            /// <summary> ENDS. Collects blocks in stored schema order, callers must not treat the ordering as permanent identity. </summary>
            internal readonly List<Entry> Entries = new List<Entry>();

            /// <summary> ENDS. Discovers block instances at any schema depth, an unregistered block model fails explicitly instead of receiving an unrelated ID. </summary>
            internal override bool EnterModel<TModel>(NodeModelDefinition<TModel> structure, TModel model, NodeAddress address)
            {
                if (!(model is DialogBlockData block)) return true;
                if (!_kinds.TryGetValue(block.GetType(), out IdentityKind kind)) throw new InvalidOperationException("No identity kind is registered for " + block.GetType().Name + ".");

                Entries.Add(new Entry(block, kind, address));

                return true;
            }

            /// <summary> ENDS. Keeps every stored branch traversable, identity discovery does not depend on the currently visible controls. </summary>
            internal override bool EnterElement<TModel>(NodeElementDefinition<TModel> element, TModel model, NodeAddress address) => true;
        }

        /// <summary> ENDS. Reserves a fresh block number through the same session and persisted high-water counters as graph nodes, independent of parent and list position. </summary>
        internal static string Allocate(DialogDatabase database, DialogEntry dialog, IdentityKind kind)
        {
            if (EndsSessionED.TryGetSession(database, out EndsSessionED session)) return session.ReserveBlockId(dialog, kind);

            EndsNodeIdentityED.InitializeDialog(dialog);
            int[] numbers = EndsNodeIdentityED.ReadNumbers(dialog);
            int number = numbers[kind.Counter];
            numbers[kind.Counter] = checked(number + 1);
            dialog.SetNodeIdentityState(dialog.NodeIdentityKey, numbers);

            return kind.Prefix + "." + number.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary> ENDS. Builds a readable address such as CHN.1.OPT.2.CNS.3 from keyed ancestors, the block stores only CNS.3 so moving it does not rewrite its identity. </summary>
        internal static string ReadAddress(DialogEntry dialog, DialogBlockData block)
        {
            foreach (Entry entry in ReadEntries(dialog))
            {
                if (!ReferenceEquals(entry.Block, block)) continue;

                var segments = new Stack<string>();

                for (NodeAddress address = entry.Address; address != null; address = address.Parent)
                {
                    if (address.Kind == NodeAddressKind.Item && address.HasStableKey) segments.Push(address.Key);
                }

                return string.Join(".", segments);
            }

            throw new InvalidOperationException("The block does not belong to this dialog.");
        }

        /// <summary> ENDS. Clears copied block identities at every schema depth, copying a whole dialog preserves local IDs in a separate dialog scope instead. </summary>
        internal static void ClearCopiedIds(DialogNode node)
        {
            var visitor = new BlockVisitor();
            NodeSchema.Traverse(node, visitor, NodeTraversalMode.Stored);

            foreach (Entry entry in visitor.Entries)
            {
                entry.Block.BlockId = null;
            }
        }

        #endregion DISCOVERY AND ALLOCATION

        #region NORMALIZATION

        /// <summary> ENDS. Initializes block IDs through shared dialog counters, legacy keys remain available for coordinated snapshot migration, never saves the database. </summary>
        internal static bool Ensure(DialogDatabase database, bool legacy = false, ISet<string> scopes = null)
        {
            bool changed = false;

            foreach (DialogEntry dialog in database.Dialogs ?? new List<DialogEntry>())
            {
                if (scopes != null && (dialog == null || !scopes.Contains(dialog.NodeIdentityKey))) continue;

                var visitor = new IdentityVisitor(database, dialog, legacy);
                DialogSchema.Traverse(dialog, visitor, NodeTraversalMode.Stored);
                visitor.AssignReadableIds();
                changed |= visitor.Changed;
            }

            return changed;
        }

        /// <summary> ENDS. Upgrades persisted editor snapshots using repeatable legacy identities so saved, working and asset baselines agree after a script reload. </summary>
        internal static string NormalizeSnapshot(string snapshot)
        {
            if (snapshot == null) return null;

            DialogDatabase database = ScriptableObject.CreateInstance<DialogDatabase>();

            try
            {
                EditorJsonUtility.FromJsonOverwrite(snapshot, database);
                Ensure(database, legacy: true);

                return EditorJsonUtility.ToJson(database);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(database);
            }
        }

        private sealed class IdentityVisitor : NodeTreeVisitor
        {
            private readonly DialogDatabase _database;
            private readonly DialogEntry _dialog;
            private readonly bool _legacy;
            private readonly HashSet<string> _blockKeys = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<DialogBlockData> _pending = new List<DialogBlockData>();

            /// <summary> ENDS. Reports metadata assignments made by this traversal so callers can avoid serializing an unchanged database twice. </summary>
            internal bool Changed { get; private set; }

            /// <summary> ENDS. Selects repeatable keys for legacy restoration or fresh keys for newly inserted blocks. </summary>
            internal IdentityVisitor(DialogDatabase database, DialogEntry dialog, bool legacy)
            {
                _database = database;
                _dialog = dialog;
                _legacy = legacy;
            }

            /// <summary> ENDS. Prepares unique temporary keys before collection validation, readable allocation waits until every stored list can be traversed safely. </summary>
            internal override bool EnterElement<TModel>(NodeElementDefinition<TModel> element, TModel model, NodeAddress address)
            {
                if (!(element is INodeStoredField<TModel> storage) || !(storage.ReadStoredValue(model) is IList items)) return true;

                var keys = new HashSet<string>(StringComparer.Ordinal);

                for (int index = 0; index < items.Count; index++)
                {
                    if (!(items[index] is DialogBlockData block)) continue;

                    HashSet<string> owners = keys;

                    if (!_legacy) owners = _blockKeys;
                    if (!string.IsNullOrEmpty(block.BlockId) && owners.Add(block.BlockId)) continue;

                    string key = Guid.NewGuid().ToString("N");

                    if (_legacy) key = LegacyKey(block.GetType().FullName + ":" + address + "/" + index);

                    while (!owners.Add(key))
                    {
                        key = Guid.NewGuid().ToString("N");
                    }

                    block.BlockId = key;

                    if (!_legacy) _pending.Add(block);

                    Changed = true;
                }

                return true;
            }

            /// <summary> ENDS. Replaces temporary keys with shared reservations after all lists are valid, temporary GUIDs never leave a successful normalization. </summary>
            internal void AssignReadableIds()
            {
                foreach (DialogBlockData block in _pending)
                {
                    string key = Allocate(_database, _dialog, KindFor(block));

                    while (!_blockKeys.Add(key))
                    {
                        key = Allocate(_database, _dialog, KindFor(block));
                    }

                    block.BlockId = key;
                }
            }
        }

        private static string LegacyKey(string address)
        {
            const int IDENTITY_BYTES = 16;

            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(address));
                byte[] identity = new byte[IDENTITY_BYTES];
                Array.Copy(bytes, identity, identity.Length);

                return new Guid(identity).ToString("N");
            }
        }

        #endregion NORMALIZATION
    }
}
#endif