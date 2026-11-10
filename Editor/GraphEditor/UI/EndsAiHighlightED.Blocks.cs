#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsAiHighlightED
    {
        #region STRUCTURAL CONTROL ADDRESS

        /// <summary> ENDS. Addresses any schema element, keyed rows retain their identity across moves. </summary>
        internal static string StructureKey(string dialog, NodeAddress address) => "schema/" + dialog + "/" + address;

        #endregion STRUCTURAL CONTROL ADDRESS

        #region APPLIED CONTENT

        private static void NoteScopeContent(DialogDatabase database, string before, string after, bool ai, bool restoring)
        {
            using (EndsSessionED.ForDatabase(database).OpenReadOnlyView())
            {
                DialogEntry original = ReadHighlightDialog(before);
                DialogEntry current = ReadHighlightDialog(after);
                TrackStructure(database, DialogSchema, original, current, current?.dialogName ?? original?.dialogName, ai, restoring);
            }
        }

        private static DialogEntry ReadHighlightDialog(string json)
        {
            if (json == null) return null;

            var dialog = new DialogEntry();
            EditorJsonUtility.FromJsonOverwrite(json, dialog);

            return dialog;
        }

        /// <summary> ENDS. Tracks caller-owned models through one schema, remaps control memory by stable structural addresses and leaves persistence and author policy to the existing session. </summary>
        internal static void TrackStructure<TModel>(DialogDatabase database, NodeModelDefinition<TModel> structure, TModel original, TModel current, string dialog, bool ai, bool restoring = false) where TModel : class
        {
            string originalName = dialog;

            if (original is DialogEntry originalDialog) originalName = originalDialog.dialogName;

            var before = new HighlightVisitor(originalName);
            var after = new HighlightVisitor(dialog);
            structure.Traverse(original, before, NodeTraversalMode.Stored);
            structure.Traverse(current, after, NodeTraversalMode.Stored);
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<NodeAddress, HighlightEntry> previous in before.Entries)
            {
                string target = null;

                if (after.Entries.TryGetValue(previous.Key, out HighlightEntry entry)) target = entry.Key;
                if (previous.Value.Key != target) replacements[previous.Value.Key] = target;
            }

            EndsAiEditMemoryED.Find(database)?.RemapPrefixes(replacements);
            Bind(database);

            if (restoring) return;

            foreach (KeyValuePair<NodeAddress, HighlightEntry> currentEntry in after.Entries)
            {
                HighlightEntry entry = currentEntry.Value;
                before.Entries.TryGetValue(currentEntry.Key, out HighlightEntry previous);
                bool changed = previous == null;

                if (!changed && !entry.Block) changed = entry.SameValue != null && (!entry.SameValue(previous) || (entry.Visible && !previous.Visible));
                if (!changed) continue;

                if (ai) Mark(database, entry.Key);
                else Forget(entry.Key);
            }
        }

        private static bool HasSameHighlightMeaning<TModel>(NodeElementDefinition<TModel> element, TModel original, TModel current) where TModel : class
        {
            foreach (NodeDependencyDefinition<TModel> dependency in element.Dependencies)
            {
                if (dependency.Kind != DependencyKind.Meaning) continue;
                if (!dependency.Source.HasSameValue(original, current) || !HasSameHighlightMeaning(dependency.Source, original, current)) return false;
            }

            if (element.Parent != null) return HasSameHighlightMeaning(element.Parent, original, current);

            return true;
        }

        #endregion APPLIED CONTENT

        #region SCHEMA PROJECTION

        private sealed class HighlightEntry
        {
            /// <summary> ENDS. Retains the typed owner so the schema can compare values without JSON or display conversion. </summary>
            internal readonly object Model;
            /// <summary> ENDS. Identifies one canonical structural control in highlight memory. </summary>
            internal readonly string Key;
            /// <summary> ENDS. Reports whether the control is visible, revealing a branch is distinct from changing hidden storage. </summary>
            internal readonly bool Visible;
            /// <summary> ENDS. Marks model creation only, edits to descendants never overwrite their containing block's ownership. </summary>
            internal readonly bool Block;
            /// <summary> ENDS. Compares one field or logical value using its original definition and transitive meaning dependencies. </summary>
            internal readonly Func<HighlightEntry, bool> SameValue;

            /// <summary> ENDS. Captures one independently owned block or field, the entry contains no snapshot, saving or color policy. </summary>
            internal HighlightEntry(object model, string key, bool visible, bool block, Func<HighlightEntry, bool> sameValue = null)
            {
                Model = model;
                Key = key;
                Visible = visible;
                Block = block;
                SameValue = sameValue;
            }
        }

        private sealed class HighlightVisitor : NodeTreeVisitor
        {
            private readonly string _dialog;
            private readonly Stack<bool> _visibility = new Stack<bool>();
            private bool _visible = true;

            /// <summary> ENDS. Indexes blocks and atomic controls by structural identity, row indices are never used to match snapshots. </summary>
            internal readonly Dictionary<NodeAddress, HighlightEntry> Entries = new Dictionary<NodeAddress, HighlightEntry>();

            /// <summary> ENDS. Starts a projection for one dialog without reading or editing its database. </summary>
            internal HighlightVisitor(string dialog) => _dialog = dialog;

            /// <summary> ENDS. Enters a typed model and records its independent background ownership, null models contribute no controls. </summary>
            internal override bool EnterModel<TModel>(NodeModelDefinition<TModel> structure, TModel model, NodeAddress address)
            {
                if (model == null) return false;

                Entries.Add(address, new HighlightEntry(model, StructureKey(_dialog, address), _visible, true));

                return true;
            }

            /// <summary> ENDS. Projects atomic content and logical typed values, nested models are traversed generically and diagnostics never own a control color. </summary>
            internal override bool EnterElement<TModel>(NodeElementDefinition<TModel> element, TModel model, NodeAddress address)
            {
                _visibility.Push(_visible);
                _visible = _visible && element.IsVisible(model);

                if (element.Role != FieldRole.Content) return false;

                bool atom = element is NodeVariableValueDefinition<TModel>;

                if (element is INodeStoredField<TModel> stored) atom = stored.ValueType.IsValueType || stored.ValueType == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(stored.ValueType);
                if (!atom) return true;

                string key = StructureKey(_dialog, address);

                Entries.Add(address, new HighlightEntry(model, key, _visible, false, previous => element.HasSameValue((TModel)previous.Model, model) && HasSameHighlightMeaning(element, (TModel)previous.Model, model)));

                return false;
            }

            /// <summary> ENDS. Balances inherited visibility while stored traversal continues through inactive branches. </summary>
            internal override void ExitElement<TModel>(NodeElementDefinition<TModel> element, TModel model, NodeAddress address) => _visible = _visibility.Pop();
        }

        #endregion SCHEMA PROJECTION
    }
}
#endif
