#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary> ENDS. Connects typed schemas and elements to specialized IMGUI drawers, contexts supply resources and authorized editing services without imposing a graph window. </summary>
    internal static class EndsNodeDrawersED
    {
        #region FIELDS

        private const int MIN_LOGIC_CONDITION_COUNT = 2;

        #endregion FIELDS

        #region PRESENTATION

        /// <summary> ENDS. Shows the AND/OR control only when multiple conditions can be combined, this UI rule does not hide stored fields from schemas or backend comparisons. </summary>
        internal static bool ShowConditionLogic<TCondition>(IReadOnlyCollection<TCondition> conditions) => conditions != null && conditions.Count >= MIN_LOGIC_CONDITION_COUNT;

        /// <summary> ENDS. Builds a UI-only copy of a node, option or condition change tree, removed cards use their recorded model, the backend report and model remain untouched. </summary>
        internal static EndsMcpJsonED PresentChanges<TModel>(EndsMcpJsonED tree, TModel current) where TModel : class
        {
            EndsMcpJsonED presented = EndsMcpJsonED.Parse(tree.Write());
            var visibility = new Dictionary<string, bool>(StringComparer.Ordinal);
            CollectLogicVisibility(current, "", visibility);
            FilterPresentedChanges(presented, visibility, typeof(TModel));

            return presented;
        }

        private static void CollectLogicVisibility(object model, string path, Dictionary<string, bool> visibility)
        {
            if (model is ChoiceNode option) visibility[path + "/" + ChoiceOptionSchema.AllConditionsRequired.Name] = ShowConditionLogic(option.ChoiceCondition);
            else if (model is ConditionNode condition) visibility[path + "/" + ConditionNodeSchema.AllConditionsRequired.Name] = ShowConditionLogic(condition.DialogConditions);
            else if (model is DialogNode node)
            {
                if (node.NodeType == NodeType.Condition) CollectLogicVisibility(node.ConditionData, path + "/" + NodeSchema.Condition.Name, visibility);
                if (node.NodeType != NodeType.Choice || node.ChoiceData == null) return;

                for (int i = 0; i < node.ChoiceData.Count; i++)
                {
                    CollectLogicVisibility(node.ChoiceData[i], path + "/" + NodeSchema.Options.Name + "/" + i.ToString(CultureInfo.InvariantCulture), visibility);
                }
            }
        }

        private static void FilterPresentedChanges(EndsMcpJsonED tree, Dictionary<string, bool> visibility, Type rootType)
        {
            for (int i = tree.Items.Count - 1; i >= 0; i--)
            {
                EndsMcpJsonED item = tree.Items[i];
                string path = item.Get("path").Text;
                EndsMcpJsonED contents = item.Get("contents");
                Dictionary<string, bool> itemVisibility = visibility;

                if (contents != null && contents.Items.Count > 0)
                {
                    EndsMcpJsonED snapshot = item.Get("after");

                    if (item.Get("status")?.Text == "Removed") snapshot = item.Get("before");

                    Type modelType = null;
                    string optionPrefix = "/" + NodeSchema.Options.Name + "/";

                    if (path == "") modelType = rootType;
                    else if (rootType == typeof(DialogNode) && path == "/" + NodeSchema.Condition.Name) modelType = typeof(ConditionNode);
                    else if (rootType == typeof(DialogNode) && path.StartsWith(optionPrefix, StringComparison.Ordinal) && path.IndexOf('/', optionPrefix.Length) < 0) modelType = typeof(ChoiceNode);

                    if (modelType != null && snapshot?.Type == EndsMcpJsonED.Kind.Object)
                    {
                        object model = Activator.CreateInstance(modelType);
                        EditorJsonUtility.FromJsonOverwrite(snapshot.Write(), model);
                        itemVisibility = new Dictionary<string, bool>(visibility, StringComparer.Ordinal);
                        CollectLogicVisibility(model, path, itemVisibility);
                    }
                }

                if (itemVisibility.TryGetValue(path, out bool visible) && !visible)
                {
                    tree.Items.RemoveAt(i);

                    continue;
                }

                EndsMcpJsonED children = item.Get("children");

                if (contents != null) FilterPresentedChanges(contents, itemVisibility, rootType);
                if (children != null) FilterPresentedChanges(children, itemVisibility, rootType);
                if (item.Get("container")?.Bool == true && children?.Items.Count == 0) tree.Items.RemoveAt(i);
            }
        }

        #endregion PRESENTATION

        #region DRAWER CONTRACT

        /// <summary> ENDS. Pairs model-dependent height in unscaled units with specialized drawing, neither registration nor measurement mutates data or creates GUI resources. </summary>
        internal sealed class Drawer<TModel, TContext> where TModel : class
        {
            private readonly Func<TModel, float> _measure;
            private readonly Action<Rect, TModel, TContext> _draw;
            private readonly Func<TModel, TContext, float> _measureWithContext;

            /// <summary> ENDS. Binds layout measurement and rendering once, the caller chooses the typed context and owns its lifetime. </summary>
            internal Drawer(Func<TModel, float> measure, Action<Rect, TModel, TContext> draw)
            {
                _measure = measure ?? throw new ArgumentNullException(nameof(measure));
                _draw = draw ?? throw new ArgumentNullException(nameof(draw));
            }

            /// <summary> ENDS. Returns unscaled layout height from the same registration used for drawing, callers apply their viewport scale. </summary>
            internal float Measure(TModel model)
            {
                if (_measure == null) throw new InvalidOperationException("This drawer requires a layout context.");

                return _measure(model);
            }

            /// <summary> ENDS. Binds layout that needs owner state, such as an option's node foldouts, without storing an implicit current model in the registry. </summary>
            internal Drawer(Func<TModel, TContext, float> measure, Action<Rect, TModel, TContext> draw)
            {
                _measureWithContext = measure ?? throw new ArgumentNullException(nameof(measure));
                _draw = draw ?? throw new ArgumentNullException(nameof(draw));
            }

            /// <summary> ENDS. Measures with explicit owner state when required, model-only registrations remain compatible with the same call. </summary>
            internal float Measure(TModel model, TContext context)
            {
                if (_measureWithContext != null) return _measureWithContext(model, context);

                return Measure(model);
            }
            /// <summary> ENDS. Draws into explicit scaled bounds using the supplied services, persistence and mutation permissions remain outside this generic API. </summary>
            internal void Draw(Rect bounds, TModel model, TContext context) => _draw(bounds, model, context);
        }

        #endregion DRAWER CONTRACT

        #region CONTROL ADDRESSES

        /// <summary> ENDS. Starts a typed control context at the same root used by schema traversal, display labels and list positions never replace stable IDs. </summary>
        internal static ControlAddress<TModel> Root<TModel>(string dialog) where TModel : class => new ControlAddress<TModel>(EndsAiHighlightED.StructureKey(dialog, NodeAddress.Root));

        /// <summary> ENDS. Identifies one model block and its fields, child contexts follow actual schema boundaries and work for detached editing snapshots too. </summary>
        internal readonly struct ControlAddress<TModel> where TModel : class
        {
            private static readonly Dictionary<NodeElementDefinition<TModel>, string> _fieldPaths = new Dictionary<NodeElementDefinition<TModel>, string>();
            private readonly string _key;

            /// <summary> ENDS. Returns this model's independent background key, child field edits never inherit or overwrite this ownership. </summary>
            internal string Key => _key;

            /// <summary> ENDS. Binds an already resolved structural prefix, consumers normally obtain contexts through Root, Child or Item. </summary>
            internal ControlAddress(string key) => _key = key;

            /// <summary> ENDS. Addresses a typed field using its complete definition ancestry, cached paths cover logical groups and variant branches without a binding table. </summary>
            internal string Field(NodeElementDefinition<TModel> element)
            {
                if (!_fieldPaths.TryGetValue(element, out string path))
                {
                    path = "/" + Uri.EscapeDataString(element.Name);

                    for (NodeElementDefinition<TModel> parent = element.Parent; parent != null; parent = parent.Parent)
                    {
                        path = "/" + Uri.EscapeDataString(parent.Name) + path;
                    }

                    _fieldPaths.Add(element, path);
                }

                if (_key == null) return null;

                return _key + path;
            }

            /// <summary> ENDS. Crosses a typed object member into its child model, the same definitions are used by the backend visitor. </summary>
            internal ControlAddress<TChild> Child<TChild, TStructure>(NodeObjectDefinition<TModel, TChild, TStructure> element) where TChild : class where TStructure : NodeModelDefinition<TChild> => new ControlAddress<TChild>(Field(element));

            /// <summary> ENDS. Addresses a collection row by its declared identity, unkeyed rows use their position, no ID is allocated by drawing. </summary>
            internal ControlAddress<TItem> Item<TItem, TStructure>(NodeCollectionDefinition<TModel, TItem, TStructure> element, TItem item, int index) where TItem : class where TStructure : NodeModelDefinition<TItem>
            {
                string key = null;

                if (item != null && element.ItemKey != null) key = element.ItemKey.Read(item);

                string row = "[index:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";

                if (!string.IsNullOrEmpty(key)) row = "[key:" + Uri.EscapeDataString(key) + "]";

                return new ControlAddress<TItem>(Field(element) + "/" + row);
            }
        }

        #endregion CONTROL ADDRESSES

        #region TYPED REGISTRATION

        /// <summary> ENDS. Registers model and element drawers by their exact shared definitions, labels and string paths never select an implementation. </summary>
        internal sealed class Registry<TModel, TContext> where TModel : class
        {
            private readonly Dictionary<NodeModelDefinition<TModel>, Drawer<TModel, TContext>> _models = new Dictionary<NodeModelDefinition<TModel>, Drawer<TModel, TContext>>();
            private readonly Dictionary<NodeElementDefinition<TModel>, Drawer<TModel, TContext>> _elements = new Dictionary<NodeElementDefinition<TModel>, Drawer<TModel, TContext>>();

            /// <summary> ENDS. Registers a complete typed model drawer, duplicate registration fails rather than silently replacing built-in behavior. </summary>
            internal void Register(NodeModelDefinition<TModel> definition, Drawer<TModel, TContext> drawer)
            {
                if (drawer == null) throw new ArgumentNullException(nameof(drawer));

                _models.Add(definition, drawer);
            }

            /// <summary> ENDS. Registers a typed field, group or branch drawer through the same contract, no new model type is needed for a branch of an existing model. </summary>
            internal void Register(NodeElementDefinition<TModel> definition, Drawer<TModel, TContext> drawer)
            {
                if (drawer == null) throw new ArgumentNullException(nameof(drawer));

                _elements.Add(definition, drawer);
            }

            /// <summary> ENDS. Resolves a complete model's registered drawer, missing registrations fail explicitly at the composition boundary. </summary>
            internal Drawer<TModel, TContext> Get(NodeModelDefinition<TModel> definition) => _models[definition];
            /// <summary> ENDS. Resolves the actual element or selected branch's drawer without inspecting an enum, field name or display caption. </summary>
            internal Drawer<TModel, TContext> Get(NodeElementDefinition<TModel> definition) => _elements[definition];

            /// <summary> ENDS. Tests whether a schema member has specialized rendering, allows a host to compose visible payloads without a second node-type dispatch table. </summary>
            internal bool TryGet(NodeElementDefinition<TModel> definition, out Drawer<TModel, TContext> drawer) => _elements.TryGetValue(definition, out drawer);
        }

        #endregion TYPED REGISTRATION
    }
}
#endif
