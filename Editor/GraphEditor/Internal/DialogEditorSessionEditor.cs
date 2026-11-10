#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("ALTUSHKA.EasyNodeDialogSystem.Tests")]

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    [InitializeOnLoad]
    internal sealed partial class EndsSessionED
    {
        #region FIELDS

        private const string SESSION_KEY_PREFIX = "ENDS.Session.";
        private static readonly Dictionary<DialogDatabase, EndsSessionED> Sessions = new Dictionary<DialogDatabase, EndsSessionED>();
        private static readonly Dictionary<DialogDatabase, EndsSessionED> _stagedSessions = new Dictionary<DialogDatabase, EndsSessionED>();

        internal sealed class Result
        {
            internal string Status;
            internal string Error;
            internal bool Changed;
            internal int Revision;
            internal int SavedRevision;
            internal bool Dirty;
            internal EndsMcpJsonED Validation;
            internal EndsMcpJsonED ErrorDetails;
            internal readonly List<string> Changes = new List<string>();
            internal readonly HashSet<string> ScopeKeys = new HashSet<string>(StringComparer.Ordinal);

            internal bool Success => Status == "applied" || Status == "unchanged" || Status == "saved" || Status == "discarded";
        }

        private readonly DialogDatabase _database;
        private readonly DialogDatabase _asset;
        private readonly Action<DialogDatabase> _persist;
        private readonly string _sessionKey;
        private string _savedSnapshot;
        private string _assetSnapshot;
        private string _observedSnapshot;
        private string _readOnlySnapshot;
        private DialogDatabase _readOnlyBaseline;
        private string _readOnlyDatabaseName;
        private HideFlags _readOnlyDatabaseFlags;
        private int _observedDirtyCount;
        private bool _validateReadOnlyView;
        private int _revision;
        private int _savedRevision;
        private bool _executing;
        private int _readOnlyViewDepth;
        private readonly Dictionary<string, int[]> _nodeNumbers = new Dictionary<string, int[]>(StringComparer.Ordinal);
        private bool _identityReady;
        private string _identityMigrationError;
        private bool _automaticColorsReady;
        private bool _externalAssetChange;
        private EndsNodeIdentityED.MigrationPlan _identityMigration = new EndsNodeIdentityED.MigrationPlan();
        private readonly HashSet<string> _reservedNodeIds = new HashSet<string>(StringComparer.Ordinal);

        internal static event Action<DialogDatabase> StateChanged;

        internal int Revision { get { if (_readOnlyViewDepth == 0) ObserveChanges(); return _revision; } }
        internal int ObservedRevision => _revision;
        internal int SavedRevision => _savedRevision;
        internal DialogDatabase WorkingDatabase => _database;
        internal DialogDatabase Asset => _asset;
        internal bool HasExternalAssetChange => _externalAssetChange;
        internal bool Dirty { get { if (_readOnlyViewDepth == 0) ObserveChanges(); return HasUnsavedChanges; } }
        private bool HasUnsavedChanges => DirtyScopes().Count > 0 || EndsLegacyNameColorMigrationED.NeedsSave(_database);

        #endregion FIELDS

        #region INITIALIZATION

        static EndsSessionED()
        {
            Undo.undoRedoPerformed += HandleUndoRedo;
            EditorApplication.update += UpdateCollaboration;
        }

        internal EndsSessionED(DialogDatabase database, Action<DialogDatabase> persist = null)
        {
            database = SourceAsset(database);
            EndsBlockIdentityED.Ensure(database, legacy: true);
            _asset = database;
            _persist = persist;
            string assetPath = AssetDatabase.GetAssetPath(database);

            _database = database;

            if (!string.IsNullOrEmpty(assetPath))
            {
                foreach (EndsSessionUndoED record in Resources.FindObjectsOfTypeAll<EndsSessionUndoED>())
                {
                    if (record.Asset == database && record.Working != null) _database = record.Working;
                }

                if (_database == database) _database = UnityEngine.Object.Instantiate(database);

                _database.name = database.name;
                _database.hideFlags = HideFlags.HideAndDontSave;
            }

            if (!string.IsNullOrEmpty(assetPath)) _sessionKey = SESSION_KEY_PREFIX + AssetDatabase.AssetPathToGUID(assetPath);

            EndsBlockIdentityED.Ensure(_database, legacy: true);
            _observedSnapshot = EditorJsonUtility.ToJson(_database);
            _savedSnapshot = _observedSnapshot;
            _assetSnapshot = EditorJsonUtility.ToJson(_asset);
            MergeNodeNumbers(database, false);

            if (_sessionKey != null)
            {
                _savedSnapshot = EndsBlockIdentityED.NormalizeSnapshot(EndsLegacyNameColorMigrationED.NormalizeSnapshot(SessionState.GetString(_sessionKey + ".saved", _observedSnapshot), migrateColors: false));
                _revision = SessionState.GetInt(_sessionKey + ".revision", 0);
                _savedRevision = SessionState.GetInt(_sessionKey + ".savedRevision", 0);
                string previousSnapshot = EndsBlockIdentityED.NormalizeSnapshot(SessionState.GetString(_sessionKey + ".observed", _observedSnapshot));
                ReadNodeNumbers(EndsMcpJsonED.Parse(SessionState.GetString(_sessionKey + ".nodeNumbers", "{}")));
                EndsNodeIdentityED.ReadPlan(EndsMcpJsonED.Parse(SessionState.GetString(_sessionKey + ".identityMigration", "{}")), _identityMigration);

                if (SessionState.GetBool(_sessionKey + ".workspace", false))
                {
                    string previousAsset = EndsBlockIdentityED.NormalizeSnapshot(SessionState.GetString(_sessionKey + ".asset", _savedSnapshot));
                    bool assetChanged = ChangedScopes(previousAsset, _assetSnapshot).Count > 0;

                    if (assetChanged && ChangedScopes(_savedSnapshot, previousSnapshot).Count == 0)
                    {
                        EditorJsonUtility.FromJsonOverwrite(_assetSnapshot, _database);
                        _database.hideFlags = HideFlags.HideAndDontSave;
                        _observedSnapshot = EditorJsonUtility.ToJson(_database);
                        _savedSnapshot = _observedSnapshot;
                        _savedRevision = ++_revision;
                        _identityMigration = new EndsNodeIdentityED.MigrationPlan();
                        _nodeNumbers.Clear();
                        MergeNodeNumbers(_database, false);

                        foreach (EndsSessionUndoED record in Resources.FindObjectsOfTypeAll<EndsSessionUndoED>())
                        {
                            if (record.Asset == _asset) Undo.ClearUndo(record);
                        }
                    }
                    else
                    {
                        EditorJsonUtility.FromJsonOverwrite(previousSnapshot, _database);
                        _observedSnapshot = previousSnapshot;
                        _externalAssetChange = assetChanged;

                        if (assetChanged) _assetSnapshot = previousAsset;
                    }
                }
                else
                {
                    if (previousSnapshot != _observedSnapshot) _revision++;

                    EditorJsonUtility.FromJsonOverwrite(_savedSnapshot, _asset);
                    EditorUtility.ClearDirty(_asset);
                    _assetSnapshot = EditorJsonUtility.ToJson(_asset);
                }

                StoreSessionState();
            }

            InitializeScopeUndo();
            _observedDirtyCount = EditorUtility.GetDirtyCount(_database);
        }

        /// <summary> ENDS. Uses imported data as the new saved baseline, pending work is retained for review only when explicitly requested. </summary>
        internal void ResolveExternalAssetChange(bool keepWorking)
        {
            if (!_externalAssetChange) return;
            if (CurrentActor != EDITOR_ACTOR) throw new InvalidOperationException("Resolve external database changes in the Unity editor.");

            RequireScopeAccess(new HashSet<string>(ReadScopes(_observedSnapshot).Keys, StringComparer.Ordinal), _savedSnapshot, _observedSnapshot);
            _assetSnapshot = EditorJsonUtility.ToJson(_asset);
            _savedSnapshot = EndsBlockIdentityED.NormalizeSnapshot(_assetSnapshot);

            if (keepWorking)
            {
                DialogDatabase imported = ReadSavedDatabase();

                try
                {
                    foreach (DialogEntry dialog in _database.Dialogs)
                    {
                        if (dialog == null || imported.Dialogs.Exists(entry => entry != null && entry.NodeIdentityKey == dialog.NodeIdentityKey)) continue;

                        DialogEntry replacement = imported.Dialogs.Find(entry => entry != null && entry.dialogName == dialog.dialogName);

                        if (replacement != null && !_database.Dialogs.Exists(entry => entry != null && entry != dialog && entry.NodeIdentityKey == replacement.NodeIdentityKey)) dialog.SetNodeIdentityState(replacement.NodeIdentityKey, dialog.NextNodeNumbers);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(imported);
                }
            }
            else EditorJsonUtility.FromJsonOverwrite(_savedSnapshot, _database);

            _database.hideFlags = HideFlags.HideAndDontSave;
            _observedSnapshot = EditorJsonUtility.ToJson(_database);
            _savedRevision = ++_revision;
            _identityMigration = new EndsNodeIdentityED.MigrationPlan();
            _identityMigrationError = null;
            _identityReady = false;
            _automaticColorsReady = false;
            _externalAssetChange = false;
            _nodeNumbers.Clear();
            _reservedNodeIds.Clear();
            MergeNodeNumbers(_database, false);

            foreach (EndsSessionUndoED record in _scopeUndo.Values)
            {
                Undo.ClearUndo(record);
            }

            InitializeScopeUndo();
            _catalogRevision++;
            EnsureNodeIdentity();
            EnsureLegacyNameColors();
            StoreSessionState();
            PublishStateChange();
        }

        internal static EndsSessionED ForDatabase(DialogDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            if (Sessions.TryGetValue(database, out EndsSessionED existing))
            {
                existing.EnsureNodeIdentity();
                existing.EnsureLegacyNameColors();

                return existing;
            }

            foreach (EndsSessionED candidate in Sessions.Values)
            {
                if (candidate._database != database) continue;

                candidate.EnsureNodeIdentity();
                candidate.EnsureLegacyNameColors();

                return candidate;
            }

            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(database)))
            {
                foreach (EndsSessionUndoED record in Resources.FindObjectsOfTypeAll<EndsSessionUndoED>())
                {
                    if (record.Working != database || record.Asset == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(record.Asset))) continue;

                    return ForDatabase(record.Asset);
                }
            }

            List<DialogDatabase> destroyed = new List<DialogDatabase>();

            foreach (DialogDatabase candidate in Sessions.Keys)
            {
                if (candidate == null) destroyed.Add(candidate);
            }

            foreach (DialogDatabase candidate in destroyed)
            {
                EndsSessionED obsolete = Sessions[candidate];

                foreach (EndsSessionUndoED record in obsolete._scopeUndo.Values)
                {
                    if (record == null) continue;

                    Undo.ClearUndo(record);
                    UnityEngine.Object.DestroyImmediate(record);
                }

                if (obsolete._database != null && obsolete._database != obsolete._asset) UnityEngine.Object.DestroyImmediate(obsolete._database);
                if (obsolete._readOnlyBaseline != null) UnityEngine.Object.DestroyImmediate(obsolete._readOnlyBaseline);

                Sessions.Remove(candidate);
            }

            if (!Sessions.TryGetValue(database, out EndsSessionED session))
            {
                session = new EndsSessionED(database);
                Sessions.Add(database, session);
            }

            session.EnsureNodeIdentity();
            session.EnsureLegacyNameColors();

            return session;
        }

        /// <summary> ENDS. Finds the session owning an asset, working copy or staged transaction without starting another identity migration. </summary>
        internal static bool TryGetSession(DialogDatabase database, out EndsSessionED session)
        {
            if (_stagedSessions.TryGetValue(database, out session)) return true;
            if (Sessions.TryGetValue(database, out session)) return true;

            foreach (EndsSessionED candidate in Sessions.Values)
            {
                if (candidate._database != database) continue;

                session = candidate;

                return true;
            }

            return false;
        }

        /// <summary> ENDS. Reserves a fresh number for one node type in the dialog, using the shared session high-water counters. </summary>
        internal string ReserveNodeId(DialogEntry dialog, NodeType type)
        {
            return ReserveIdentity(dialog, (int)type, EndsNodeIdentityED.Prefix(type));
        }


        /// <summary> ENDS. Reserves a readable ID for any registered block family, sharing node reservations that survive rejection, Discard, Undo and script reloads. </summary>
        internal string ReserveBlockId(DialogEntry dialog, EndsBlockIdentityED.IdentityKind kind)
        {
            return ReserveIdentity(dialog, kind.Counter, kind.Prefix);
        }

        private string ReserveIdentity(DialogEntry dialog, int counter, string prefix)
        {
            string key = dialog.NodeIdentityKey;

            if (string.IsNullOrEmpty(key))
            {
                DialogEntry current = _database.Dialogs.Find(candidate => candidate != null && candidate.dialogName == dialog.dialogName);

                if (current == null) throw new InvalidOperationException("Cannot allocate a node id without its dialog.");

                key = current.NodeIdentityKey;
            }

            if (!_nodeNumbers.TryGetValue(key, out int[] numbers))
            {
                numbers = EndsNodeIdentityED.ReadNumbers(dialog);
                _nodeNumbers.Add(key, numbers);
            }

            string id = prefix + "." + numbers[counter].ToString(System.Globalization.CultureInfo.InvariantCulture);
            numbers[counter] = checked(numbers[counter] + 1);
            _reservedNodeIds.Add(key + "/" + id);
            StoreSessionState();

            return id;
        }

        internal static bool IsReservedNodeId(DialogDatabase database, DialogEntry dialog, string id)
        {
            string key = EndsNodeIdentityED.ReservationKey(dialog, id);

            if (_stagedSessions.TryGetValue(database, out EndsSessionED staged)) return staged._reservedNodeIds.Contains(key);
            if (Sessions.TryGetValue(database, out EndsSessionED session)) return session._reservedNodeIds.Contains(key);

            return false;
        }

        private void MergeNodeNumbers(DialogDatabase database, bool write, ISet<string> scopes = null)
        {
            foreach (DialogEntry dialog in database.Dialogs ?? new List<DialogEntry>())
            {
                if (dialog == null || string.IsNullOrEmpty(dialog.NodeIdentityKey)) continue;
                if (scopes != null && !scopes.Contains(dialog.NodeIdentityKey)) continue;

                int[] numbers = EndsNodeIdentityED.ReadNumbers(dialog);

                if (_nodeNumbers.TryGetValue(dialog.NodeIdentityKey, out int[] reserved))
                {
                    for (int i = 0; i < numbers.Length && i < reserved.Length; i++)
                    {
                        numbers[i] = Math.Max(numbers[i], reserved[i]);
                    }
                }

                _nodeNumbers[dialog.NodeIdentityKey] = numbers;

                if (write) dialog.SetNodeIdentityState(dialog.NodeIdentityKey, numbers);
            }
        }

        private void ReadNodeNumbers(EndsMcpJsonED json)
        {
            foreach (string key in json.Keys)
            {
                int[] numbers = EndsNodeIdentityED.NewNumbers();
                EndsMcpJsonED values = json.Get(key);

                for (int i = 0; i < numbers.Length && i < values.Items.Count; i++)
                {
                    numbers[i] = Math.Max(numbers[i], EndsDialogGraphContractED.Integer(values.Items[i]));
                }

                if (_nodeNumbers.TryGetValue(key, out int[] existing))
                {
                    for (int i = 0; i < numbers.Length && i < existing.Length; i++)
                    {
                        numbers[i] = Math.Max(numbers[i], existing[i]);
                    }
                }

                _nodeNumbers[key] = numbers;
            }
        }

        private void EnsureNodeIdentity()
        {
            if (_identityReady || _executing || _readOnlyViewDepth > 0 || _externalAssetChange) return;

            DialogDatabase saved = ReadSavedDatabase();
            DialogDatabase baseline = EndsDialogRulesED.Snapshot(_database);
            string savedSnapshot = _savedSnapshot;

            try
            {
                _identityMigration = EndsNodeIdentityED.BuildPlan(saved, _database, _identityMigration);
                _savedSnapshot = EndsNodeIdentityED.NormalizeDialogKeys(_savedSnapshot, _identityMigration);
                EndsNodeIdentityED.ApplyPlan(baseline, _identityMigration);
                _identityMigrationError = null;
                Result result = Execute(staged => EndsNodeIdentityED.ApplyPlan(staged, _identityMigration), "Migrate ENDS node identities", validationBaseline: baseline);

                if (!result.Success) throw new InvalidOperationException("Node identity migration failed: " + result.Error);

                EndsAiEditMemoryED.Find(_database)?.RemapNodeIds(_identityMigration);
                _identityMigrationError = null;
                _identityReady = true;
                StoreSessionState();
            }
            catch (Exception exception)
            {
                _savedSnapshot = savedSnapshot;
                _identityMigrationError = exception.Message;
                _identityReady = true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(saved);
                UnityEngine.Object.DestroyImmediate(baseline);
            }
        }

        private static void HandleUndoRedo()
        {
            foreach (KeyValuePair<DialogDatabase, EndsSessionED> entry in Sessions)
            {
                if (entry.Key == null || entry.Value._database == null) continue;

                string beforeUndo = entry.Value._observedSnapshot;
                bool restoredScope = entry.Value.ApplyScopeUndo();

                if (!restoredScope && entry.Value._identityMigrationError == null && entry.Value._identityReady)
                {
                    EndsNodeIdentityED.ApplyPlan(entry.Value._database, entry.Value._identityMigration);
                    entry.Value.MergeNodeNumbers(entry.Value._database, true);
                }

                if (!restoredScope) entry.Value.ObserveChanges(true);

                if (beforeUndo != entry.Value._observedSnapshot) entry.Value.RecordCollaborationChange(beforeUndo, entry.Value._observedSnapshot, "Undo/Redo", ChangedScopes(beforeUndo, entry.Value._observedSnapshot), true);

                Dictionary<string, string> scopes = ReadScopes(entry.Value._observedSnapshot);

                foreach (KeyValuePair<string, EndsSessionUndoED> scope in entry.Value._scopeUndo)
                {
                    scopes.TryGetValue(scope.Key, out string value);
                    scope.Value.Snapshot = value;
                }

                entry.Value.PublishStateChange();
            }
        }

        #endregion INITIALIZATION

        #region TRANSACTIONS

        internal Result Execute(Action<DialogDatabase> prepare, string label, int? expectedRevision = null, bool save = false, IReadOnlyList<string> changes = null, DialogDatabase validationBaseline = null,
            IReadOnlyList<EndsVariableRuntimeED.DefinitionEdit> variableEdits = null, bool restoring = false, ISet<string> dialogScopes = null)
        {
            ObserveChanges();

            if (_executing) return CreateResult("rejected", "A database operation is already running.");
            if (_externalAssetChange) return CreateResult("conflict", "The database was changed outside ENDS. Resolve the external change in the Unity editor before editing. Pending work has been preserved.");
            if (expectedRevision.HasValue && expectedRevision.Value != _revision) return CreateResult("conflict", "Database revision changed. Read the current state before retrying.");
            if (_identityMigrationError != null) return CreateResult("rejected", "Repair node identities before editing: " + _identityMigrationError);

            _executing = true;
            string beforeSnapshot = _observedSnapshot;
            int beforeRevision = _revision;
            int undoGroup = -1;
            DialogDatabase staged = null;
            Result result;

            try
            {
                staged = CreateScopeStage(dialogScopes);
                _stagedSessions.Add(staged, this);

                MergeNodeNumbers(staged, true, dialogScopes);
                prepare(staged);

                if (restoring && _identityMigration.Dialogs.Count > 0) EndsNodeIdentityED.ApplyPlan(staged, _identityMigration);

                EndsBlockIdentityED.Ensure(staged, scopes: dialogScopes);
                EndsDialogRulesED.ValidateDatabaseChanges(validationBaseline ?? _database, staged, dialogScopes);

                bool addedNodes = false;

                if (!restoring && validationBaseline == null && _database.NodeIdentityVersion == EndsNodeIdentityED.VERSION) addedNodes = ValidateNewNodeIds(_database, staged, dialogScopes);

                MergeNodeNumbers(staged, true, dialogScopes);
                string preparedSnapshot;

                if (dialogScopes == null) preparedSnapshot = EditorJsonUtility.ToJson(staged);
                else preparedSnapshot = SnapshotScopes(staged, dialogScopes);

                if (!addedNodes && validationBaseline == null)
                {
                    foreach (DialogEntry dialog in staged.Dialogs ?? new List<DialogEntry>())
                    {
                        if (dialogScopes != null && (dialog == null || !dialogScopes.Contains(dialog.NodeIdentityKey))) continue;

                        DialogEntry previous = _database.Dialogs.Find(candidate => candidate != null && candidate.NodeIdentityKey == dialog?.NodeIdentityKey);

                        if (previous != null) dialog.SetNodeIdentityState(previous.NodeIdentityKey, previous.NextNodeNumbers);
                    }

                    string stableSnapshot;

                    if (dialogScopes == null) stableSnapshot = EditorJsonUtility.ToJson(staged);
                    else stableSnapshot = SnapshotScopes(staged, dialogScopes);

                    if (ChangedScopes(_observedSnapshot, stableSnapshot).Count == 0) preparedSnapshot = _observedSnapshot;
                }

                if (preparedSnapshot == _observedSnapshot || ChangedScopes(_observedSnapshot, preparedSnapshot).Count == 0) result = CreateResult("unchanged");
                else
                {
                    HashSet<string> affectedScopes = ChangedScopes(_observedSnapshot, preparedSnapshot);
                    RequireScopeAccess(affectedScopes, _observedSnapshot, preparedSnapshot);
                    Undo.IncrementCurrentGroup();
                    undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName(label);
                    RecordScopeUndo(preparedSnapshot, affectedScopes, label);
                    if (dialogScopes == null) EndsVariableRuntimeED.PrepareDefinitions(_database, staged, label, variableEdits, discard: restoring);

                    MergeNodeNumbers(staged, true, dialogScopes);

                    ApplyPreparedScopes(staged, affectedScopes, preparedSnapshot);

                    if (dialogScopes == null) _catalogRevision++;

                    Undo.CollapseUndoOperations(undoGroup);
                    EditorUtility.SetDirty(_database);
                    _observedSnapshot = preparedSnapshot;
                    _revision++;
                    StoreSessionState();
                    result = CreateResult("applied");
                    result.Changed = true;
                    result.ScopeKeys.UnionWith(affectedScopes);
                    RecordCollaborationChange(beforeSnapshot, preparedSnapshot, label, affectedScopes, restoring);

                    ForgetCommittedReservations(dialogScopes);

                    if (changes != null) result.Changes.AddRange(changes);

                    PublishStateChange();
                }
            }
            catch (Exception exception)
            {
                if (undoGroup >= 0)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    EditorJsonUtility.FromJsonOverwrite(beforeSnapshot, _database);
                    _observedSnapshot = beforeSnapshot;
                    _revision = beforeRevision;
                    _catalogRevision++;
                    StoreSessionState();
                }

                result = Reject(exception);
            }
            finally
            {
                if (staged != null)
                {
                    MergeNodeNumbers(staged, false, dialogScopes);
                    _stagedSessions.Remove(staged);
                    UnityEngine.Object.DestroyImmediate(staged);
                    StoreSessionState();
                }

                _executing = false;
            }

            if (result.Success && save)
            {
                Result saved = SaveScopes(result.ScopeKeys, _revision);
                saved.Changed |= result.Changed;
                saved.Changes.AddRange(result.Changes);
                saved.ScopeKeys.UnionWith(result.ScopeKeys);

                return saved;
            }

            return result;
        }

        private bool ValidateNewNodeIds(DialogDatabase before, DialogDatabase staged, ISet<string> selected = null)
        {
            bool addedNodes = false;
            HashSet<string> scopes = new HashSet<string>(StringComparer.Ordinal);

            foreach (DialogEntry dialog in staged.Dialogs ?? new List<DialogEntry>())
            {
                if (dialog == null) continue;
                if (selected != null && !selected.Contains(dialog.NodeIdentityKey)) continue;
                if (string.IsNullOrEmpty(dialog.NodeIdentityKey) || !scopes.Add(dialog.NodeIdentityKey)) throw new InvalidOperationException("Each dialog must have its own internal identity scope.");

                DialogEntry previous = before.Dialogs?.Find(candidate => candidate != null && candidate.NodeIdentityKey == dialog.NodeIdentityKey);
                int[] numbers = EndsNodeIdentityED.NewNumbers();

                if (previous != null) numbers = EndsNodeIdentityED.ReadNumbers(previous);

                if (previous != null && _nodeNumbers.TryGetValue(dialog.NodeIdentityKey, out int[] reserved))
                {
                    for (int type = 0; type < numbers.Length && type < reserved.Length; type++)
                    {
                        numbers[type] = Math.Max(numbers[type], reserved[type]);
                    }
                }

                HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

                foreach (DialogNode node in dialog.nodes ?? new List<DialogNode>())
                {
                    if (node == null) continue;
                    if (!ids.Add(node.nodeId)) throw new InvalidOperationException("Node ids must be unique within their dialog.");
                    if (previous?.nodes != null && previous.nodes.Exists(candidate => candidate != null && candidate.nodeId == node.nodeId)) continue;

                    addedNodes = true;

                    if (!EndsNodeIdentityED.IsReadable(node.nodeId, node.NodeType)) throw new InvalidOperationException("A new node id must use its type prefix and a positive number.");

                    EndsNodeIdentityED.TryReadNumber(node.nodeId, out int number);

                    if (number < numbers[(int)node.NodeType] && !_reservedNodeIds.Contains(EndsNodeIdentityED.ReservationKey(dialog, node.nodeId))) throw new InvalidOperationException("The node number was already allocated for this type in this dialog.");
                }

                var previousBlocks = new Dictionary<string, EndsBlockIdentityED.IdentityKind>(StringComparer.Ordinal);

                if (previous != null)
                {
                    foreach (EndsBlockIdentityED.Entry entry in EndsBlockIdentityED.ReadEntries(previous))
                    {
                        previousBlocks[entry.Block.BlockId] = entry.Kind;
                    }
                }

                var blocks = new HashSet<string>(StringComparer.Ordinal);

                foreach (EndsBlockIdentityED.Entry entry in EndsBlockIdentityED.ReadEntries(dialog))
                {
                    string identity = entry.Block.BlockId;

                    if (!blocks.Add(identity)) throw new InvalidOperationException("Block IDs must be unique within their dialog.");
                    if (previousBlocks.TryGetValue(identity, out EndsBlockIdentityED.IdentityKind oldKind) && oldKind == entry.Kind) continue;

                    addedNodes = true;

                    if (!entry.Kind.TryReadNumber(identity, out int number)) throw new InvalidOperationException("A new block ID must use " + entry.Kind.Prefix + ". followed by a positive number without leading zeros.");
                    if (number < numbers[entry.Kind.Counter] && !_reservedNodeIds.Contains(EndsNodeIdentityED.ReservationKey(dialog, identity))) throw new InvalidOperationException("The block number was already allocated in this dialog.");
                }
            }

            return addedNodes;
        }

        private void ForgetCommittedReservations(ISet<string> selected = null)
        {
            foreach (DialogEntry dialog in _database.Dialogs ?? new List<DialogEntry>())
            {
                if (dialog?.nodes == null) continue;
                if (selected != null && !selected.Contains(dialog.NodeIdentityKey)) continue;

                foreach (DialogNode node in dialog.nodes)
                {
                    if (node != null) _reservedNodeIds.Remove(EndsNodeIdentityED.ReservationKey(dialog, node.nodeId));

                }

                foreach (EndsBlockIdentityED.Entry entry in EndsBlockIdentityED.ReadEntries(dialog))
                {
                    _reservedNodeIds.Remove(EndsNodeIdentityED.ReservationKey(dialog, entry.Block.BlockId));
                }
            }
        }

        internal Result Reject(Exception exception)
        {
            Result result = CreateResult("rejected", exception.Message);

            if (exception is EndsDialogGraphContractED.OperationFailure operation) result.ErrorDetails = operation.ReadDetails();

            Exception cause = exception;

            while (cause != null)
            {
                if (cause is EndsDialogRulesED.GraphValidationException validation)
                {
                    if (exception is EndsDialogGraphContractED.OperationFailure context)
                    {
                        foreach (EndsDialogRulesED.GraphValidationIssue issue in validation.Issues)
                        {
                            if (string.IsNullOrEmpty(issue.Dialog)) issue.Dialog = context.Command?.Get("dialog")?.Text;
                            if (string.IsNullOrEmpty(issue.Node)) issue.Node = context.Command?.Get("node")?.Text;
                        }
                    }

                    result.Validation = EndsDialogGraphContractED.WriteValidationReport(validation.Issues);
                }

                cause = cause.InnerException;
            }

            return result;
        }

        #endregion TRANSACTIONS

        #region SAVE AND DISCARD

        private void EnsureLegacyNameColors()
        {
            if (_automaticColorsReady || _executing || _readOnlyViewDepth > 0 || _identityMigrationError != null || _externalAssetChange) return;

            _automaticColorsReady = true;

            if (!EndsLegacyNameColorMigrationED.RequiresMigration(_database)) return;

            Result result = MigrateLegacyNameColors();

            if (!result.Success) Debug.LogWarning("ENDS: automatic name color migration failed. Repair the affected name color before saving. " + result.Error, _asset);
        }

        internal Result MigrateLegacyNameColors()
        {
            string convertedSaved = EndsLegacyNameColorMigrationED.NormalizeSnapshot(_savedSnapshot);
            Result result = Execute(staged => EndsLegacyNameColorMigrationED.MigrateDatabase(staged), "Migrate ENDS legacy name colors", changes: new[] { "database/nameColors" });

            if (!result.Success) return result;

            _savedSnapshot = convertedSaved;

            if (result.Changed) EndsLegacyNameColorMigrationED.MarkNeedsSave(_database);

            StoreSessionState();
            result.Dirty = HasUnsavedChanges;
            PublishStateChange();

            return result;
        }

        internal Result Save(int? expectedRevision = null)
        {
            return SaveScopes(null, expectedRevision);
        }

        internal Result Discard(int? expectedRevision = null)
        {
            return DiscardScopes(null, expectedRevision);
        }

        internal DialogDatabase ReadSavedDatabase()
        {
            DialogDatabase saved = ScriptableObject.CreateInstance<DialogDatabase>();
            EditorJsonUtility.FromJsonOverwrite(_savedSnapshot, saved);

            if (_identityMigration.Dialogs.Count > 0) EndsNodeIdentityED.ApplyPlan(saved, _identityMigration);

            return saved;
        }

        internal List<DialogEntry> ReadSavedDialogs()
        {
            DialogDatabase saved = ReadSavedDatabase();

            try
            {
                return saved.Dialogs;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(saved);
            }
        }

        #endregion SAVE AND DISCARD

        #region STATE

        internal void ObserveChanges(bool allowExternal = false)
        {
            _observedDirtyCount = EditorUtility.GetDirtyCount(_database);

            if (_readOnlyBaseline != null && _readOnlySnapshot == _observedSnapshot && _readOnlyDatabaseName == _database.name && _readOnlyDatabaseFlags == _database.hideFlags && EndsDialogRulesED.HasSameStoredValue(_readOnlyBaseline, _database)) return;

            string current = EditorJsonUtility.ToJson(_database);

            if (current == _observedSnapshot || ChangedScopes(_observedSnapshot, current).Count == 0) return;

            if (_readOnlyViewDepth > 0 && !_executing && !allowExternal)
            {
                EditorJsonUtility.FromJsonOverwrite(_observedSnapshot, _database);

                throw new InvalidOperationException("An editor view changed the live database directly. Submit a registered operation or edit the detached graph draft.");
            }

            if (EndsBlockIdentityED.Ensure(_database)) current = EditorJsonUtility.ToJson(_database);

            _observedSnapshot = current;

            if (!_executing && (_identityMigrationError != null || _database.NodeIdentityVersion < EndsNodeIdentityED.VERSION)) _identityReady = false;

            // Observed external edits become the current state, stale records must not look like unrelated native Undo changes.
            Dictionary<string, string> observedScopes = ReadScopes(_observedSnapshot);

            foreach (KeyValuePair<string, EndsSessionUndoED> record in _scopeUndo)
            {
                observedScopes.TryGetValue(record.Key, out string snapshot);
                record.Value.Snapshot = snapshot;
            }

            MergeNodeNumbers(_database, false);
            _revision++;
            StoreSessionState();
        }

        internal EndsMcpJsonED ReadState()
        {
            ObserveChanges();
            EndsMcpJsonED state = EndsMcpJsonED.Obj();
            state.Set("database", EndsMcpJsonED.Str(AssetPath(_database)));
            state.Set("revision", EndsMcpJsonED.Num(_revision));
            state.Set("savedRevision", EndsMcpJsonED.Num(_savedRevision));
            state.Set("dirty", EndsMcpJsonED.Boolean(HasUnsavedChanges));
            state.Set("contractVersion", EndsMcpJsonED.Num(EndsDialogGraphContractED.CONTRACT_VERSION));
            state.Set("migratedNodeCount", EndsMcpJsonED.Num(_identityMigration.ReplacementCount));
            state.Set("collaboration", ReadCollaboration());

            if (_identityMigrationError != null) state.Set("identityMigrationError", EndsMcpJsonED.Str(_identityMigrationError));

            return state;
        }

        internal EndsMcpJsonED ReadNodeIdMigration()
        {
            EndsNodeIdentityED.MigrationPlan active = new EndsNodeIdentityED.MigrationPlan();

            foreach (DialogEntry dialog in _database.Dialogs)
            {
                if (dialog == null || !_identityMigration.Dialogs.TryGetValue(dialog.NodeIdentityKey ?? "", out EndsNodeIdentityED.DialogPlan scope)) continue;

                scope.Name = dialog.dialogName;
                scope.Names.Add(dialog.dialogName);
                active.Dialogs.Add(scope.Key, scope);
            }

            return EndsNodeIdentityED.WritePlan(active);
        }

        internal static EndsMcpJsonED WriteResult(Result result)
        {
            EndsMcpJsonED json = EndsMcpJsonED.Obj();
            json.Set("status", EndsMcpJsonED.Str(result.Status));
            json.Set("revision", EndsMcpJsonED.Num(result.Revision));
            json.Set("savedRevision", EndsMcpJsonED.Num(result.SavedRevision));
            json.Set("dirty", EndsMcpJsonED.Boolean(result.Dirty));
            json.Set("changed", EndsMcpJsonED.Boolean(result.Changed));
            EndsMcpJsonED changes = EndsMcpJsonED.Arr();

            foreach (string change in result.Changes)
            {
                changes.Add(EndsMcpJsonED.Str(change));
            }

            json.Set("changes", changes);

            if (!string.IsNullOrEmpty(result.Error)) json.Set("error", EndsMcpJsonED.Str(result.Error));
            if (result.Validation != null) json.Set("validation", result.Validation);
            if (result.ErrorDetails != null) json.Set("errorDetails", result.ErrorDetails);

            return json;
        }

        /// <summary> ENDS. Protects interactive views with a full write check, passive rendering uses cached state and observes only Unity-notified external changes. </summary>
        internal IDisposable OpenReadOnlyView(bool validateWrites = true)
        {
            bool entering = _readOnlyViewDepth == 0;
            bool upgrade = !entering && validateWrites && !_validateReadOnlyView;

            if (entering) _validateReadOnlyView = validateWrites;
            else if (validateWrites) _validateReadOnlyView = true;

            if ((_validateReadOnlyView && (entering || upgrade || _readOnlySnapshot != _observedSnapshot)) || (entering && _observedDirtyCount != EditorUtility.GetDirtyCount(_database))) ObserveChanges();

            if (_validateReadOnlyView && _readOnlySnapshot != _observedSnapshot)
            {
                if (_readOnlyBaseline != null) UnityEngine.Object.DestroyImmediate(_readOnlyBaseline);

                _readOnlyBaseline = EndsDialogRulesED.Snapshot(_database);
                _readOnlyBaseline.hideFlags = HideFlags.HideAndDontSave;
                _readOnlySnapshot = _observedSnapshot;
                _readOnlyDatabaseName = _database.name;
                _readOnlyDatabaseFlags = _database.hideFlags;
            }

            return new ReadOnlyView(this);
        }

        private sealed class ReadOnlyView : IDisposable
        {
            private readonly EndsSessionED _session;
            private bool _disposed;

            internal ReadOnlyView(EndsSessionED session)
            {
                _session = session;
                _session._readOnlyViewDepth++;
            }

            public void Dispose()
            {
                if (_disposed) return;

                _disposed = true;

                try
                {
                    if (_session._readOnlyViewDepth == 1 && (_session._validateReadOnlyView || _session._observedDirtyCount != EditorUtility.GetDirtyCount(_session._database))) _session.ObserveChanges();
                }
                finally
                {
                    _session._readOnlyViewDepth--;

                    if (_session._readOnlyViewDepth == 0) _session._validateReadOnlyView = false;
                }
            }
        }

        private void PublishStateChange()
        {
            Action<DialogDatabase> listeners = StateChanged;

            if (listeners == null) return;

            foreach (Action<DialogDatabase> listener in listeners.GetInvocationList())
            {
                try
                {
                    listener(_database);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private Result CreateResult(string status, string error = null)
        {
            return new Result { Status = status, Error = error, Revision = _revision, SavedRevision = _savedRevision, Dirty = HasUnsavedChanges };
        }

        private void StoreSessionState()
        {
            _observedDirtyCount = EditorUtility.GetDirtyCount(_database);

            if (_sessionKey == null) return;

            SessionState.SetString(_sessionKey + ".saved", _savedSnapshot);
            SessionState.SetString(_sessionKey + ".asset", _assetSnapshot);
            SessionState.SetString(_sessionKey + ".observed", _observedSnapshot);
            SessionState.SetInt(_sessionKey + ".revision", _revision);
            SessionState.SetInt(_sessionKey + ".savedRevision", _savedRevision);
            SessionState.SetBool(_sessionKey + ".workspace", true);
            EndsMcpJsonED counters = EndsMcpJsonED.Obj();

            foreach (KeyValuePair<string, int[]> scope in _nodeNumbers)
            {
                EndsMcpJsonED numbers = EndsMcpJsonED.Arr();

                foreach (int number in scope.Value)
                {
                    numbers.Add(EndsMcpJsonED.Num(number));
                }

                counters.Set(scope.Key, numbers);
            }

            SessionState.SetString(_sessionKey + ".nodeNumbers", counters.Write());
            SessionState.SetString(_sessionKey + ".identityMigration", EndsNodeIdentityED.WritePlan(_identityMigration, true).Write());
        }

        #endregion STATE
    }
}
#endif