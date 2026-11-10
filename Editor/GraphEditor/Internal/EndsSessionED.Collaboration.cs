#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal sealed partial class EndsSessionED
    {
        #region FIELDS

        internal const string EDITOR_ACTOR = "editor";
        internal const int LEASE_SECONDS = 120;
        private const int MAX_CHANGE_RECORDS = 64;
        private const string EXCLUSIVE_PREF = "ENDS.ExclusiveDialogEditing";
        private static readonly ConcurrentQueue<string> _closedActors = new ConcurrentQueue<string>();
        [ThreadStatic] private static string _actorId;
        [ThreadStatic] private static string _actorLabel;
        private readonly Dictionary<string, Lease> _leases = new Dictionary<string, Lease>(StringComparer.Ordinal);
        private readonly List<EndsMcpJsonED> _changeRecords = new List<EndsMcpJsonED>();
        private static double _nextCollaborationUpdate;
        private string _pendingReportSavedSnapshot;
        private string _pendingReportWorkingSnapshot;
        private EndsMcpJsonED _pendingReport;
        private string _pendingSavedScopesSnapshot;
        private EndsNodeIdentityED.MigrationPlan _pendingSavedScopesMigration;
        private Dictionary<string, string> _pendingSavedScopes;
        private readonly Dictionary<string, PendingScopeReport> _pendingScopeReports = new Dictionary<string, PendingScopeReport>(StringComparer.Ordinal);

        private sealed class PendingScopeReport
        {
            internal string Before;
            internal string After;
            internal EndsMcpJsonED Report;
        }
        internal static event Action<DialogDatabase, string, string, bool, bool> ScopeContentApplied;

        internal static string CurrentActor => _actorId ?? EDITOR_ACTOR;
        internal static string CurrentActorLabel => _actorLabel ?? "Unity Editor";
        internal static bool ExclusiveEditing
        {
            get => EditorPrefs.GetBool(EXCLUSIVE_PREF, true);
            set => EditorPrefs.SetBool(EXCLUSIVE_PREF, value);
        }

        private sealed class Lease
        {
            internal string Owner;
            internal string Label;
            internal string Task;
            internal DateTime ExpiresUtc;
        }

        private sealed class ActorScope : IDisposable
        {
            private readonly string _previousId = _actorId;
            private readonly string _previousLabel = _actorLabel;

            internal ActorScope(string id, string label)
            {
                _actorId = id;
                _actorLabel = label;
            }

            public void Dispose()
            {
                _actorId = _previousId;
                _actorLabel = _previousLabel;
            }
        }

        #endregion FIELDS

        #region OWNERSHIP

        internal static IDisposable UseActor(string id, string label)
        {
            return new ActorScope(id, label);
        }

        internal static void NotifyClientClosed(string id)
        {
            _closedActors.Enqueue(id);
        }

        private static void UpdateCollaboration()
        {
            if (EditorApplication.timeSinceStartup < _nextCollaborationUpdate) return;

            _nextCollaborationUpdate = EditorApplication.timeSinceStartup + 1;
            HashSet<string> closed = new HashSet<string>(StringComparer.Ordinal);

            while (_closedActors.TryDequeue(out string actor)) closed.Add(actor);

            foreach (EndsSessionED session in Sessions.Values)
            {
                if (session._database == null) continue;

                List<string> expired = new List<string>();

                foreach (KeyValuePair<string, Lease> lease in session._leases)
                {
                    if (closed.Contains(lease.Value.Owner) || lease.Value.ExpiresUtc <= DateTime.UtcNow) expired.Add(lease.Key);
                }

                if (expired.Count == 0) continue;

                foreach (string key in expired)
                {
                    session._leases.Remove(key);
                }

                session._revision++;
                session.StoreSessionState();
                session.PublishStateChange();
            }
        }

        internal void RenewActorLeases()
        {
            foreach (Lease lease in _leases.Values)
            {
                if (lease.Owner == CurrentActor && lease.ExpiresUtc > DateTime.UtcNow && lease.Owner != EDITOR_ACTOR) lease.ExpiresUtc = DateTime.UtcNow.AddSeconds(LEASE_SECONDS);
            }
        }

        internal EndsMcpJsonED ChangeLease(string action, IReadOnlyList<string> dialogs, bool shared, string task = null, bool takeover = false)
        {
            ObserveChanges();
            HashSet<string> keys = ResolveScopes(dialogs, shared);

            if (action == "release" && keys.Count == 0)
            {
                foreach (KeyValuePair<string, Lease> lease in _leases)
                {
                    if (lease.Value.Owner == CurrentActor) keys.Add(lease.Key);
                }

                if (keys.Count == 0) return ReadState();
            }

            if (keys.Count == 0) throw new InvalidOperationException("Select at least one dialog or shared=true.");
            if (action != "acquire" && action != "renew" && action != "release") throw new InvalidOperationException("Lease action must be acquire, renew or release.");
            if (takeover && CurrentActor != EDITOR_ACTOR) throw new InvalidOperationException("Only the local editor can take control from another owner.");

            foreach (string key in keys)
            {
                _leases.TryGetValue(key, out Lease existing);

                if (existing != null && existing.ExpiresUtc <= DateTime.UtcNow) existing = null;
                if (existing != null && existing.Owner != CurrentActor && !takeover) throw new InvalidOperationException("Editing is owned by " + existing.Label + ": " + ScopeName(key) + ".");
                if (action == "renew" && (existing == null || existing.Owner != CurrentActor)) throw new InvalidOperationException("The editing lease expired or was revoked, read the current data and acquire it again.");
            }

            bool changed = false;

            foreach (string key in keys)
            {
                if (action == "release") changed |= _leases.Remove(key);
                else
                {
                    _leases.TryGetValue(key, out Lease existing);
                    bool sameOwner = existing != null && existing.Owner == CurrentActor && existing.ExpiresUtc > DateTime.UtcNow;
                    Lease lease = new Lease { Owner = CurrentActor, Label = CurrentActorLabel, Task = task ?? existing?.Task ?? "" };

                    if (CurrentActor == EDITOR_ACTOR) lease.ExpiresUtc = DateTime.MaxValue;
                    else lease.ExpiresUtc = DateTime.UtcNow.AddSeconds(LEASE_SECONDS);

                    _leases[key] = lease;
                    changed |= !sameOwner;
                }
            }

            if (changed)
            {
                _revision++;
                StoreSessionState();
                PublishStateChange();
            }

            return ReadState();
        }

        internal string DialogOwner(string dialogName)
        {
            DialogEntry dialog = _database.Dialogs?.Find(entry => entry != null && entry.dialogName == dialogName);

            if (dialog != null && _leases.TryGetValue(dialog.NodeIdentityKey, out Lease lease) && lease.ExpiresUtc > DateTime.UtcNow && lease.Owner != EDITOR_ACTOR) return lease.Label;

            return null;
        }

        internal bool EditorOwnsDialog(string dialogName)
        {
            DialogEntry dialog = _database.Dialogs?.Find(entry => entry != null && entry.dialogName == dialogName);

            return dialog != null && _leases.TryGetValue(dialog.NodeIdentityKey, out Lease lease) && lease.Owner == EDITOR_ACTOR;
        }

        internal void ReleaseEditorDialog(string dialogName)
        {
            List<string> released = new List<string>();

            foreach (KeyValuePair<string, Lease> lease in _leases)
            {
                if (lease.Value.Owner == EDITOR_ACTOR && (lease.Key == dialogName || ScopeName(lease.Key) == dialogName)) released.Add(lease.Key);
            }

            foreach (string key in released)
            {
                _leases.Remove(key);
            }

            if (released.Count == 0) return;

            _revision++;
            StoreSessionState();

            PublishStateChange();
        }

        private void RequireScopeAccess(HashSet<string> keys, string before, string after)
        {
            if (!ExclusiveEditing || !_identityReady) return;

            Dictionary<string, string> previous = ReadScopes(before);

            foreach (string key in keys)
            {
                _leases.TryGetValue(key, out Lease lease);

                if (lease != null && lease.ExpiresUtc <= DateTime.UtcNow) lease = null;
                if (lease != null && lease.Owner != CurrentActor) throw new InvalidOperationException("Editing is owned by " + lease.Label + ": " + ScopeName(key) + ".");
                if (CurrentActor == EDITOR_ACTOR) continue;
                if (lease != null && lease.Owner == CurrentActor) continue;
                if (key != SHARED_SCOPE && !previous.ContainsKey(key) && _leases.TryGetValue(SHARED_SCOPE, out Lease shared) && shared.Owner == CurrentActor && shared.ExpiresUtc > DateTime.UtcNow) continue;

                throw new InvalidOperationException("Acquire an editing lease before writing " + ScopeName(key) + ". Use ends_edit_lease and reread the revision.");
            }
        }

        #endregion OWNERSHIP

        #region COLLABORATION REPORT

        internal EndsMcpJsonED ReadPendingChanges()
        {
            if (_readOnlyViewDepth == 0) ObserveChanges();

            if (_pendingReport != null && _pendingReportSavedSnapshot == _savedSnapshot && _pendingReportWorkingSnapshot == _observedSnapshot) return _pendingReport;

            if (_pendingSavedScopes == null || _pendingSavedScopesSnapshot != _savedSnapshot || _pendingSavedScopesMigration != _identityMigration)
            {
                if (_identityMigration.Dialogs.Count == 0) _pendingSavedScopes = ReadScopes(_savedSnapshot);
                else
                {
                    DialogDatabase savedDatabase = ReadSavedDatabase();

                    try
                    {
                        _pendingSavedScopes = ReadScopes(EditorJsonUtility.ToJson(savedDatabase));
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(savedDatabase);
                    }
                }

                _pendingSavedScopesSnapshot = _savedSnapshot;
                _pendingSavedScopesMigration = _identityMigration;
            }

            Dictionary<string, string> saved = _pendingSavedScopes;
            Dictionary<string, string> working = ReadScopes(_observedSnapshot);
            EndsMcpJsonED report = EndsMcpJsonED.Arr();
            HashSet<string> dirty = DirtyScopes();

            foreach (string key in new List<string>(_pendingScopeReports.Keys))
            {
                if (!dirty.Contains(key)) _pendingScopeReports.Remove(key);
            }

            foreach (string key in dirty)
            {
                saved.TryGetValue(key, out string previous);
                working.TryGetValue(key, out string current);

                if (_pendingScopeReports.TryGetValue(key, out PendingScopeReport cached) && cached.Before == previous && cached.After == current)
                {
                    report.Add(cached.Report);

                    continue;
                }

                EndsMcpJsonED scope = EndsMcpJsonED.Obj();
                scope.Set("key", EndsMcpJsonED.Str(key));
                scope.Set("name", EndsMcpJsonED.Str(ScopeName(key)));
                EndsMcpJsonED groups = EndsMcpJsonED.Arr();
                Dictionary<string, string> oldNodes = ReadReportNodes(previous);
                Dictionary<string, string> newNodes = ReadReportNodes(current);
                HashSet<string> nodeIds = new HashSet<string>(newNodes.Keys, StringComparer.Ordinal);
                nodeIds.UnionWith(oldNodes.Keys);
                int added = 0;
                int removed = 0;
                int changed = 0;

                foreach (string nodeId in nodeIds)
                {
                    oldNodes.TryGetValue(nodeId, out string oldNode);
                    newNodes.TryGetValue(nodeId, out string newNode);

                    if (oldNode == newNode) continue;

                    EndsMcpJsonED nodeGroup = BuildPendingGroup(nodeId, "Changed", oldNode, newNode, typeof(DialogNode));

                    if (oldNode != null && newNode != null && nodeGroup.Get("fields").Items.Count == 0) continue;

                    string status = "Changed";

                    if (oldNode == null)
                    {
                        status = "Added";
                        added++;
                    }
                    else if (newNode == null)
                    {
                        status = "Removed";
                        removed++;
                    }
                    else changed++;

                    nodeGroup.Set("status", EndsMcpJsonED.Str(status));
                    groups.Add(nodeGroup);
                }

                EndsMcpJsonED oldSettings = EndsMcpJsonED.Obj();
                EndsMcpJsonED newSettings = EndsMcpJsonED.Obj();

                if (previous != null) oldSettings = EndsMcpJsonED.Parse(previous);
                if (current != null) newSettings = EndsMcpJsonED.Parse(current);

                Type settingsType = typeof(DialogEntry);

                if (key == SHARED_SCOPE) settingsType = typeof(DialogDatabase);

                EndsMcpJsonED oldOrder = ReadPendingOrder(oldSettings, key);
                EndsMcpJsonED newOrder = ReadPendingOrder(newSettings, key);
                oldSettings.Remove("nodes");
                newSettings.Remove("nodes");
                EndsMcpJsonED settingsGroup = BuildPendingGroup("Dialog settings", "Changed", oldSettings.Write(), newSettings.Write(), settingsType);
                string orderPath = "/$nodeOrder";

                if (key == SHARED_SCOPE) orderPath = "/$dialogOrder";

                AppendPendingOrder(oldOrder, newOrder, orderPath, settingsGroup.Get("fields"));

                foreach (EndsMcpJsonED field in settingsGroup.Get("fields").Items)
                {
                    if (field.Get("path").Text == orderPath) settingsGroup.Get("tree")?.Add(field);
                }

                if (key == SHARED_SCOPE) settingsGroup.Set("name", EndsMcpJsonED.Str("Shared database data"));
                if (settingsGroup.Get("fields").Items.Count > 0) groups.Items.Insert(0, settingsGroup);

                scope.Set("groups", groups);
                scope.Set("nodesAdded", EndsMcpJsonED.Num(added));
                scope.Set("nodesRemoved", EndsMcpJsonED.Num(removed));
                scope.Set("nodesChanged", EndsMcpJsonED.Num(changed));
                string scopeStatus = "Changed";

                if (previous == null) scopeStatus = "Added";
                else if (current == null) scopeStatus = "Removed";

                scope.Set("status", EndsMcpJsonED.Str(scopeStatus));
                scope.Set("internalOnly", EndsMcpJsonED.Boolean(groups.Items.Count == 0));
                _pendingScopeReports[key] = new PendingScopeReport { Before = previous, After = current, Report = scope };
                report.Add(scope);
            }

            _pendingReportSavedSnapshot = _savedSnapshot;
            _pendingReportWorkingSnapshot = _observedSnapshot;
            _pendingReport = report;

            return report;
        }

        private static EndsMcpJsonED BuildPendingGroup(string name, string status, string before, string after, Type modelType = null)
        {
            EndsMcpJsonED group = EndsMcpJsonED.Obj();
            group.Set("name", EndsMcpJsonED.Str(name));
            group.Set("status", EndsMcpJsonED.Str(status));
            EndsMcpJsonED fields = EndsMcpJsonED.Arr();
            EndsMcpJsonED oldValue = null;
            EndsMcpJsonED newValue = null;
            bool structured = EndsNodeChangesED.TryBuild(modelType, before, after, out EndsMcpJsonED tree, out EndsMcpJsonED structuredFields);

            if (before != null) oldValue = EndsMcpJsonED.Parse(before);
            if (after != null) newValue = EndsMcpJsonED.Parse(after);

            if (structured)
            {
                group.Set("tree", tree);
                fields = structuredFields;
            }
            else AppendPendingFields(ProjectPendingValue(oldValue, modelType), ProjectPendingValue(newValue, modelType), "", fields);

            if (modelType == typeof(DialogNode))
            {
                EndsMcpJsonED node = newValue ?? oldValue;
                string typeName = "Node";

                if (int.TryParse(node?.Get(EndsNodeStructureED.NodeSchema.Type.Name)?.Text, out int nodeType)) typeName = Enum.GetName(typeof(NodeType), nodeType) ?? typeName;

                group.Set("nodeType", EndsMcpJsonED.Str(typeName));
                string identityPath = "/" + EndsNodeStructureED.NodeSchema.Id.Name;
                fields.Items.RemoveAll(field => field.Get("path").Text == identityPath);

                if (structured) tree.Items.RemoveAll(field => field.Get("path").Text == identityPath);
            }

            group.Set("fields", fields);

            return group;
        }

        private static void AppendPendingFields(EndsMcpJsonED before, EndsMcpJsonED after, string path, EndsMcpJsonED fields, string language = null)
        {
            if (before?.Write() == after?.Write()) return;

            language = after?.Get("LanguageCode")?.Text ?? after?.Get("_languageCode")?.Text ?? before?.Get("LanguageCode")?.Text ?? before?.Get("_languageCode")?.Text ?? language;
            bool oldObject = before?.Type == EndsMcpJsonED.Kind.Object;
            bool newObject = after?.Type == EndsMcpJsonED.Kind.Object;
            bool oldArray = before?.Type == EndsMcpJsonED.Kind.Array;
            bool newArray = after?.Type == EndsMcpJsonED.Kind.Array;

            if (!string.IsNullOrEmpty(path) && (before == null || after == null || IsPendingAtomicValue(before) || IsPendingAtomicValue(after)))
            {
                if (before == null && IsPendingEmptyValue(after) || after == null && IsPendingEmptyValue(before)) return;

                AddPendingField(before, after, path, fields, language);

                return;
            }

            if ((oldObject || before == null) && (newObject || after == null) && (oldObject || newObject))
            {
                HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

                if (oldObject) keys.UnionWith(before.Keys);
                if (newObject) keys.UnionWith(after.Keys);

                foreach (string key in keys)
                {
                    AppendPendingFields(before?.Get(key), after?.Get(key), path + "/" + key.Replace("~", "~0").Replace("/", "~1"), fields, language);
                }

                return;
            }

            if ((oldArray || before == null) && (newArray || after == null) && (oldArray || newArray))
            {
                if (path.EndsWith("/DialogsText", StringComparison.Ordinal) || path.EndsWith("/Translations", StringComparison.Ordinal))
                {
                    Dictionary<string, EndsMcpJsonED> previousLanguages = IndexPendingTranslations(before);
                    Dictionary<string, EndsMcpJsonED> currentLanguages = IndexPendingTranslations(after);

                    if (previousLanguages != null && currentLanguages != null)
                    {
                        HashSet<string> languages = new HashSet<string>(previousLanguages.Keys, StringComparer.Ordinal);
                        languages.UnionWith(currentLanguages.Keys);

                        foreach (string code in languages)
                        {
                            previousLanguages.TryGetValue(code, out EndsMcpJsonED previousText);
                            currentLanguages.TryGetValue(code, out EndsMcpJsonED currentText);
                            AppendPendingFields(previousText, currentText, path + "/" + code.Replace("~", "~0").Replace("/", "~1"), fields, code);
                        }

                        AppendPendingOrder(PendingKeys(previousLanguages), PendingKeys(currentLanguages), path + "/$order", fields);

                        return;
                    }
                }

                AppendPendingArray(before, after, path, fields, language);

                return;
            }

            AddPendingField(before, after, path, fields, language);
        }

        private static Dictionary<string, EndsMcpJsonED> IndexPendingTranslations(EndsMcpJsonED translations)
        {
            Dictionary<string, EndsMcpJsonED> languages = new Dictionary<string, EndsMcpJsonED>(StringComparer.Ordinal);

            foreach (EndsMcpJsonED entry in translations?.Items ?? new List<EndsMcpJsonED>())
            {
                string code = entry.Get("LanguageCode")?.Text ?? entry.Get("_languageCode")?.Text ?? entry.Get("languageCode")?.Text;

                if (string.IsNullOrEmpty(code) || languages.ContainsKey(code)) return null;

                languages.Add(code, entry);
            }

            return languages;
        }

        private string ScopeName(string key)
        {
            if (key == SHARED_SCOPE) return "Shared database data";

            DialogEntry current = _database.Dialogs?.Find(dialog => dialog?.NodeIdentityKey == key);

            if (current != null) return current.dialogName;

            foreach (Dictionary<string, string> scopes in new[] { ReadScopes(_observedSnapshot), ReadScopes(_savedSnapshot) })
            {
                if (scopes.TryGetValue(key, out string value)) return EndsMcpJsonED.Parse(value).Get("dialogName")?.Text ?? key;
            }

            return key;
        }

        private void RecordCollaborationChange(string before, string after, string label, HashSet<string> affected, bool restoring = false)
        {
            Dictionary<string, string> oldScopes = ReadScopes(before);
            Dictionary<string, string> newScopes = ReadScopes(after);
            EndsMcpJsonED record = EndsMcpJsonED.Obj();
            record.Set("actor", EndsMcpJsonED.Str(CurrentActorLabel));
            record.Set("sessionId", EndsMcpJsonED.Str(CurrentActor));
            record.Set("operation", EndsMcpJsonED.Str(label));
            record.Set("revision", EndsMcpJsonED.Num(_revision));
            record.Set("utc", EndsMcpJsonED.Str(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            EndsMcpJsonED edits = EndsMcpJsonED.Arr();

            foreach (string key in affected)
            {
                oldScopes.TryGetValue(key, out string oldValue);
                newScopes.TryGetValue(key, out string newValue);
                EndsMcpJsonED edit = EndsMcpJsonED.Obj();
                edit.Set("scopeKey", EndsMcpJsonED.Str(key));
                edit.Set("scope", EndsMcpJsonED.Str(ScopeName(key)));
                EndsMcpJsonED paths = EndsMcpJsonED.Arr();
                AppendChangedPaths(oldValue, newValue, paths);
                edit.Set("fields", paths);
                Dictionary<string, string> oldNodes = ReadReportNodes(oldValue);
                Dictionary<string, string> newNodes = ReadReportNodes(newValue);
                int added = 0;
                int removed = 0;
                int changed = 0;

                foreach (KeyValuePair<string, string> node in newNodes)
                {
                    if (!oldNodes.TryGetValue(node.Key, out string previous)) added++;
                    else if (node.Value != previous) changed++;
                }

                foreach (string node in oldNodes.Keys)
                {
                    if (!newNodes.ContainsKey(node)) removed++;
                }

                edit.Set("nodesAdded", EndsMcpJsonED.Num(added));
                edit.Set("nodesRemoved", EndsMcpJsonED.Num(removed));
                edit.Set("nodesChanged", EndsMcpJsonED.Num(changed));
                edits.Add(edit);

                if (key != SHARED_SCOPE) PublishScopeContentApplied(oldValue, newValue, restoring);

                if (_identityReady && !oldScopes.ContainsKey(key) && key != SHARED_SCOPE && CurrentActor != EDITOR_ACTOR) _leases[key] = new Lease { Owner = CurrentActor, Label = CurrentActorLabel, Task = "Created dialog", ExpiresUtc = DateTime.UtcNow.AddSeconds(LEASE_SECONDS) };
            }

            record.Set("edits", edits);
            _changeRecords.Insert(0, record);

            if (_changeRecords.Count > MAX_CHANGE_RECORDS) _changeRecords.RemoveAt(_changeRecords.Count - 1);
        }

        private void PublishScopeContentApplied(string before, string after, bool restoring)
        {
            Action<DialogDatabase, string, string, bool, bool> listeners = ScopeContentApplied;

            if (listeners == null) return;

            foreach (Action<DialogDatabase, string, string, bool, bool> listener in listeners.GetInvocationList())
            {
                try
                {
                    listener(_database, before, after, CurrentActor != EDITOR_ACTOR, restoring);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        private static void AppendChangedPaths(string before, string after, EndsMcpJsonED paths)
        {
            EndsMcpJsonED previous = null;
            EndsMcpJsonED current = null;

            if (before != null) previous = EndsMcpJsonED.Parse(before);
            if (after != null) current = EndsMcpJsonED.Parse(after);

            AppendFieldChanges(previous, current, "", paths);
        }

        private static Dictionary<string, string> ReadReportNodes(string value)
        {
            Dictionary<string, string> nodes = new Dictionary<string, string>(StringComparer.Ordinal);

            if (value == null) return nodes;

            foreach (EndsMcpJsonED node in EndsMcpJsonED.Parse(value).Get("nodes")?.Items ?? new List<EndsMcpJsonED>())
            {
                string id = node.Get("nodeId")?.Text;

                if (id != null) nodes[id] = node.Write();
            }

            return nodes;
        }

        private static void AppendFieldChanges(EndsMcpJsonED before, EndsMcpJsonED after, string path, EndsMcpJsonED paths)
        {
            if (before?.Write() == after?.Write()) return;

            if (before?.Type == EndsMcpJsonED.Kind.Object && after?.Type == EndsMcpJsonED.Kind.Object)
            {
                HashSet<string> keys = new HashSet<string>(before.Keys, StringComparer.Ordinal);
                keys.UnionWith(after.Keys);

                foreach (string key in keys)
                {
                    AppendFieldChanges(before.Get(key), after.Get(key), path + "/" + key, paths);
                }
            }
            else if (before?.Type == EndsMcpJsonED.Kind.Array && after?.Type == EndsMcpJsonED.Kind.Array)
            {
                for (int i = 0; i < Math.Max(before.Items.Count, after.Items.Count); i++)
                {
                    EndsMcpJsonED oldValue = null;
                    EndsMcpJsonED newValue = null;

                    if (i < before.Items.Count) oldValue = before.Items[i];
                    if (i < after.Items.Count) newValue = after.Items[i];

                    string key = i.ToString(CultureInfo.InvariantCulture);
                    string nodeId = newValue?.Get("nodeId")?.Text ?? oldValue?.Get("nodeId")?.Text;

                    if (!string.IsNullOrEmpty(nodeId)) key = nodeId;

                    AppendFieldChanges(oldValue, newValue, path + "/" + key, paths);
                }
            }
            else paths.Add(EndsMcpJsonED.Str(path));
        }

        internal EndsMcpJsonED ReadCollaboration(bool includeChanges = false)
        {
            EndsMcpJsonED report = EndsMcpJsonED.Obj();
            report.Set("exclusiveEditing", EndsMcpJsonED.Boolean(ExclusiveEditing));
            report.Set("currentActor", EndsMcpJsonED.Str(CurrentActor));
            report.Set("leaseSeconds", EndsMcpJsonED.Num(LEASE_SECONDS));
            EndsMcpJsonED leases = EndsMcpJsonED.Arr();

            foreach (KeyValuePair<string, Lease> lease in _leases)
            {
                if (lease.Value.ExpiresUtc <= DateTime.UtcNow) continue;

                EndsMcpJsonED row = EndsMcpJsonED.Obj();
                row.Set("scope", EndsMcpJsonED.Str(ScopeName(lease.Key)));
                row.Set("sessionId", EndsMcpJsonED.Str(lease.Value.Owner));
                row.Set("owner", EndsMcpJsonED.Str(lease.Value.Label));
                row.Set("task", EndsMcpJsonED.Str(lease.Value.Task));
                row.Set("expiresUtc", EndsMcpJsonED.Str(lease.Value.ExpiresUtc.ToString("O", CultureInfo.InvariantCulture)));
                leases.Add(row);
            }

            report.Set("leases", leases);
            EndsMcpJsonED scopeStates = EndsMcpJsonED.Arr();
            HashSet<string> dirty = DirtyScopes();
            HashSet<string> allScopes = new HashSet<string>(ReadScopes(_observedSnapshot).Keys, StringComparer.Ordinal);
            allScopes.UnionWith(ReadScopes(_savedSnapshot).Keys);

            foreach (string key in allScopes)
            {
                EndsMcpJsonED scope = EndsMcpJsonED.Obj();
                scope.Set("scope", EndsMcpJsonED.Str(ScopeName(key)));
                scope.Set("shared", EndsMcpJsonED.Boolean(key == SHARED_SCOPE));
                scope.Set("dirty", EndsMcpJsonED.Boolean(dirty.Contains(key)));
                scopeStates.Add(scope);
            }

            report.Set("scopes", scopeStates);
            EndsMcpJsonED changes = EndsMcpJsonED.Arr();

            foreach (EndsMcpJsonED record in _changeRecords)
            {
                if (includeChanges) changes.Add(record);
            }

            report.Set("recentChanges", changes);

            return report;
        }

        #endregion COLLABORATION REPORT
    }
}
#endif