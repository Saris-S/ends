#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary> ENDS. Provides typed tree traversal and structural addresses for the shared node descriptions without implementing presentation, mutation or persistence. </summary>
    internal static partial class EndsNodeStructureED
    {
        #region TYPED TREE VISITOR

        /// <summary> ENDS. Receives reachable typed model and member descriptions without requiring database values, used by adapters that register storage or contracts before instances exist. </summary>
        internal abstract class NodeDefinitionVisitor
        {
            /// <summary> ENDS. Receives each shared model description once per walk, use ElementsDepthFirst for its complete own-model stored members. </summary>
            internal abstract void VisitModel<TModel>(NodeModelDefinition<TModel> structure) where TModel : class;
            /// <summary> ENDS. Receives each logical or stored member including hidden branches, object and collection definitions subsequently expose their referenced schemas. </summary>
            internal virtual void VisitElement<TModel>(NodeElementDefinition<TModel> element) where TModel : class { }
        }

        private static void VisitDefinition<TModel>(NodeModelDefinition<TModel> structure, NodeDefinitionVisitor visitor, HashSet<object> visited) where TModel : class
        {
            if (!visited.Add(structure)) return;

            visitor.VisitModel(structure);

            foreach (NodeElementDefinition<TModel> element in structure.ElementsDepthFirst())
            {
                visitor.VisitElement(element);
                element.VisitNestedDefinitions(visitor, visited);
            }
        }

        /// <summary> ENDS. Distinguishes complete stored content from the currently visible logical tree, neither mode writes or normalizes model data. </summary>
        internal enum NodeTraversalMode
        {
            /// <summary> ENDS. Includes all branches and typed value alternatives, suitable for inspecting complete storage without presentation filtering. </summary>
            Stored,
            /// <summary> ENDS. Includes visible elements and selected alternatives, suitable for presentation consumers rather than snapshot serialization. </summary>
            Active
        }

        /// <summary> ENDS. Receives typed definitions and their actual model instances across nested boundaries, consumers need no per-node switch or reflective member lookup to walk content. </summary>
        internal abstract class NodeTreeVisitor
        {
            /// <summary> ENDS. Requires unique collection keys by default, diagnostic visitors may inspect malformed legacy data without stopping their report. </summary>
            internal virtual bool RequireUniqueKeys => true;

            /// <summary> ENDS. Selects relevant definition subtrees before reading values or allocating addresses, default visitors retain the complete tree. </summary>
            internal virtual bool ShouldVisitElement<TModel>(NodeElementDefinition<TModel> element) where TModel : class => true;

            /// <summary> ENDS. Allows an adapter to include a hidden stored element in Active traversal when its own domain still uses it, defaults preserve visible-tree behavior. </summary>
            internal virtual bool IncludeHiddenElement<TModel>(NodeElementDefinition<TModel> element, TModel model) where TModel : class => false;

            /// <summary> ENDS. Enters a root, nested object or collection row, null instances are reported but never traversed, return false to skip this model's elements. </summary>
            internal virtual bool EnterModel<TModel>(NodeModelDefinition<TModel> structure, TModel model, NodeAddress address) where TModel : class => true;
            /// <summary> ENDS. Leaves a model after its children, also called for null instances and deliberately skipped models. </summary>
            internal virtual void ExitModel<TModel>(NodeModelDefinition<TModel> structure, TModel model, NodeAddress address) where TModel : class { }
            /// <summary> ENDS. Enters a logical block or stored member with its typed owner and address, return false to prune children while still receiving ExitElement. </summary>
            internal abstract bool EnterElement<TModel>(NodeElementDefinition<TModel> element, TModel model, NodeAddress address) where TModel : class;
            /// <summary> ENDS. Leaves an entered element after its descendants, balanced even when its children are skipped or throw. </summary>
            internal virtual void ExitElement<TModel>(NodeElementDefinition<TModel> element, TModel model, NodeAddress address) where TModel : class { }
        }

        private static void TraverseModel<TModel>(NodeModelDefinition<TModel> structure, TModel model, NodeTreeVisitor visitor, NodeTraversalMode mode, NodeAddress address) where TModel : class
        {
            bool descend = visitor.EnterModel(structure, model, address);

            try
            {
                if (!descend || model == null) return;

                foreach (NodeElementDefinition<TModel> member in structure.Members)
                {
                    TraverseElement(member, model, visitor, mode, address);
                }
            }
            finally
            {
                visitor.ExitModel(structure, model, address);
            }
        }

        private static void TraverseElement<TModel>(NodeElementDefinition<TModel> element, TModel model, NodeTreeVisitor visitor, NodeTraversalMode mode, NodeAddress parentAddress) where TModel : class
        {
            if (!visitor.ShouldVisitElement(element)) return;
            if (mode == NodeTraversalMode.Active && !element.IsVisible(model) && !visitor.IncludeHiddenElement(element, model)) return;

            NodeAddress address = parentAddress.Member(element.Name);
            bool descend = visitor.EnterElement(element, model, address);

            try
            {
                if (descend) element.TraverseChildren(model, visitor, mode, address);
            }
            finally
            {
                visitor.ExitElement(element, model, address);
            }
        }

        #endregion TYPED TREE VISITOR

        #region STRUCTURAL ADDRESSES

        /// <summary> ENDS. Identifies an address segment without parsing display strings, Root starts a walk, Member selects a definition and Item selects a collection row. </summary>
        internal enum NodeAddressKind
        {
            /// <summary> ENDS. The start of a tree walk, has no parent or member identity. </summary>
            Root,
            /// <summary> ENDS. A local definition name derived from a selected member or explicitly named logical block. </summary>
            Member,
            /// <summary> ENDS. A collection row with an existing stable key when available, otherwise positional identity. </summary>
            Item
        }

        /// <summary> ENDS. Immutable linked address inside one traversal root, preserves typed segments and existing row keys, never resolves members or changes data by parsing its display text. </summary>
        internal sealed class NodeAddress : IEquatable<NodeAddress>
        {
            private static readonly NodeAddress _root = new NodeAddress(null, NodeAddressKind.Root, null, -1, null);
            private readonly int _hashCode;

            /// <summary> ENDS. Returns the shared empty address, callers associate the traversal root with its database, dialog or snapshot outside this value. </summary>
            internal static NodeAddress Root => _root;
            /// <summary> ENDS. Returns the preceding segment, null only for Root. </summary>
            internal NodeAddress Parent { get; }
            /// <summary> ENDS. Returns whether this segment is Root, Member or Item, no text parsing is required. </summary>
            internal NodeAddressKind Kind { get; }
            /// <summary> ENDS. Returns a member segment's local definition name, null for Root and Item. </summary>
            internal string Name { get; }
            /// <summary> ENDS. Returns the row's current position or -1 for other segments, keyed row equality ignores position to survive reordering. </summary>
            internal int Index { get; }
            /// <summary> ENDS. Returns the row's existing nonempty identity or null, unkeyed rows deliberately remain positional rather than receiving generated IDs. </summary>
            internal string Key { get; }
            /// <summary> ENDS. Reports stable row identity for this segment, false means an Item address changes when its positional row shifts. </summary>
            internal bool HasStableKey => Key != null;

            private NodeAddress(NodeAddress parent, NodeAddressKind kind, string name, int index, string key)
            {
                Parent = parent;
                Kind = kind;
                Name = name;
                Index = index;
                Key = key;
                int identityHash = index;

                if (name != null) identityHash = StringComparer.Ordinal.GetHashCode(name);
                if (key != null) identityHash = StringComparer.Ordinal.GetHashCode(key);

                unchecked
                {
                    int parentHash = 0;

                    if (parent != null) parentHash = parent._hashCode;

                    _hashCode = ((parentHash * 397) ^ (int)kind) * 397 ^ identityHash;
                    if (key != null) _hashCode ^= 0x4B4559;
                }
            }

            /// <summary> ENDS. Appends a definition name as one structural segment, punctuation remains part of the name and never becomes an extra path segment. </summary>
            internal NodeAddress Member(string name)
            {
                if (string.IsNullOrEmpty(name)) throw new ArgumentException("Member name cannot be empty.", nameof(name));

                return new NodeAddress(this, NodeAddressKind.Member, name, -1, null);
            }

            /// <summary> ENDS. Appends a collection row, a nonempty existing key defines identity while Index records its current position, null or empty keys use positional equality. </summary>
            internal NodeAddress Item(int index, string key = null)
            {
                if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
                if (string.IsNullOrEmpty(key)) key = null;

                return new NodeAddress(this, NodeAddressKind.Item, null, index, key);
            }

            /// <summary> ENDS. Compares structural segments and their parents, keyed rows match across reorderings while positional rows match only at the same index. </summary>
            public bool Equals(NodeAddress other)
            {
                if (ReferenceEquals(this, other)) return true;
                if (other == null || Kind != other.Kind || !string.Equals(Name, other.Name, StringComparison.Ordinal)) return false;
                if (!string.Equals(Key, other.Key, StringComparison.Ordinal)) return false;
                if (Key == null && Index != other.Index) return false;

                return Equals(Parent, other.Parent);
            }

            /// <summary> ENDS. Supports structural address equality in ordinary collections without using formatted paths as keys. </summary>
            public override bool Equals(object other) => Equals(other as NodeAddress);
            /// <summary> ENDS. Returns the cached structural hash, keyed row positions are deliberately excluded to agree with equality. </summary>
            public override int GetHashCode() => _hashCode;

            /// <summary> ENDS. Formats an escaped diagnostic path, this text is for inspection and is never the source of member access or structural equality. </summary>
            public override string ToString()
            {
                if (Kind == NodeAddressKind.Root) return "$";
                if (Kind == NodeAddressKind.Member) return Parent + "/" + Uri.EscapeDataString(Name);
                if (Key != null) return Parent + "/[key:" + Uri.EscapeDataString(Key) + "]";

                return Parent + "/[index:" + Index + "]";
            }
        }

        #endregion STRUCTURAL ADDRESSES
    }
}
#endif
