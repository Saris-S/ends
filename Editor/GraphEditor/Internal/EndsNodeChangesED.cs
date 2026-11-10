#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary> ENDS. Projects saved and working snapshots through the shared typed schemas, produces a dependency tree and its flat leaves without mutating models or owning session state. </summary>
    internal static class EndsNodeChangesED
    {
        #region PROJECTION

        private const int ARRAY_MATCH_LIMIT = 262144;
        private static readonly Dictionary<Type, Func<string, Entry>> _projectors = CreateProjectors();
        private static readonly HashSet<object> _combinedGroups = new HashSet<object> { AnimationSchema.Position };
        private static readonly Dictionary<object, string> _rowLabels = new Dictionary<object, string>
        {
            { ChoiceOptionSchema, "Option" },
            { DialogConditionSchema, "Condition" },
            { ChoiceConditionSchema, "Condition" },
            { ChoiceConsequenceSchema, "Consequence" },
            { AnimationSchema, "Animation" }
        };
        private static readonly Dictionary<object, string> _fieldLabels = new Dictionary<object, string>
        {
            { ConditionNodeSchema.TrueNodeId, "Next node (True)" },
            { ConditionNodeSchema.FalseNodeId, "Next node (False)" },
            { NodeSchema.Position, "Position X/Y" }
        };
        private static readonly Dictionary<object, string> _emptyFieldTexts = new Dictionary<object, string>
        {
            { AnimationSchema.Character, "Not selected" },
            { AnimationSchema.Emotion, "Not selected" },
            { LineNodeSchema.NameColor, "Not selected" },
            { StartNodeSchema.NextNodeId, "Not connected" },
            { LineNodeSchema.NextNodeId, "Not connected" },
            { ChoiceOptionSchema.NextNodeId, "Not connected" },
            { ConditionNodeSchema.TrueNodeId, "Not connected" },
            { ConditionNodeSchema.FalseNodeId, "Not connected" }
        };

        /// <summary> ENDS. Builds changed branches and logical field leaves for a registered model, returns false for catalogs outside the node schema so their existing adapter can handle them. </summary>
        internal static bool TryBuild(Type modelType, string before, string after, out EndsMcpJsonED tree, out EndsMcpJsonED fields)
        {
            tree = EndsMcpJsonED.Arr();
            fields = EndsMcpJsonED.Arr();

            if (modelType == null || !_projectors.TryGetValue(modelType, out Func<string, Entry> project)) return false;

            Entry original = project(before);
            Entry current = project(after);

            if (original == null || current == null)
            {
                if (original != null || current != null) tree.Add(CreateChange(original, current, "", "Content"));
            }
            else tree = CompareMembers(original.Children, current.Children, "");

            Flatten(tree, fields);

            return true;
        }

        /// <summary> ENDS. Compares caller-owned in-memory models using any schema, including extensions, no registration, snapshot persistence or per-model cases are required. </summary>
        internal static EndsMcpJsonED Compare<TModel>(NodeModelDefinition<TModel> structure, TModel original, TModel current) where TModel : class
        {
            EndsMcpJsonED originalJson = null;
            EndsMcpJsonED currentJson = null;

            if (original != null) originalJson = EndsMcpJsonED.Parse(EditorJsonUtility.ToJson(original));
            if (current != null) currentJson = EndsMcpJsonED.Parse(EditorJsonUtility.ToJson(current));

            Entry before = Project(structure, original, originalJson);
            Entry after = Project(structure, current, currentJson);

            if (before != null && after != null) return CompareMembers(before.Children, after.Children, "");

            EndsMcpJsonED result = EndsMcpJsonED.Arr();

            if (before != null || after != null) result.Add(CreateChange(before, after, "", "Content"));

            return result;
        }

        private static Dictionary<Type, Func<string, Entry>> CreateProjectors()
        {
            var visitor = new ProjectorRegistration();
            DialogSchema.VisitDefinitions(visitor);

            return visitor.Projectors;
        }

        private sealed class ProjectorRegistration : NodeDefinitionVisitor
        {
            /// <summary> ENDS. Holds one typed snapshot reader per reachable schema model, discovered from the root rather than maintained as a second model list. </summary>
            internal readonly Dictionary<Type, Func<string, Entry>> Projectors = new Dictionary<Type, Func<string, Entry>>();

            /// <summary> ENDS. Registers a typed snapshot reader without creating data, private storage names and Unity object references remain handled by Unity's snapshot codec. </summary>
            internal override void VisitModel<TModel>(NodeModelDefinition<TModel> structure)
            {
                Projectors.Add(typeof(TModel), json =>
                {
                    if (json == null) return null;

                    TModel model = Activator.CreateInstance<TModel>();
                    EditorJsonUtility.FromJsonOverwrite(json, model);

                    return Project(structure, model, EndsMcpJsonED.Parse(json));
                });
            }
        }

        private sealed class Entry
        {
            /// <summary> ENDS. Identifies the shared definition within one owner model, independent of its display label. </summary>
            internal readonly object Definition;
            /// <summary> ENDS. Retains the typed snapshot owner for comparisons delegated to its definition. </summary>
            internal readonly object Model;
            /// <summary> ENDS. Compares the typed owners through the original field definition without formatting values. </summary>
            internal readonly Func<Entry, bool> SameValue;
            /// <summary> ENDS. Compares interpretation through transitive Meaning dependencies of the same typed owner, including sources outside a logical group. </summary>
            internal readonly Func<Entry, bool> SameMeaning;
            /// <summary> ENDS. Retains the physical storage member or existing row key for diagnostic report paths. </summary>
            internal readonly string Name;
            /// <summary> ENDS. Retains a field caption or an object header containing its existing identity, display text never supplies the comparison key. </summary>
            internal readonly string Label;
            /// <summary> ENDS. Retains the serialized snapshot value for MCP report consumers. </summary>
            internal readonly EndsMcpJsonED Value;
            /// <summary> ENDS. Retains the formatted atomic value, comparison never uses this text. </summary>
            internal readonly string Text;
            /// <summary> ENDS. Retains an existing collection key, null means positional row matching. </summary>
            internal readonly string Key;
            /// <summary> ENDS. Distinguishes ordered row containers from nested object boundaries. </summary>
            internal readonly bool Collection;
            /// <summary> ENDS. Identifies a collection item as an independently addressable object, collection containers and dependency groups are not rows. </summary>
            internal readonly bool Row;
            /// <summary> ENDS. Marks one logical value counted as a single change, including grouped coordinates. </summary>
            internal readonly bool Atom;
            /// <summary> ENDS. Contains projected active members or collection rows owned by this entry. </summary>
            internal readonly List<Entry> Children = new List<Entry>();
            /// <summary> ENDS. Contains same-model grouping sources from schema dependencies and branch selectors. </summary>
            internal readonly List<object> Dependencies = new List<object>();

            /// <summary> ENDS. Captures one projection entry without copying model storage or assigning identity to rows. </summary>
            internal Entry(object definition, object model, string name, string label, EndsMcpJsonED value, string text, bool atom = false, bool collection = false, string key = null, Func<Entry, bool> sameValue = null, Func<Entry, bool> sameMeaning = null, bool row = false)
            {
                Definition = definition;
                Model = model;
                Name = name;
                Label = label;
                Value = value;
                Text = text;
                Atom = atom;
                Collection = collection;
                Row = row;
                Key = key;
                SameValue = sameValue;
                SameMeaning = sameMeaning;
            }
        }

        private static Entry Project<TModel>(NodeModelDefinition<TModel> structure, TModel model, EndsMcpJsonED json) where TModel : class
        {
            if (model == null) return null;

            var visitor = new ProjectionVisitor(json);
            structure.Traverse(model, visitor, NodeTraversalMode.Active);

            return visitor.Root;
        }

        private sealed class ProjectionVisitor : NodeTreeVisitor
        {
            private readonly EndsMcpJsonED _json;
            private readonly Stack<Entry> _frames = new Stack<Entry>();
            private readonly Stack<bool> _containers = new Stack<bool>();

            /// <summary> ENDS. Returns the projected root after traversal, nested model boundaries retain collection row identity and same-model dependency ownership. </summary>
            internal Entry Root { get; private set; }

            /// <summary> ENDS. Associates the typed traversal with the same serialized snapshot, JSON supplies original storage values while definitions supply their meaning. </summary>
            internal ProjectionVisitor(EndsMcpJsonED json) => _json = json;

            /// <summary> ENDS. Enters a model boundary, rows use an existing key or their current index, missing models remain explicit without reading their fields. </summary>
            internal override bool EnterModel<TModel>(NodeModelDefinition<TModel> structure, TModel model, NodeAddress address)
            {
                EndsMcpJsonED value = _json;
                string name = "";
                string label = "Content";

                if (_frames.Count > 0) value = _frames.Peek().Value;

                if (address.Kind == NodeAddressKind.Item)
                {
                    name = address.Index.ToString(CultureInfo.InvariantCulture);
                    label = "#" + (address.Index + 1);

                    if (model is DialogBlockData block && _rowLabels.TryGetValue(structure, out string rowLabel)) label = rowLabel + " " + (block.BlockId ?? label);

                    if (address.HasStableKey && !(model is DialogBlockData))
                    {
                        name = address.Key;
                        label = address.Key;
                    }

                    if (ReferenceEquals(structure, LineTranslationSchema) || ReferenceEquals(structure, ChoiceTranslationSchema)) label = "Language: " + FormatValue(address.Key);

                    value = value?.Items[address.Index];
                }

                var entry = new Entry(structure, model, name, label, value, null, key: address.Key, row: address.Kind == NodeAddressKind.Item);

                if (_frames.Count == 0) Root = entry;
                else _frames.Peek().Children.Add(entry);

                _frames.Push(entry);

                return model != null;
            }

            /// <summary> ENDS. Balances the entered model frame even when a null instance prevented child traversal. </summary>
            internal override void ExitModel<TModel>(NodeModelDefinition<TModel> structure, TModel model, NodeAddress address) => _frames.Pop();

            /// <summary> ENDS. Projects atomic fields and logical values, transparent variants contribute selector dependencies, nested boundaries remain traversable without model-specific code. </summary>
            internal override bool EnterElement<TModel>(NodeElementDefinition<TModel> element, TModel model, NodeAddress address)
            {
                if (element.Role == FieldRole.Diagnostic || (element.Role == FieldRole.Identity && model is DialogBlockData))
                {
                    _containers.Push(false);

                    return false;
                }

                Entry owner = _frames.Peek();

                bool transparent = element.SourceMember == null && !(element is NodeVariableValueDefinition<TModel>) && !(element is NodeGroupDefinition<TModel>);

                if (transparent)
                {
                    _containers.Push(false);

                    return true;
                }

                string name = StorageName<TModel>(element);
                EndsMcpJsonED value = owner.Value?.Get(name);
                string text = null;
                bool atom = false;
                bool collection = false;

                if (element is NodeVariableValueDefinition<TModel> logical)
                {
                    var stored = (INodeStoredField<TModel>)logical.ActiveField(model);
                    value = owner.Value?.Get(StorageName(logical.ActiveField(model)));
                    text = FormatValue(stored.ReadStoredValue(model));
                    atom = true;
                }
                else if (element is NodeGroupDefinition<TModel> && _combinedGroups.Contains(element))
                {
                    value = EndsMcpJsonED.Obj();
                    var parts = new List<string>();

                    foreach (NodeElementDefinition<TModel> child in element.ActiveChildren(model))
                    {
                        if (!(child is INodeStoredField<TModel> stored)) throw new InvalidOperationException("An atomic group must contain stored fields: " + element.Name);

                        value.Set(child.Name, owner.Value?.Get(StorageName(child)) ?? EndsMcpJsonED.Null());
                        parts.Add(FormatValue(stored.ReadStoredValue(model)));
                    }

                    text = string.Join("/", parts);
                    atom = true;
                }
                else if (element is INodeStoredField<TModel> stored)
                {
                    Type type = stored.ValueType;
                    collection = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
                    atom = !collection && (type.IsValueType || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type));

                    if (atom) text = FormatValue(stored.ReadStoredValue(model), element);
                }

                if (!atom && element.SourceMember == null) value = owner.Value;

                string label = element.Label;

                if (_fieldLabels.TryGetValue(element, out string fieldLabel)) label = fieldLabel;
                if (ReferenceEquals(element, AnimationSchema.Position)) label += " X/Y";

                var entry = new Entry(element, model, name, label, value, text, atom, collection, sameValue: other => element.HasSameValue(model, (TModel)other.Model),
                    sameMeaning: other => HasSameMeaning(element, model, (TModel)other.Model));

                foreach (NodeDependencyDefinition<TModel> dependency in element.Dependencies)
                {
                    entry.Dependencies.Add(dependency.Source);

                }

                // A selected branch's content depends on its selector, even though the branch itself has no stored member.
                NodeElementDefinition<TModel> parent = element.Parent;

                while (parent != null)
                {
                    if (parent is INodeVariantDefinition<TModel> variant && !ReferenceEquals(variant.Selector, element)) entry.Dependencies.Add(variant.Selector);

                    parent = parent.Parent;
                }

                owner.Children.Add(entry);
                _frames.Push(entry);
                _containers.Push(true);

                return !atom;
            }

            /// <summary> ENDS. Leaves only frames actually created for an element, transparent logical branches and skipped diagnostics do not own projection frames. </summary>
            internal override void ExitElement<TModel>(NodeElementDefinition<TModel> element, TModel model, NodeAddress address)
            {
                if (!_containers.Pop()) return;

                Entry entry = _frames.Pop();

                if (element is NodeGroupDefinition<TModel> && !entry.Atom)
                {
                    var contained = new HashSet<object>();

                    CollectContained(entry, contained);
                    CollectExternalDependencies(entry, entry, contained);
                }
            }
        }

        private static string StorageName<TModel>(NodeElementDefinition<TModel> element) where TModel : class
        {
            if (element.SourceMember != null && EndsDialogRulesED.Models.TryGetValue(typeof(TModel), out EndsDialogRulesED.ModelField[] fields))
            {
                foreach (EndsDialogRulesED.ModelField field in fields)
                {
                    if (field.Name == element.Name) return field.Source;
                }
            }

            return element.Name;
        }

        private static bool HasSameMeaning<TModel>(NodeElementDefinition<TModel> element, TModel original, TModel current) where TModel : class
        {
            foreach (NodeDependencyDefinition<TModel> dependency in element.Dependencies)
            {
                if (dependency.Kind != DependencyKind.Meaning) continue;
                if (!dependency.Source.HasSameValue(original, current) || !HasSameMeaning(dependency.Source, original, current)) return false;
            }

            return true;
        }

        private static void CollectContained(Entry entry, HashSet<object> contained)
        {
            contained.Add(entry.Definition);

            foreach (Entry child in entry.Children)
            {
                if (ReferenceEquals(child.Model, entry.Model)) CollectContained(child, contained);
            }
        }

        private static void CollectExternalDependencies(Entry owner, Entry entry, HashSet<object> contained)
        {
            foreach (Entry child in entry.Children)
            {
                if (!ReferenceEquals(child.Model, owner.Model)) continue;

                foreach (object source in child.Dependencies)
                {
                    if (!contained.Contains(source) && !owner.Dependencies.Contains(source)) owner.Dependencies.Add(source);
                }

                CollectExternalDependencies(owner, child, contained);
            }
        }

        private static string FormatValue(object value, object definition = null)
        {
            if (definition != null && (value == null || value is string empty && empty.Length == 0) && _emptyFieldTexts.TryGetValue(definition, out string emptyText)) return emptyText;
            if (value == null) return "Not set";
            if (value is string text)
            {
                if (string.IsNullOrEmpty(text)) return "Empty text";

                return text;
            }

            if (value is bool flag)
            {
                if (flag) return "True";

                return "False";
            }

            if (value is Enum) return value.ToString();
            if (value is float number) return number.ToString("G7", CultureInfo.InvariantCulture);
            if (value is Vector2 position) return  position.x.ToString("G7", CultureInfo.InvariantCulture) + "/" + position.y.ToString("G7", CultureInfo.InvariantCulture);
            if (value is Color color) return "#" + ColorUtility.ToHtmlStringRGBA(color);
            if (value is UnityEngine.Object asset)
            {
                if (asset == null) return "Not assigned";

                return asset.name + " (" + AssetDatabase.GetAssetPath(asset) + ")";
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        #endregion PROJECTION

        #region DEPENDENCY TREE

        private static EndsMcpJsonED CompareMembers(List<Entry> original, List<Entry> current, string path)
        {
            var before = new Dictionary<object, Entry>();
            var after = new Dictionary<object, Entry>();
            var ordered = new List<Entry>(current);
            var changes = new Dictionary<object, EndsMcpJsonED>();

            foreach (Entry entry in original)
            {
                before.Add(entry.Definition, entry);
            }

            foreach (Entry entry in current)
            {
                after.Add(entry.Definition, entry);
            }

            foreach (Entry entry in original)
            {
                if (!after.ContainsKey(entry.Definition)) ordered.Add(entry);
            }

            foreach (Entry entry in ordered)
            {
                before.TryGetValue(entry.Definition, out Entry previous);
                after.TryGetValue(entry.Definition, out Entry next);

                EndsMcpJsonED change = CompareEntry(previous, next, Join(path, entry.Name));

                if (change != null) changes.Add(entry.Definition, change);
            }

            // Meaning is compared through typed owners, context only groups edits which already exist.
            foreach (Entry entry in ordered)
            {
                if (!entry.Atom || changes.ContainsKey(entry.Definition) || !before.ContainsKey(entry.Definition) || !after.ContainsKey(entry.Definition)) continue;
                if (before[entry.Definition].SameMeaning(after[entry.Definition])) continue;

                EndsMcpJsonED change = CreateChange(before[entry.Definition], after[entry.Definition], Join(path, entry.Name), entry.Label);
                change.Set("meaningChanged", EndsMcpJsonED.Boolean(true));
                changes.Add(entry.Definition, change);
            }

            EndsMcpJsonED roots = EndsMcpJsonED.Arr();

            foreach (Entry entry in ordered)
            {
                if (!changes.TryGetValue(entry.Definition, out EndsMcpJsonED change)) continue;

                object parent = null;
                int depth = -1;

                foreach (object source in entry.Dependencies)
                {
                    if (!changes.ContainsKey(source) || ReferenceEquals(source, entry.Definition)) continue;

                    int sourceDepth = DependencyDepth(source, ordered, changes);

                    if (sourceDepth <= depth) continue;

                    depth = sourceDepth;
                    parent = source;
                }

                if (parent == null) roots.Add(change);
                else changes[parent].Get("children").Add(change);
            }

            return roots;
        }

        private static int DependencyDepth(object definition, List<Entry> entries, Dictionary<object, EndsMcpJsonED> changes)
        {
            foreach (Entry entry in entries)
            {
                if (!ReferenceEquals(entry.Definition, definition)) continue;

                int depth = 0;

                foreach (object source in entry.Dependencies)
                {
                    if (changes.ContainsKey(source)) depth = Math.Max(depth, 1 + DependencyDepth(source, entries, changes));
                }

                return depth;
            }

            return 0;
        }

        private static EndsMcpJsonED CompareEntry(Entry before, Entry after, string path)
        {
            Entry entry = after ?? before;

            if (before == null || after == null) return CreateChange(before, after, path, entry.Label);

            if (entry.Atom)
            {
                if (before.SameValue(after)) return null;

                return CreateChange(before, after, path, entry.Label);
            }

            if (!entry.Collection && before.Children.Count == 1 && after.Children.Count == 1)
            {
                if (before.Children[0].Model == null && after.Children[0].Model != null) return CreateChange(null, after, path, entry.Label);
                if (before.Children[0].Model != null && after.Children[0].Model == null) return CreateChange(before, null, path, entry.Label);
            }

            EndsMcpJsonED children;

            if (entry.Collection) children = CompareRows(before.Children, after.Children, path);
            else if (entry.Children.Count == 1 && before.Children.Count == 1 && entry.Children[0].Name == "") children = CompareMembers(before.Children[0].Children, after.Children[0].Children, path);
            else children = CompareMembers(before.Children, after.Children, path);

            if (children.Items.Count == 0) return null;

            EndsMcpJsonED result = CreateChange(before, after, path, entry.Label, container: true);
            result.Set("children", children);

            return result;
        }

        private static EndsMcpJsonED CreateChange(Entry before, Entry after, string path, string label, bool container = false)
        {
            EndsMcpJsonED result = EndsMcpJsonED.Obj();
            string status = "Changed";

            if (before == null) status = "Added";
            else if (after == null) status = "Removed";

            result.Set("path", EndsMcpJsonED.Str(path));
            result.Set("label", EndsMcpJsonED.Str(label));
            result.Set("status", EndsMcpJsonED.Str(status));
            result.Set("children", EndsMcpJsonED.Arr());
            DescribeObjectIdentity(result, after ?? before);

            if (container) result.Set("container", EndsMcpJsonED.Boolean(true));
            else
            {
                result.Set("before", before?.Value ?? EndsMcpJsonED.Null());
                result.Set("after", after?.Value ?? EndsMcpJsonED.Null());
                result.Set("beforeText", EndsMcpJsonED.Str(Summary(before)));
                result.Set("afterText", EndsMcpJsonED.Str(Summary(after)));

                Entry content = after ?? before;

                if ((before == null || after == null) && content != null && !content.Atom) result.Set("contents", DescribeContents(content.Children, path));
            }

            return result;
        }

        private static void DescribeObjectIdentity(EndsMcpJsonED item, Entry entry)
        {
            if (entry == null) return;
            if (entry.Collection) item.Set("collection", EndsMcpJsonED.Boolean(true));
            if (!entry.Row) return;

            item.Set("row", EndsMcpJsonED.Boolean(true));

            if (!string.IsNullOrEmpty(entry.Key)) item.Set("id", EndsMcpJsonED.Str(entry.Key));
            if (_rowLabels.TryGetValue(entry.Definition, out string caption) && int.TryParse(entry.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int index)) item.Set("displayLabel", EndsMcpJsonED.Str(caption + " " + (index + 1).ToString(CultureInfo.InvariantCulture)));
            if (ReferenceEquals(entry.Definition, LineTranslationSchema) || ReferenceEquals(entry.Definition, ChoiceTranslationSchema))
            {
                string headerField = StorageName(LineTranslationSchema.LanguageCode);

                if (ReferenceEquals(entry.Definition, ChoiceTranslationSchema)) headerField = StorageName(ChoiceTranslationSchema.LanguageCode);

                item.Set("headerField", EndsMcpJsonED.Str(headerField));
                item.Set("displayLabel", EndsMcpJsonED.Str("Translation " + FormatValue(entry.Key)));
            }
        }

        private static string Summary(Entry entry)
        {
            if (entry == null) return "Not set";
            if (entry.Atom) return entry.Text;

            var parts = new List<string>();

            foreach (Entry child in entry.Children)
            {
                if (child.Collection && child.Children.Count == 0) continue;

                string prefix = child.Label + ": ";

                if (child.Name == "") prefix = "";

                parts.Add(prefix + Summary(child));
            }

            return string.Join("\n", parts);
        }

        private static EndsMcpJsonED DescribeContents(List<Entry> entries, string path)
        {
            EndsMcpJsonED contents = EndsMcpJsonED.Arr();

            foreach (Entry entry in entries)
            {
                if (entry.Collection && entry.Children.Count == 0) continue;

                if (entry.Name == "")
                {
                    foreach (EndsMcpJsonED child in DescribeContents(entry.Children, path).Items)
                    {
                        contents.Add(child);
                    }

                    continue;
                }

                string fieldPath = Join(path, entry.Name);
                EndsMcpJsonED item = EndsMcpJsonED.Obj();
                item.Set("path", EndsMcpJsonED.Str(fieldPath));
                item.Set("label", EndsMcpJsonED.Str(entry.Label));
                item.Set("children", EndsMcpJsonED.Arr());
                DescribeObjectIdentity(item, entry);

                if (entry.Atom || entry.Children.Count == 0) item.Set("valueText", EndsMcpJsonED.Str(Summary(entry)));
                else
                {
                    item.Set("container", EndsMcpJsonED.Boolean(true));
                    item.Set("children", DescribeContents(entry.Children, fieldPath));
                }

                contents.Add(item);
            }

            return contents;
        }

        private static string Join(string path, string name) => path + "/" + name.Replace("~", "~0").Replace("/", "~1");

        /// <summary> ENDS. Collects logical changes once for counts and MCP consumers, container context is excluded and dependent changes retain their individual paths. </summary>
        internal static void Flatten(EndsMcpJsonED tree, EndsMcpJsonED fields)
        {
            foreach (EndsMcpJsonED item in tree.Items)
            {
                if (item.Get("container")?.Bool != true) fields.Add(item);

                Flatten(item.Get("children"), fields);
            }
        }

        #endregion DEPENDENCY TREE

        #region COLLECTION MATCHING

        private static EndsMcpJsonED CompareRows(List<Entry> before, List<Entry> after, string path)
        {
            EndsMcpJsonED result = EndsMcpJsonED.Arr();
            var oldKeys = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var newKeys = new Dictionary<string, Entry>(StringComparer.Ordinal);
            bool keyed = IndexRows(before, oldKeys) && IndexRows(after, newKeys);

            if (keyed)
            {
                foreach (Entry row in after)
                {
                    oldKeys.TryGetValue(row.Key, out Entry previous);
                    AddRow(result, previous, row, path);
                }

                foreach (Entry row in before)
                {
                    if (!newKeys.ContainsKey(row.Key)) AddRow(result, row, null, path);
                }

                var previousOrder = new List<string>();
                var currentOrder = new List<string>();

                foreach (Entry row in before)
                {
                    if (newKeys.ContainsKey(row.Key)) previousOrder.Add(row.Key);
                }

                foreach (Entry row in after)
                {
                    if (oldKeys.ContainsKey(row.Key)) currentOrder.Add(row.Key);
                }

                if (before.Count > 0 && before[0].Model is DialogBlockData)
                {
                    for (int index = 0; index < previousOrder.Count; index++)
                    {
                        previousOrder[index] = oldKeys[previousOrder[index]].Label;
                    }

                    for (int index = 0; index < currentOrder.Count; index++)
                    {
                        currentOrder[index] = oldKeys[currentOrder[index]].Label;
                    }
                }

                AddOrder(result, previousOrder, currentOrder, path);

                return result;
            }

            var oldPrints = new List<string>();
            var newPrints = new List<string>();

            foreach (Entry row in before)
            {
                oldPrints.Add(Fingerprint(row));
            }

            foreach (Entry row in after)
            {
                newPrints.Add(Fingerprint(row));
            }

            if (TryReorder(result, before, after, oldPrints, newPrints, path)) return result;

            if ((long)before.Count * after.Count > ARRAY_MATCH_LIMIT)
            {
                AddRange(result, before, after, 0, before.Count, 0, after.Count, path);

                return result;
            }

            var matches = new int[before.Count + 1, after.Count + 1];

            for (int old = before.Count - 1; old >= 0; old--)
            {
                for (int next = after.Count - 1; next >= 0; next--)
                {
                    if (oldPrints[old] == newPrints[next] && SameContent(before[old], after[next])) matches[old, next] = 1 + matches[old + 1, next + 1];
                    else matches[old, next] = Math.Max(matches[old + 1, next], matches[old, next + 1]);
                }
            }

            int i = 0;
            int j = 0;
            int oldStart = 0;
            int newStart = 0;

            while (i < before.Count && j < after.Count)
            {
                if (oldPrints[i] == newPrints[j] && SameContent(before[i], after[j]))
                {
                    AddRange(result, before, after, oldStart, i, newStart, j, path);
                    i++;
                    j++;
                    oldStart = i;
                    newStart = j;
                }
                else if (matches[i + 1, j] >= matches[i, j + 1]) i++;
                else j++;
            }

            AddRange(result, before, after, oldStart, before.Count, newStart, after.Count, path);

            return result;
        }

        private static bool IndexRows(List<Entry> rows, Dictionary<string, Entry> index)
        {
            foreach (Entry row in rows)
            {
                if (string.IsNullOrEmpty(row.Key) || index.ContainsKey(row.Key)) return false;

                index.Add(row.Key, row);
            }

            return true;
        }

        private static string Fingerprint(Entry entry)
        {
            if (entry.Atom) return entry.Definition.GetType().FullName + ":" + (entry.Value?.Write() ?? "null");

            var parts = new List<string>();

            foreach (Entry child in entry.Children)
            {
                parts.Add(child.Name + ":" + Fingerprint(child));
            }

            return entry.Key + ":" + string.Join("|", parts);
        }

        private static bool SameContent(Entry before, Entry after)
        {
            if (!ReferenceEquals(before.Definition, after.Definition)) return false;
            if (!string.Equals(before.Key, after.Key, StringComparison.Ordinal)) return false;
            if (before.Atom) return before.SameValue(after) && before.SameMeaning(after);
            if (before.Children.Count != after.Children.Count) return false;

            for (int i = 0; i < before.Children.Count; i++)
            {
                if (!SameContent(before.Children[i], after.Children[i])) return false;
            }

            return true;
        }

        private static void AddRange(EndsMcpJsonED result, List<Entry> before, List<Entry> after, int oldStart, int oldEnd, int newStart, int newEnd, string path)
        {
            for (int offset = 0; offset < Math.Max(oldEnd - oldStart, newEnd - newStart); offset++)
            {
                Entry previous = null;
                Entry current = null;

                if (oldStart + offset < oldEnd) previous = before[oldStart + offset];
                if (newStart + offset < newEnd) current = after[newStart + offset];

                AddRow(result, previous, current, path);
            }
        }

        private static void AddRow(EndsMcpJsonED result, Entry before, Entry after, string path)
        {
            Entry row = after ?? before;
            string rowPath = Join(path, row.Name);

            if (before?.Model == null) before = null;
            if (after?.Model == null) after = null;
            if (before == null && after == null) return;

            if (before == null || after == null)
            {
                result.Add(CreateChange(before, after, rowPath, row.Label));

                return;
            }

            EndsMcpJsonED children = CompareMembers(before.Children, after.Children, rowPath);

            if (children.Items.Count == 0) return;

            EndsMcpJsonED change = CreateChange(before, after, rowPath, row.Label, container: true);
            change.Set("children", children);
            result.Add(change);
        }

        private static bool TryReorder(EndsMcpJsonED result, List<Entry> oldRows, List<Entry> newRows, List<string> before, List<string> after, string path)
        {
            if (before.Count != after.Count) return false;

            var indices = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);

            for (int i = 0; i < before.Count; i++)
            {
                if (!indices.TryGetValue(before[i], out Queue<int> positions)) indices.Add(before[i], positions = new Queue<int>());

                positions.Enqueue(i);
            }

            var oldOrder = new List<string>();
            var newOrder = new List<string>();

            for (int i = 0; i < after.Count; i++)
            {
                if (!indices.TryGetValue(after[i], out Queue<int> positions) || positions.Count == 0) return false;

                int oldIndex = positions.Dequeue();

                if (!SameContent(oldRows[oldIndex], newRows[i])) return false;

                oldOrder.Add("#" + (i + 1));
                newOrder.Add("#" + (oldIndex + 1));
            }

            AddOrder(result, oldOrder, newOrder, path);

            return true;
        }

        private static void AddOrder(EndsMcpJsonED result, List<string> before, List<string> after, string path)
        {
            string oldText = string.Join(", ", before);
            string newText = string.Join(", ", after);

            if (oldText == newText) return;

            EndsMcpJsonED change = CreateChange(null, null, Join(path, "$order"), "Order");
            change.Set("status", EndsMcpJsonED.Str("Changed"));
            change.Set("before", EndsMcpJsonED.Str(oldText));
            change.Set("after", EndsMcpJsonED.Str(newText));
            change.Set("beforeText", EndsMcpJsonED.Str(oldText));
            change.Set("afterText", EndsMcpJsonED.Str(newText));
            result.Add(change);
        }

        #endregion COLLECTION MATCHING
    }
}
#endif
