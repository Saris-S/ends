#if UNITY_EDITOR
using System;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsNodeStructureED
    {
        /// <summary> ENDS. Describes what a string field references, policies for validity, renaming and deletion remain in backend consumers. </summary>
        internal enum NodeReferenceKind
        {
            /// <summary> ENDS. A variable name scoped by its declared ConditionVariableType. </summary>
            Variable,
            /// <summary> ENDS. A sprite actor name, independent of speaker name colors. </summary>
            Actor,
            /// <summary> ENDS. A line's name-color entry from the display name catalog. </summary>
            Speaker,
            /// <summary> ENDS. A sprite slot scoped by an actor name. </summary>
            Sprite,
            /// <summary> ENDS. A target dialog name scoped by category policy. </summary>
            Dialog,
            /// <summary> ENDS. A dialog or redirect category from the category catalog. </summary>
            Category,
            /// <summary> ENDS. A translation's language code from the language catalog. </summary>
            Language,
            /// <summary> ENDS. An outgoing local node ID in the current dialog. </summary>
            Node
        }

        /// <summary> ENDS. Identifies graph output roles independently of model storage and preserves existing external port names. </summary>
        internal enum NodePortKind
        {
            /// <summary> ENDS. The single next output on a Start or Line node. </summary>
            Next,
            /// <summary> ENDS. The successful output of a Condition node. </summary>
            True,
            /// <summary> ENDS. The unsuccessful output of a Condition node. </summary>
            False,
            /// <summary> ENDS. An option's output, externally addressed by its current list position. </summary>
            Choice
        }

        /// <summary> ENDS. Describes a reference using existing typed fields from one model, introduces no stored data, identifiers or display-driven member lookup. </summary>
        internal sealed class NodeReferenceDefinition<TModel> where TModel : class
        {
            private readonly Func<TModel, bool> _active;

            /// <summary> ENDS. Selects the actual reference member, the same definition is used by storage, Changes and drawers. </summary>
            internal NodeFieldDefinition<TModel, string> Field { get; }
            /// <summary> ENDS. Selects the reference catalog or local graph namespace, not the operation performed on it. </summary>
            internal NodeReferenceKind Kind { get; }
            /// <summary> ENDS. Supplies a containing catalog name, sprite actor, redirect category or an actor's stored sprite slot when required. </summary>
            internal NodeFieldDefinition<TModel, string> Qualifier { get; }
            /// <summary> ENDS. Supplies the variable namespace type for variable references without inspecting model classes. </summary>
            internal NodeFieldDefinition<TModel, ConditionVariableType> VariableType { get; }
            /// <summary> ENDS. Supplies persisted missing-reference state when the model supports repair diagnostics. </summary>
            internal NodeFieldDefinition<TModel, DialogReferenceState> State { get; }
            /// <summary> ENDS. Supplies the preserved target label used when restoring a previously removed reference. </summary>
            internal NodeFieldDefinition<TModel, string> MissingLabel { get; }
            /// <summary> ENDS. Identifies the output's protocol role, relevant only to local node references. </summary>
            internal NodePortKind Port { get; }

            /// <summary> ENDS. Binds a semantic role to typed fields already owned by the model schema, active limits runtime-facing discovery while Stored discovery also visits inactive references. </summary>
            internal NodeReferenceDefinition(NodeFieldDefinition<TModel, string> field, NodeReferenceKind kind, NodeFieldDefinition<TModel, string> qualifier = null, NodeFieldDefinition<TModel, ConditionVariableType> variableType = null,
                NodeFieldDefinition<TModel, DialogReferenceState> state = null, NodeFieldDefinition<TModel, string> missingLabel = null, NodePortKind port = NodePortKind.Next, Func<TModel, bool> active = null)
            {
                Field = field ?? throw new ArgumentNullException(nameof(field));
                Kind = kind;
                Qualifier = qualifier;
                VariableType = variableType;
                State = state;
                MissingLabel = missingLabel;
                Port = port;
                _active = active;

                if (kind == NodeReferenceKind.Variable && variableType == null) throw new ArgumentException("Variable references require a typed namespace.");
                if ((state == null) != (missingLabel == null)) throw new ArgumentException("Missing-reference state and label must be supplied together.");
            }

            /// <summary> ENDS. Tests semantic activity without deleting hidden storage, consumers choose whether inactive values matter to their operation. </summary>
            internal bool IsActive(TModel model) => Field.IsVisible(model) && (_active == null || _active(model));
        }
    }
}
#endif
