#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal sealed partial class EndsSessionED
    {
        #region FIELDS

        private static readonly ConditionalWeakTable<string, SnapshotParts> _snapshotParts = new ConditionalWeakTable<string, SnapshotParts>();
        private readonly Dictionary<string, ScopeVersion> _scopeVersions = new Dictionary<string, ScopeVersion>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _categoryRevisions = new Dictionary<string, int>(StringComparer.Ordinal);
        private string _versionedSnapshot;
        private string _versionedShared;
        private int _catalogRevision;

        private sealed class SnapshotParts
        {
            internal readonly Dictionary<string, string> Scopes;
            internal readonly EndsMcpJsonED Envelope;

            internal SnapshotParts(string snapshot)
            {
                Envelope = EndsMcpJsonED.Parse(snapshot);
                EndsMcpJsonED root = Envelope.Get("MonoBehaviour") ?? Envelope;
                EndsMcpJsonED order = EndsMcpJsonED.Arr();
                Scopes = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (EndsMcpJsonED dialog in root.Get("_dialogs")?.Items ?? new List<EndsMcpJsonED>())
                {
                    string key = dialog.Get("_nodeIdentityKey")?.Text;

                    if (string.IsNullOrEmpty(key)) key = "legacy:" + dialog.Get("dialogName")?.Text;
                    if (Scopes.ContainsKey(key)) throw new InvalidOperationException("Duplicate dialog identity in the workspace.");

                    Scopes.Add(key, dialog.Write());
                    order.Add(EndsMcpJsonED.Str(key));
                }

                root.Remove("_dialogs");
                root.Set(ORDER_FIELD, order);
                Scopes.Add(SHARED_SCOPE, root.Write());
            }

            internal SnapshotParts(Dictionary<string, string> scopes, EndsMcpJsonED envelope)
            {
                Scopes = scopes;
                Envelope = envelope;
            }
        }

        private sealed class ScopeVersion
        {
            internal string Snapshot;
            internal string Metadata;
            internal string Category;
            internal int Revision;
        }

        #endregion FIELDS

        #region SNAPSHOT PARTS

        private static SnapshotParts GetSnapshotParts(string snapshot)
        {
            return _snapshotParts.GetValue(snapshot, value => new SnapshotParts(value));
        }

        private static string ComposeScopes(string baseline, Dictionary<string, string> replacements)
        {
            SnapshotParts original = GetSnapshotParts(baseline);
            Dictionary<string, string> scopes = new Dictionary<string, string>(original.Scopes, StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> replacement in replacements)
            {
                if (replacement.Value == null) scopes.Remove(replacement.Key);
                else scopes[replacement.Key] = replacement.Value;
            }

            EndsMcpJsonED root = EndsMcpJsonED.Parse(scopes[SHARED_SCOPE]);
            var text = new StringBuilder();
            bool wrapped = original.Envelope.Get("MonoBehaviour") != null;

            if (wrapped) text.Append("{\"MonoBehaviour\":");

            text.Append('{');

            foreach (string field in root.Keys)
            {
                if (field == ORDER_FIELD) continue;

                text.Append(EndsMcpJsonED.Str(field).Write()).Append(':').Append(root.Get(field).Write()).Append(',');
            }

            text.Append("\"_dialogs\":[");
            HashSet<string> included = new HashSet<string>(StringComparer.Ordinal);
            EndsMcpJsonED actualOrder = EndsMcpJsonED.Arr();

            foreach (EndsMcpJsonED key in root.Get(ORDER_FIELD).Items)
            {
                if (!scopes.TryGetValue(key.Text, out string dialog)) continue;
                if (included.Count > 0) text.Append(',');

                text.Append(dialog);
                included.Add(key.Text);
                actualOrder.Add(EndsMcpJsonED.Str(key.Text));
            }

            foreach (KeyValuePair<string, string> scope in scopes)
            {
                if (scope.Key == SHARED_SCOPE || included.Contains(scope.Key)) continue;
                if (included.Count > 0) text.Append(',');

                text.Append(scope.Value);
                included.Add(scope.Key);
                actualOrder.Add(EndsMcpJsonED.Str(scope.Key));
            }

            text.Append("]}");

            if (wrapped)
            {
                foreach (string field in original.Envelope.Keys)
                {
                    if (field == "MonoBehaviour") continue;

                    text.Append(',').Append(EndsMcpJsonED.Str(field).Write()).Append(':').Append(original.Envelope.Get(field).Write());
                }

                text.Append('}');
            }

            string snapshot = text.ToString();
            root.Set(ORDER_FIELD, actualOrder);
            scopes[SHARED_SCOPE] = root.Write();
            _snapshotParts.Add(snapshot, new SnapshotParts(scopes, original.Envelope));

            return snapshot;
        }

        #endregion SNAPSHOT PARTS

        #region SCOPED TRANSACTIONS

        private DialogDatabase CreateScopeStage(ISet<string> keys)
        {
            DialogDatabase staged = ScriptableObject.CreateInstance<DialogDatabase>();

            foreach (EndsDialogRulesED.ModelField field in EndsDialogRulesED.Models[typeof(DialogDatabase)])
            {
                if (field.Name == nameof(DialogDatabase.Dialogs)) continue;

                object value = field.Read(_database);

                if (keys == null) value = EndsDialogRulesED.Snapshot(value);

                field.Write(staged, value);
            }

            staged.name = _database.name;
            staged.hideFlags = _database.hideFlags;
            staged.Dialogs = new List<DialogEntry>();

            foreach (DialogEntry dialog in _database.Dialogs ?? new List<DialogEntry>())
            {
                if (dialog != null && (keys == null || keys.Contains(dialog.NodeIdentityKey))) staged.Dialogs.Add(EndsDialogRulesED.Snapshot(dialog));
                else staged.Dialogs.Add(dialog);
            }

            return staged;
        }

        private string SnapshotScopes(DialogDatabase staged, ISet<string> keys)
        {
            Dictionary<string, string> replacements = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (DialogEntry dialog in staged.Dialogs)
            {
                if (dialog == null || !keys.Contains(dialog.NodeIdentityKey)) continue;

                replacements.Add(dialog.NodeIdentityKey, EndsMcpJsonED.Parse(EditorJsonUtility.ToJson(dialog)).Write());
            }

            return MergeScopes(_observedSnapshot, replacements);
        }

        private bool CanRestoreDialogScopes(Dictionary<string, string> replacements)
        {
            if (_identityMigration.Dialogs.Count > 0 || replacements.ContainsKey(SHARED_SCOPE)) return false;

            Dictionary<string, string> current = GetSnapshotParts(_observedSnapshot).Scopes;

            foreach (KeyValuePair<string, string> replacement in replacements)
            {
                if (replacement.Value == null || !current.TryGetValue(replacement.Key, out string previous)) return false;
                if (SelectionMetadata(EndsMcpJsonED.Parse(previous)) != SelectionMetadata(EndsMcpJsonED.Parse(replacement.Value))) return false;
            }

            return true;
        }

        private static void RestoreDialogScopes(DialogDatabase staged, Dictionary<string, string> replacements)
        {
            foreach (DialogEntry dialog in staged.Dialogs)
            {
                if (dialog == null || !replacements.TryGetValue(dialog.NodeIdentityKey, out string snapshot)) continue;

                EditorJsonUtility.FromJsonOverwrite(snapshot, dialog);
            }
        }

        private void ApplyPreparedScopes(DialogDatabase staged, ISet<string> keys, string snapshot)
        {
            ApplyScopeModels(_database, staged, keys, false);

            if (_readOnlyBaseline == null) return;

            ApplyScopeModels(_readOnlyBaseline, staged, keys, true);
            _readOnlySnapshot = snapshot;
            _readOnlyDatabaseName = _database.name;
            _readOnlyDatabaseFlags = _database.hideFlags;
        }

        private static void ApplyScopeModels(DialogDatabase target, DialogDatabase staged, ISet<string> keys, bool copy)
        {
            if (keys.Contains(SHARED_SCOPE))
            {
                foreach (EndsDialogRulesED.ModelField field in EndsDialogRulesED.Models[typeof(DialogDatabase)])
                {
                    if (field.Name == nameof(DialogDatabase.Dialogs)) continue;

                    object value = field.Read(staged);

                    if (copy) value = EndsDialogRulesED.Snapshot(value);

                    field.Write(target, value);
                }

                target.name = staged.name;
                target.hideFlags = staged.hideFlags;
            }

            Dictionary<string, DialogEntry> previous = new Dictionary<string, DialogEntry>(StringComparer.Ordinal);

            foreach (DialogEntry dialog in target.Dialogs ?? new List<DialogEntry>())
            {
                if (dialog != null && !string.IsNullOrEmpty(dialog.NodeIdentityKey)) previous[dialog.NodeIdentityKey] = dialog;
            }

            List<DialogEntry> next = new List<DialogEntry>();

            foreach (DialogEntry dialog in staged.Dialogs ?? new List<DialogEntry>())
            {
                if (dialog == null)
                {
                    next.Add(null);

                    continue;
                }

                DialogEntry existing = null;

                if (!string.IsNullOrEmpty(dialog.NodeIdentityKey)) previous.TryGetValue(dialog.NodeIdentityKey, out existing);

                if (existing != null && !keys.Contains(dialog.NodeIdentityKey)) next.Add(existing);
                else
                {
                    DialogEntry prepared = dialog;

                    if (copy) prepared = EndsDialogRulesED.Snapshot(dialog);

                    if (existing == null) next.Add(prepared);
                    else
                    {
                        foreach (EndsDialogRulesED.ModelField field in EndsDialogRulesED.Models[typeof(DialogEntry)])
                        {
                            field.Write(existing, field.Read(prepared));
                        }

                        next.Add(existing);
                    }
                }
            }

            target.Dialogs = next;
        }

        #endregion SCOPED TRANSACTIONS

        #region SCOPED REVISIONS

        /// <summary> ENDS. Invalidates catalogs only when shared data or dialog selection metadata changes, node content edits retain catalog instances. </summary>
        internal int CatalogRevision { get { RefreshScopeVersions(); return _catalogRevision; } }

        /// <summary> ENDS. Returns a dialog-local content revision without observing or serializing the live database. </summary>
        internal int DialogRevision(string scope)
        {
            RefreshScopeVersions();

            if (_scopeVersions.TryGetValue(scope, out ScopeVersion version)) return version.Revision;

            return -1;
        }

        /// <summary> ENDS. Invalidates start-condition diagnostics only after relevant category metadata, conditions or evaluation order changes. </summary>
        internal int CategoryRevision(string category)
        {
            RefreshScopeVersions();
            _categoryRevisions.TryGetValue(category, out int revision);

            return revision;
        }

        private static string SelectionMetadata(EndsMcpJsonED model)
        {
            EndsMcpJsonED metadata = EndsMcpJsonED.Obj();

            if (model != null)
            {
                foreach (string field in new[] { "dialogName", "initiator", "isLocked", "isAndLogic", "conditions" })
                {
                    metadata.Set(field, model.Get(field));
                }

                metadata.Set("hasNodes", EndsMcpJsonED.Boolean(model.Get("nodes")?.Items.Count > 0));
            }

            return metadata.Write();
        }

        private void RefreshScopeVersions()
        {
            if (_versionedSnapshot == _observedSnapshot) return;

            Dictionary<string, string> scopes = GetSnapshotParts(_observedSnapshot).Scopes;
            string shared = scopes[SHARED_SCOPE];
            bool sharedChanged = shared != _versionedShared;
            bool orderChanged = _versionedShared == null;

            if (sharedChanged && !orderChanged) orderChanged = EndsMcpJsonED.Parse(shared).Get(ORDER_FIELD).Write() != EndsMcpJsonED.Parse(_versionedShared).Get(ORDER_FIELD).Write();

            HashSet<string> categories = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> keys = new HashSet<string>(_scopeVersions.Keys, StringComparer.Ordinal);
            keys.UnionWith(scopes.Keys);
            keys.Remove(SHARED_SCOPE);

            foreach (string key in keys)
            {
                if (!_scopeVersions.TryGetValue(key, out ScopeVersion version))
                {
                    version = new ScopeVersion();
                    _scopeVersions.Add(key, version);
                }

                if (orderChanged && version.Category != null) categories.Add(version.Category);

                scopes.TryGetValue(key, out string snapshot);

                if (snapshot == version.Snapshot) continue;

                EndsMcpJsonED model = null;
                string category = null;

                if (snapshot != null)
                {
                    model = EndsMcpJsonED.Parse(snapshot);
                    category = model.Get("initiator")?.Text;

                    if (string.IsNullOrEmpty(category)) category = "Unnamed";
                }

                string signature = SelectionMetadata(model);

                if (version.Metadata != signature)
                {
                    if (version.Category != null) categories.Add(version.Category);
                    if (category != null) categories.Add(category);

                    _catalogRevision++;
                }

                version.Revision++;
                version.Snapshot = snapshot;
                version.Metadata = signature;
                version.Category = category;

                if (orderChanged && category != null) categories.Add(category);
            }

            if (sharedChanged) _catalogRevision++;

            foreach (string category in categories)
            {
                _categoryRevisions.TryGetValue(category, out int revision);
                _categoryRevisions[category] = revision + 1;
            }

            _versionedShared = shared;
            _versionedSnapshot = _observedSnapshot;
        }

        #endregion SCOPED REVISIONS
    }
}
#endif