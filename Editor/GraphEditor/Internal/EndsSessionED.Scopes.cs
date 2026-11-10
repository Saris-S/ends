#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal sealed partial class EndsSessionED
    {
        #region FIELDS

        internal const string SHARED_SCOPE = "$shared";
        private const string ORDER_FIELD = "$dialogOrder";
        private readonly Dictionary<string, EndsSessionUndoED> _scopeUndo = new Dictionary<string, EndsSessionUndoED>(StringComparer.Ordinal);
        private string _dirtyWorkingSnapshot;
        private string _dirtySavedSnapshot;
        private HashSet<string> _dirtyScopes;
        internal static event Action<DialogDatabase, IReadOnlyCollection<string>, bool> ScopesResolved;

        #endregion FIELDS

        #region SCOPE SNAPSHOTS

        internal static DialogDatabase SourceAsset(DialogDatabase database)
        {
            if (database == null) return null;

            foreach (EndsSessionED session in Sessions.Values)
            {
                if (session._database == database || session._asset == database) return session._asset;
            }

            return database;
        }

        internal static string AssetPath(DialogDatabase database)
        {
            return AssetDatabase.GetAssetPath(SourceAsset(database));
        }

        internal static Dictionary<string, string> ReadScopes(string snapshot)
        {
            return new Dictionary<string, string>(GetSnapshotParts(snapshot).Scopes, StringComparer.Ordinal);
        }

        internal static HashSet<string> ChangedScopes(string before, string after)
        {
            Dictionary<string, string> oldScopes = ReadScopes(before);
            Dictionary<string, string> newScopes = ReadScopes(after);
            HashSet<string> changed = new HashSet<string>(oldScopes.Keys, StringComparer.Ordinal);
            changed.UnionWith(newScopes.Keys);
            changed.RemoveWhere(key => oldScopes.TryGetValue(key, out string oldValue) && newScopes.TryGetValue(key, out string newValue) && oldValue == newValue);

            return changed;
        }

        internal static string MergeScopes(string baseline, Dictionary<string, string> replacements)
        {
            return ComposeScopes(baseline, replacements);
        }

        internal HashSet<string> ResolveScopes(IReadOnlyList<string> dialogs, bool shared)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> current = ReadScopes(_observedSnapshot);
            Dictionary<string, string> saved = ReadScopes(_savedSnapshot);

            if (shared) keys.Add(SHARED_SCOPE);

            foreach (string name in dialogs ?? Array.Empty<string>())
            {
                string found = null;

                foreach (Dictionary<string, string> collection in new[] { current, saved })
                {
                    foreach (KeyValuePair<string, string> scope in collection)
                    {
                        if (scope.Key == SHARED_SCOPE || EndsMcpJsonED.Parse(scope.Value).Get("dialogName")?.Text != name) continue;
                        if (found != null && found != scope.Key) throw new InvalidOperationException("Ambiguous dialog name across saved and working data: " + name + ".");

                        found = scope.Key;
                    }
                }

                if (found == null) throw new InvalidOperationException("Dialog not found in saved or working data: " + name + ".");

                keys.Add(found);
            }

            return keys;
        }

        internal bool IsDialogDirty(string dialogName)
        {
            if (_readOnlyViewDepth == 0) ObserveChanges();

            DialogEntry dialog = _database.Dialogs.Find(entry => entry != null && entry.dialogName == dialogName);

            if (dialog == null) return false;

            string key = dialog.NodeIdentityKey;

            if (string.IsNullOrEmpty(key)) key = "legacy:" + dialogName;

            return DirtyScopes().Contains(key);
        }

        internal bool IsScopeDirty(string key)
        {
            if (_readOnlyViewDepth == 0) ObserveChanges();

            return DirtyScopes().Contains(key);
        }

        private HashSet<string> DirtyScopes()
        {
            if (_dirtyScopes == null || _dirtyWorkingSnapshot != _observedSnapshot || _dirtySavedSnapshot != _savedSnapshot)
            {
                _dirtyScopes = ChangedScopes(_savedSnapshot, _observedSnapshot);
                _dirtyWorkingSnapshot = _observedSnapshot;
                _dirtySavedSnapshot = _savedSnapshot;
            }

            return _dirtyScopes;
        }

        #endregion SCOPE SNAPSHOTS

        #region SCOPED SAVE AND DISCARD

        internal Result SaveSelection(IReadOnlyList<string> dialogs, bool shared, int? expectedRevision = null)
        {
            HashSet<string> selected = ResolveScopes(dialogs, shared);

            if (selected.Count == 0) return CreateResult("rejected", "Select at least one dialog or shared=true.");

            return SaveScopes(selected, expectedRevision);
        }

        internal Result SaveScopes(HashSet<string> selected, int? expectedRevision = null)
        {
            ObserveChanges();

            if (_executing) return CreateResult("rejected", "A database operation is already running.");
            if (_externalAssetChange) return CreateResult("conflict", "The database was changed outside ENDS. Resolve the external change in the Unity editor before saving. Pending work has been preserved.");
            if (expectedRevision.HasValue && expectedRevision.Value != _revision) return CreateResult("conflict", "Database revision changed. Read the current state before saving.");
            if (selected != null && selected.Count == 0) return CreateResult("unchanged");

            DialogDatabase candidate = null;
            DialogDatabase saved = null;
            string assetBefore = EditorJsonUtility.ToJson(_asset);
            _executing = true;

            try
            {
                HashSet<string> keys = selected;

                if (keys == null) keys = new HashSet<string>(ReadScopes(_observedSnapshot).Keys, StringComparer.Ordinal);

                if (selected == null) keys.UnionWith(ReadScopes(_savedSnapshot).Keys);

                RequireScopeAccess(keys, _savedSnapshot, _observedSnapshot);
                Dictionary<string, string> working = ReadScopes(_observedSnapshot);
                Dictionary<string, string> replacements = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (string key in keys)
                {
                    working.TryGetValue(key, out string value);
                    replacements.Add(key, value);
                }

                candidate = ScriptableObject.CreateInstance<DialogDatabase>();
                saved = ReadSavedDatabase();
                EditorJsonUtility.FromJsonOverwrite(MergeScopes(_savedSnapshot, replacements), candidate);

                if (_identityMigration.Dialogs.Count > 0) EndsNodeIdentityED.ApplyPlan(candidate, _identityMigration);

                MergeNodeNumbers(candidate, true);
                EndsLegacyNameColorMigrationED.RequireConverted(candidate);
                EndsDialogRulesED.ValidateDatabaseChanges(saved, candidate);
                RequireSavedDependencies(saved, candidate, _database);
                string candidateSnapshot = EditorJsonUtility.ToJson(candidate);

                if (_asset != _database && assetBefore != _assetSnapshot) throw new InvalidOperationException("The asset changed outside the ENDS workspace, saving was stopped to preserve the external edit.");

                if (_asset != _database) EditorJsonUtility.FromJsonOverwrite(candidateSnapshot, _asset);

                if (_persist != null)
                {
                    if (_asset == _database) _persist(candidate);
                    else _persist(_asset);
                }
                else
                {
                    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_asset))) throw new InvalidOperationException("The database has no asset path. Create or assign a database asset before saving.");

                    EditorUtility.SetDirty(_asset);
                    AssetDatabase.SaveAssetIfDirty(_asset);

                    if (EditorUtility.IsDirty(_asset)) throw new InvalidOperationException("Unity did not confirm that the database was saved.");
                }

                HashSet<string> savedChanges = ChangedScopes(_savedSnapshot, candidateSnapshot);
                savedChanges.IntersectWith(keys);
                HashSet<string> unchanged = new HashSet<string>(keys, StringComparer.Ordinal);
                unchanged.ExceptWith(savedChanges);

                if (_savedSnapshot != candidateSnapshot || keys.Contains(SHARED_SCOPE) && EndsLegacyNameColorMigrationED.NeedsSave(_database)) _revision++;

                _savedSnapshot = candidateSnapshot;
                _assetSnapshot = EditorJsonUtility.ToJson(_asset);
                _savedRevision = _revision;

                if (keys.Contains(SHARED_SCOPE)) EndsLegacyNameColorMigrationED.ConfirmSaved(_database);

                if (keys.Contains(SHARED_SCOPE)) EndsVariableRuntimeED.RememberSavedDefinitions(_database);

                StoreSessionState();
                PublishScopeResolution(savedChanges, true);
                PublishScopeResolution(unchanged, false);
                PublishStateChange();
                Result result = CreateResult("saved");
                result.ScopeKeys.UnionWith(keys);

                return result;
            }
            catch (Exception exception)
            {
                if (_asset != _database) EditorJsonUtility.FromJsonOverwrite(assetBefore, _asset);

                Result result = Reject(exception);
                result.Status = "save_failed";

                return result;
            }
            finally
            {
                if (candidate != null) UnityEngine.Object.DestroyImmediate(candidate);
                if (saved != null) UnityEngine.Object.DestroyImmediate(saved);

                _executing = false;
            }
        }

        internal Result DiscardSelection(IReadOnlyList<string> dialogs, bool shared, int? expectedRevision = null)
        {
            HashSet<string> selected = ResolveScopes(dialogs, shared);

            if (selected.Count == 0) return CreateResult("rejected", "Select at least one dialog or shared=true.");

            return DiscardScopes(selected, expectedRevision);
        }

        internal Result DiscardScopes(HashSet<string> selected, int? expectedRevision)
        {
            ObserveChanges();

            if (expectedRevision.HasValue && expectedRevision.Value != _revision) return CreateResult("conflict", "Database revision changed. Read the current state before discarding.");

            HashSet<string> keys = selected ?? ChangedScopes(_savedSnapshot, _observedSnapshot);
            Dictionary<string, string> saved = ReadScopes(_savedSnapshot);
            Dictionary<string, string> replacements = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string key in keys)
            {
                saved.TryGetValue(key, out string value);
                replacements.Add(key, value);
            }

            ISet<string> localScopes = null;

            if (CanRestoreDialogScopes(replacements)) localScopes = keys;

            Result result = Execute(staged =>
            {
                if (localScopes == null) EditorJsonUtility.FromJsonOverwrite(MergeScopes(_observedSnapshot, replacements), staged);
                else RestoreDialogScopes(staged, replacements);
            }, "Discard selected ENDS changes", expectedRevision, restoring: true, dialogScopes: localScopes);

            if (result.Success)
            {
                if (result.Changed) result.Status = "discarded";

                PublishScopeResolution(keys, false);
                PublishStateChange();
            }

            return result;
        }

        private void PublishScopeResolution(IReadOnlyCollection<string> keys, bool saved)
        {
            Action<DialogDatabase, IReadOnlyCollection<string>, bool> listeners = ScopesResolved;

            if (listeners == null) return;

            foreach (Action<DialogDatabase, IReadOnlyCollection<string>, bool> listener in listeners.GetInvocationList())
            {
                try
                {
                    listener(_database, keys, saved);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void RequireSavedDependencies(DialogDatabase saved, DialogDatabase candidate, DialogDatabase working)
        {
            HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);

            foreach (EndsDialogRulesED.GraphValidationIssue issue in EndsDialogRulesED.CollectGraphValidation(saved, null, false))
            {
                existing.Add(issue.Key);
            }

            foreach (EndsDialogRulesED.GraphValidationIssue issue in EndsDialogRulesED.CollectGraphValidation(working, null, false))
            {
                if (issue.Code == "missing_reference" && issue.Severity == "warning") existing.Add(issue.Key);
            }

            List<EndsDialogRulesED.GraphValidationIssue> introduced = new List<EndsDialogRulesED.GraphValidationIssue>();

            foreach (EndsDialogRulesED.GraphValidationIssue issue in EndsDialogRulesED.CollectGraphValidation(candidate, null, false))
            {
                if (existing.Contains(issue.Key)) continue;
                if (issue.Code.IndexOf("missing", StringComparison.OrdinalIgnoreCase) < 0 && issue.Code.IndexOf("reference", StringComparison.OrdinalIgnoreCase) < 0) continue;

                introduced.Add(issue);
            }

            if (introduced.Count > 0) throw new EndsDialogRulesED.GraphValidationException(introduced);
        }

        #endregion SCOPED SAVE AND DISCARD

        #region SCOPED UNDO

        private void InitializeScopeUndo()
        {
            Dictionary<string, string> scopes = ReadScopes(_observedSnapshot);

            foreach (EndsSessionUndoED record in Resources.FindObjectsOfTypeAll<EndsSessionUndoED>())
            {
                if (record.Asset != _asset) continue;

                scopes.TryGetValue(record.Scope, out string snapshot);
                record.Snapshot = snapshot;
                _scopeUndo[record.Scope] = record;
            }

            foreach (KeyValuePair<string, string> scope in scopes)
            {
                if (_scopeUndo.TryGetValue(scope.Key, out EndsSessionUndoED existing)) existing.Snapshot = scope.Value;
                else
                {
                    EndsSessionUndoED record = ScriptableObject.CreateInstance<EndsSessionUndoED>();
                    record.Initialize(_asset, _database, scope.Key, scope.Value);
                    _scopeUndo.Add(scope.Key, record);
                }
            }
        }

        private void RecordScopeUndo(string snapshot, HashSet<string> keys, string label)
        {
            Dictionary<string, string> previous = ReadScopes(_observedSnapshot);
            Dictionary<string, string> next = ReadScopes(snapshot);

            foreach (string key in keys)
            {
                if (!_scopeUndo.TryGetValue(key, out EndsSessionUndoED record))
                {
                    record = ScriptableObject.CreateInstance<EndsSessionUndoED>();
                    previous.TryGetValue(key, out string oldValue);
                    record.Initialize(_asset, _database, key, oldValue);
                    _scopeUndo.Add(key, record);
                }

                previous.TryGetValue(key, out string before);
                record.Snapshot = before;
                Undo.RegisterCompleteObjectUndo(record, label);
                next.TryGetValue(key, out string value);
                record.Snapshot = value;
            }
        }

        private bool ApplyScopeUndo()
        {
            Dictionary<string, string> current = ReadScopes(_observedSnapshot);
            Dictionary<string, string> replacements = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, EndsSessionUndoED> record in _scopeUndo)
            {
                current.TryGetValue(record.Key, out string value);

                if (record.Value.Snapshot != value) replacements.Add(record.Key, record.Value.Snapshot);
            }

            if (replacements.Count == 0) return false;

            HashSet<string> keys = new HashSet<string>(replacements.Keys, StringComparer.Ordinal);
            bool local = CanRestoreDialogScopes(replacements);
            DialogDatabase candidate;

            if (local) candidate = CreateScopeStage(keys);
            else candidate = ScriptableObject.CreateInstance<DialogDatabase>();

            try
            {
                string next = MergeScopes(_observedSnapshot, replacements);
                RequireScopeAccess(keys, _observedSnapshot, next);

                if (local)
                {
                    RestoreDialogScopes(candidate, replacements);
                    MergeNodeNumbers(candidate, true, keys);
                    next = SnapshotScopes(candidate, keys);
                    EndsDialogRulesED.ValidateDatabaseChanges(_database, candidate, keys);
                    ApplyPreparedScopes(candidate, keys, next);
                    _observedSnapshot = next;
                    _revision++;
                    StoreSessionState();
                }
                else
                {
                    EditorJsonUtility.FromJsonOverwrite(next, candidate);
                    EndsDialogRulesED.ValidateDatabaseChanges(_database, candidate);
                    EditorJsonUtility.FromJsonOverwrite(next, _database);
                    _catalogRevision++;
                }

                return local;
            }
            catch (Exception exception)
            {
                if (replacements.ContainsKey(SHARED_SCOPE)) EndsVariableRuntimeED.CancelDefinitionUndo(_database);

                foreach (KeyValuePair<string, string> replacement in replacements)
                {
                    current.TryGetValue(replacement.Key, out string value);
                    _scopeUndo[replacement.Key].Snapshot = value;
                }

                Debug.LogWarning("ENDS Undo was not applied: " + exception.Message);

                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
            }
        }

        #endregion SCOPED UNDO
    }
}
#endif