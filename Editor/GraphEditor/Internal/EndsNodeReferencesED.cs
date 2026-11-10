#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary> ENDS. Discovers references and ports through the shared typed tree, consumers retain validation, mutation, persistence and external protocol policies. </summary>
    internal static class EndsNodeReferencesED
    {
        private static readonly Dictionary<(object element, NodeReferenceKind? kind), bool> _referenceBranches = new Dictionary<(object element, NodeReferenceKind? kind), bool>();

        /// <summary> ENDS. Binds one described field to an actual model and its owners, setters write caller-owned data and never save or create a session. </summary>
        internal sealed class Reference
        {
            private readonly Func<string> _read;
            private readonly Action<string> _write;
            private readonly Func<string> _readQualifier;
            private readonly Action<string> _writeQualifier;
            private readonly Func<DialogReferenceState> _readState;
            private readonly Action<DialogReferenceState> _writeState;
            private readonly Func<string> _readMissing;
            private readonly Action<string> _writeMissing;

            /// <summary> ENDS. Identifies the catalog or local graph target of this reference. </summary>
            internal NodeReferenceKind Kind { get; }
            /// <summary> ENDS. Retains the exact described model for specialized domain policies without rediscovering its location. </summary>
            internal object Model { get; }
            /// <summary> ENDS. Retains the enclosing dialog when discovery starts at DialogSchema. </summary>
            internal DialogEntry Dialog { get; }
            /// <summary> ENDS. Retains the enclosing graph node even when the field belongs to a nested option or condition. </summary>
            internal DialogNode Node { get; }
            /// <summary> ENDS. Supplies the immediate list for sibling uniqueness and operator rules, null for a root or unlisted model. </summary>
            internal IList Siblings { get; }
            /// <summary> ENDS. Retains the stable field address, current indices remain available for diagnostics and external indexed ports. </summary>
            internal NodeAddress Address { get; }
            /// <summary> ENDS. Retains the stable owner address independently of readable labels and field roles. </summary>
            internal NodeAddress OwnerAddress { get; }
            /// <summary> ENDS. Supplies the existing external port name for local node references, empty for other kinds. </summary>
            internal string Port { get; }
            /// <summary> ENDS. Supplies the typed output role for a local node reference, other reference kinds have no graph port. </summary>
            internal NodePortKind? PortKind { get; }
            /// <summary> ENDS. Maps the declared output role to the existing graph selection index, option indices follow current list order while their addresses retain stable IDs. </summary>
            internal int PortIndex
            {
                get
                {
                    switch (PortKind)
                    {
                        case NodePortKind.True: return 0;
                        case NodePortKind.False: return 1;
                        case NodePortKind.Choice: return OwnerAddress.Index;
                        default: return -1;
                    }
                }
            }
            /// <summary> ENDS. Supplies a variable reference's declared type, absent for non-variable references. </summary>
            internal ConditionVariableType? VariableType { get; }
            /// <summary> ENDS. Reports semantic activity separately from retained stored values. </summary>
            internal bool Active { get; }
            /// <summary> ENDS. Reads or changes this actual string member using the schema's compiled typed accessors. </summary>
            internal string Value { get => _read(); set => _write(value); }
            /// <summary> ENDS. Reads the associated namespace qualifier without assuming a particular model type. </summary>
            internal string Qualifier => _readQualifier?.Invoke();
            /// <summary> ENDS. Reads persisted diagnostic status, references without diagnostic storage are valid by definition. </summary>
            internal DialogReferenceState State => _readState?.Invoke() ?? DialogReferenceState.Valid;
            /// <summary> ENDS. Reads the preserved missing target label, absent when the model has no diagnostic storage. </summary>
            internal string MissingLabel => _readMissing?.Invoke();
            /// <summary> ENDS. Returns the field path relative to its graph node or dialog, current row indices are retained for existing diagnostics. </summary>
            internal string Path => FormatPath(Address);
            /// <summary> ENDS. Returns the corresponding owner path, useful for diagnostics that concern an entire animation. </summary>
            internal string OwnerPath => FormatPath(OwnerAddress);

            /// <summary> ENDS. Captures delegates from an actual typed model and its reference definition, no reflection or display parsing is used. </summary>
            internal Reference(NodeReferenceKind kind, object model, DialogEntry dialog, DialogNode node, IList siblings, NodeAddress ownerAddress, NodeAddress address, string port, NodePortKind portKind, ConditionVariableType? variableType, bool active,
                Func<string> read, Action<string> write, Func<string> readQualifier, Action<string> writeQualifier, Func<DialogReferenceState> readState, Action<DialogReferenceState> writeState, Func<string> readMissing, Action<string> writeMissing)
            {
                Kind = kind;
                Model = model;
                Dialog = dialog;
                Node = node;
                Siblings = siblings;
                OwnerAddress = ownerAddress;
                Address = address;
                Port = port;

                if (kind == NodeReferenceKind.Node) PortKind = portKind;

                VariableType = variableType;
                Active = active;
                _read = read;
                _write = write;
                _readQualifier = readQualifier;
                _writeQualifier = writeQualifier;
                _readState = readState;
                _writeState = writeState;
                _readMissing = readMissing;
                _writeMissing = writeMissing;
            }

            /// <summary> ENDS. Restores a target and optional qualifier while clearing missing-reference diagnostics, callers enforce domain validity. </summary>
            internal void Restore(string value, string qualifier = null)
            {
                Value = value;

                if (qualifier != null) _writeQualifier?.Invoke(qualifier);

                _writeState?.Invoke(DialogReferenceState.Valid);
                _writeMissing?.Invoke("");
            }

            /// <summary> ENDS. Preserves a removed target label and clears its stored reference, optional qualifier clearing belongs to the caller's deletion policy. </summary>
            internal void MarkMissing(string label, bool clearQualifier = false)
            {
                if (_readState == null || _readMissing == null) throw new InvalidOperationException("This reference has no missing-reference storage.");

                _writeState(DialogReferenceState.Missing);
                _writeMissing(label);
                Value = "";

                if (clearQualifier) _writeQualifier?.Invoke("");
            }
        }

        /// <summary> ENDS. Visits every declared reference in a caller-owned model tree, Active follows selected payloads and semantic activity while Stored includes inactive storage for migration and cleanup. </summary>
        internal static void Visit<TModel>(NodeModelDefinition<TModel> schema, TModel model, Action<Reference> action, NodeTraversalMode mode = NodeTraversalMode.Active, bool includeInactiveReferences = false, NodeReferenceKind? kind = null) where TModel : class
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            schema.Traverse(model, new Visitor(action, mode, includeInactiveReferences, kind), mode);
        }

        /// <summary> ENDS. Visits dialog contents without an independent list of node types or nested collection locations. </summary>
        internal static void Visit(DialogDatabase database, Action<Reference> action, NodeTraversalMode mode = NodeTraversalMode.Active, bool includeInactiveReferences = false, NodeReferenceKind? kind = null)
        {
            if (database?.Dialogs == null) return;

            foreach (DialogEntry dialog in database.Dialogs)
            {
                if (dialog != null) Visit(DialogSchema, dialog, action, mode, includeInactiveReferences, kind);
            }
        }

        /// <summary> ENDS. Resolves a currently existing output using the schema while preserving indexed choice port names as an external compatibility contract. </summary>
        internal static Reference FindPort(DialogNode node, string port)
        {
            Reference found = null;
            Visit(NodeSchema, node, reference =>
            {
                if (reference.Kind == NodeReferenceKind.Node && reference.Port == port) found = reference;
            }, kind: NodeReferenceKind.Node);

            return found;
        }

        /// <summary> ENDS. Resolves an existing output by its graph selection index using declared port roles, invalid branch or option indices do not select another output. </summary>
        internal static Reference FindPort(DialogNode node, int index)
        {
            Reference found = null;
            Visit(NodeSchema, node, reference =>
            {
                if (reference.PortIndex == index) found = reference;
            }, kind: NodeReferenceKind.Node);

            return found;
        }

        private static string FormatPath(NodeAddress address)
        {
            var parts = new List<NodeAddress>();

            for (NodeAddress part = address; part?.Parent != null; part = part.Parent)
            {
                parts.Add(part);
            }

            parts.Reverse();
            int start = 0;

            for (int i = 0; i + 1 < parts.Count; i++)
            {
                if (parts[i].Kind == NodeAddressKind.Member && parts[i].Name == nameof(DialogEntry.nodes) && parts[i + 1].Kind == NodeAddressKind.Item) start = i + 2;
            }

            var path = new StringBuilder();

            for (int i = start; i < parts.Count; i++)
            {
                NodeAddress part = parts[i];

                if (part.Kind == NodeAddressKind.Item) path.Append("[").Append(part.Index).Append("]");
                else
                {
                    if (path.Length > 0) path.Append(".");

                    path.Append(part.Name);
                }
            }

            return path.ToString();
        }

        private sealed class ReferenceDefinitions : NodeDefinitionVisitor
        {
            private readonly NodeReferenceKind? _kind;
            public bool Found;

            public ReferenceDefinitions(NodeReferenceKind? kind) => _kind = kind;

            /// <summary> ENDS. Detects requested reference kinds in nested schema definitions without reading any model values. </summary>
            internal override void VisitModel<TModel>(NodeModelDefinition<TModel> structure)
            {
                foreach (NodeReferenceDefinition<TModel> reference in structure.References)
                {
                    if (!_kind.HasValue || reference.Kind == _kind.Value) Found = true;
                }
            }
        }

        private sealed class Visitor : NodeTreeVisitor
        {
            private readonly Action<Reference> _action;
            private readonly NodeTraversalMode _mode;
            private readonly bool _includeInactiveReferences;
            private readonly NodeReferenceKind? _kind;
            private readonly Stack<(DialogEntry dialog, DialogNode node)> _owners = new Stack<(DialogEntry dialog, DialogNode node)>();
            private readonly Stack<bool> _activity = new Stack<bool>();
            private readonly Stack<IList> _collections = new Stack<IList>();
            private readonly Stack<bool> _enteredCollections = new Stack<bool>();

            /// <summary> ENDS. Allows diagnostic consumers to inspect malformed duplicate IDs instead of failing before validation can report them. </summary>
            internal override bool RequireUniqueKeys => false;

            /// <summary> ENDS. Keeps locked dialog conditions in reference operations, locking hides their editor controls but does not erase their stored catalog references. </summary>
            internal override bool IncludeHiddenElement<TModel>(NodeElementDefinition<TModel> element, TModel model) => model is DialogEntry && ReferenceEquals(element, DialogSchema.Conditions);

            /// <summary> ENDS. Configures one discovery operation, no global state or persistent registry is created. </summary>
            internal Visitor(Action<Reference> action, NodeTraversalMode mode, bool includeInactiveReferences, NodeReferenceKind? kind)
            {
                _action = action;
                _mode = mode;
                _includeInactiveReferences = includeInactiveReferences;
                _kind = kind;
            }

            /// <summary> ENDS. Prunes fields and whole branches that contain no requested references, the immutable schema plan is reused across edits. </summary>
            internal override bool ShouldVisitElement<TModel>(NodeElementDefinition<TModel> element)
            {
                var key = ((object)element, _kind);

                if (_referenceBranches.TryGetValue(key, out bool found)) return found;

                var definitions = new ReferenceDefinitions(_kind);
                element.VisitNestedDefinitions(definitions, new HashSet<object>());
                found = definitions.Found;

                foreach (NodeElementDefinition<TModel> child in element.Children)
                {
                    if (ShouldVisitElement(child)) found = true;
                }

                _referenceBranches.Add(key, found);

                return found;
            }

            /// <summary> ENDS. Emits each model's declared references and carries dialog/node ownership across typed boundaries. </summary>
            internal override bool EnterModel<TModel>(NodeModelDefinition<TModel> schema, TModel model, NodeAddress address)
            {
                DialogEntry dialog = null;
                DialogNode node = null;

                if (_owners.Count > 0) (dialog, node) = _owners.Peek();
                if (model is DialogEntry ownerDialog) dialog = ownerDialog;
                if (model is DialogNode ownerNode) node = ownerNode;

                _owners.Push((dialog, node));

                if (model == null) return false;

                IList siblings = null;

                if (_collections.Count > 0) siblings = _collections.Peek();

                foreach (NodeReferenceDefinition<TModel> definition in schema.References)
                {
                    if (_kind.HasValue && definition.Kind != _kind.Value) continue;

                    bool active = definition.IsActive(model) && (_activity.Count == 0 || _activity.Peek());

                    if (_mode == NodeTraversalMode.Active && !_includeInactiveReferences && !active) continue;

                    string port = "";
                    ConditionVariableType? variableType = null;

                    if (definition.Kind == NodeReferenceKind.Node)
                    {
                        port = definition.Port.ToString().ToLowerInvariant();

                        if (definition.Port == NodePortKind.Choice) port += ":" + address.Index.ToString(CultureInfo.InvariantCulture);
                    }
                    if (definition.VariableType != null) variableType = definition.VariableType.Read(model);

                    Func<DialogReferenceState> stateReader = null;
                    Func<string> missingReader = null;

                    if (definition.State != null) stateReader = () => definition.State.Read(model);
                    if (definition.MissingLabel != null) missingReader = () => definition.MissingLabel.Read(model);

                    _action(new Reference(definition.Kind, model, dialog, node, siblings, address, address.Member(definition.Field.Name), port, definition.Port, variableType, active,
                        () => definition.Field.Read(model), value => definition.Field.Write(model, value),
                        () => definition.Qualifier?.Read(model), value => definition.Qualifier?.Write(model, value),
                        stateReader, value => definition.State?.Write(model, value),
                        missingReader, value => definition.MissingLabel?.Write(model, value)));
                }

                return true;
            }

            /// <summary> ENDS. Restores enclosing ownership after null, skipped or completed models. </summary>
            internal override void ExitModel<TModel>(NodeModelDefinition<TModel> schema, TModel model, NodeAddress address) => _owners.Pop();

            /// <summary> ENDS. Supplies the nearest actual list to sibling rules while relying on schema traversal to discover nested models. </summary>
            internal override bool EnterElement<TModel>(NodeElementDefinition<TModel> element, TModel model, NodeAddress address)
            {
                IList collection = null;

                if (element is INodeStoredField<TModel> stored) collection = stored.ReadStoredValue(model) as IList;

                bool entered = collection != null;
                bool active = _activity.Count == 0 || _activity.Peek();
                _activity.Push(active && (element.IsVisible(model) || IncludeHiddenElement(element, model)));
                _enteredCollections.Push(entered);

                if (entered) _collections.Push(collection);

                return true;
            }

            /// <summary> ENDS. Balances list context at every element boundary without retaining model instances after the walk. </summary>
            internal override void ExitElement<TModel>(NodeElementDefinition<TModel> element, TModel model, NodeAddress address)
            {
                if (_enteredCollections.Pop()) _collections.Pop();

                _activity.Pop();
            }
        }
    }
}
#endif
