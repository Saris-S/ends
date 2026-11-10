#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary> Owns editor access to G, definition synchronization, and explicit file operations for UI and MCP. </summary>
    [InitializeOnLoad]
    internal static class EndsVariableRuntimeED
    {
        #region FIELDS

        private static readonly Dictionary<DialogDatabase, RuntimeState> _editStates = new Dictionary<DialogDatabase, RuntimeState>();
        private static readonly Dictionary<DialogDatabase, RuntimeState> _playStates = new Dictionary<DialogDatabase, RuntimeState>();
        private static RuntimeState _activeState;

        internal sealed class DefinitionEdit
        {
            internal string Operation;
            internal string Name;
            internal string NewName;
        }

        [Serializable]
        private sealed class Binding
        {
            public string Name;
            public ConditionVariableType Type;
            public int Token;
        }

        private sealed class BindingBook : ScriptableObject
        {
            [SerializeField] internal List<Binding> Bindings = new List<Binding>();
        }

        [Serializable]
        private sealed class StoredValue
        {
            public int Token;
            public ConditionVariableType Type;
            public bool BoolValue;
            public int IntValue;
            public float FloatValue;
            public string StringValue;
        }

        private sealed class RuntimeState : ScriptableObject
        {
            [SerializeField] internal DialogDatabase Database;
            [SerializeField] internal BindingBook Book;
            [SerializeField] internal List<Binding> Applied = new List<Binding>();
            [SerializeField] internal List<Binding> Saved = new List<Binding>();
            [SerializeField] internal List<StoredValue> StoredValues = new List<StoredValue>();
            [SerializeField] internal int NextToken;
            [SerializeField] internal bool PlayState;
            [NonSerialized] internal readonly Dictionary<(int, ConditionVariableType), object> Values = new Dictionary<(int, ConditionVariableType), object>();
            internal G.FileResult LastFileResult;
        }

        #endregion FIELDS

        #region LIFETIME

        static EndsVariableRuntimeED()
        {
            foreach (RuntimeState state in Resources.FindObjectsOfTypeAll<RuntimeState>())
            {
                if (state.Database == null || state.Book == null)
                {
                    if (state.Book != null) UnityEngine.Object.DestroyImmediate(state.Book);

                    UnityEngine.Object.DestroyImmediate(state);

                    continue;
                }

                foreach (StoredValue stored in state.StoredValues)
                {
                    object value;

                    if (stored.Type == ConditionVariableType.Bool) value = stored.BoolValue;
                    else if (stored.Type == ConditionVariableType.Int) value = stored.IntValue;
                    else if (stored.Type == ConditionVariableType.Float) value = stored.FloatValue;
                    else value = stored.StringValue ?? "";

                    state.Values[(stored.Token, stored.Type)] = value;
                }

                Dictionary<DialogDatabase, RuntimeState> states = state.PlayState ? _playStates : _editStates;
                EndsSessionED session = EndsSessionED.ForDatabase(state.Database);

                if (state.PlayState) state.Database = session.Asset;
                else state.Database = session.WorkingDatabase;

                states[state.Database] = state;
            }

            EndsSessionED.StateChanged += HandleDatabaseChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += HandleAssemblyReload;
        }

        private static void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.ExitingPlayMode)
            {
                CaptureCurrentValues(_activeState);
                _activeState = null;
            }

            if (change != PlayModeStateChange.EnteredEditMode) return;

            foreach (RuntimeState state in _playStates.Values)
            {
                UnityEngine.Object.DestroyImmediate(state.Book);
                UnityEngine.Object.DestroyImmediate(state);
            }

            _playStates.Clear();
        }

        private static void HandleAssemblyReload()
        {
            CaptureCurrentValues(_activeState);
            List<RuntimeState> states = new List<RuntimeState>(_editStates.Values);
            states.AddRange(_playStates.Values);

            foreach (RuntimeState state in states)
            {
                state.StoredValues.Clear();

                foreach (KeyValuePair<(int, ConditionVariableType), object> current in state.Values)
                {
                    StoredValue stored = new StoredValue { Token = current.Key.Item1, Type = current.Key.Item2 };

                    if (stored.Type == ConditionVariableType.Bool) stored.BoolValue = (bool)current.Value;
                    else if (stored.Type == ConditionVariableType.Int) stored.IntValue = (int)current.Value;
                    else if (stored.Type == ConditionVariableType.Float) stored.FloatValue = (float)current.Value;
                    else stored.StringValue = (string)current.Value;

                    state.StoredValues.Add(stored);
                }
            }
        }

        #endregion LIFETIME

        #region CURRENT VALUES

        internal static void EnsureRuntime(DialogDatabase database)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");

            if (Application.isPlaying) database = EndsSessionED.SourceAsset(database);
            else database = EndsSessionED.ForDatabase(database).WorkingDatabase;

            if (Application.isPlaying && (DialogRunner.Instance == null || DialogRunner.Instance.DialogDatabase != database)) throw new InvalidOperationException("Current values belong to the active DialogRunner database in Play Mode.");

            Dictionary<DialogDatabase, RuntimeState> states = Application.isPlaying ? _playStates : _editStates;

            if (!states.TryGetValue(database, out RuntimeState state))
            {
                BindingBook book = ScriptableObject.CreateInstance<BindingBook>();
                book.hideFlags = HideFlags.HideAndDontSave;
                state = ScriptableObject.CreateInstance<RuntimeState>();
                state.hideFlags = HideFlags.HideAndDontSave;
                state.Database = database;
                state.Book = book;
                state.PlayState = Application.isPlaying;

                try
                {
                    Dictionary<DialogDatabase, RuntimeState> peerStates = Application.isPlaying ? _editStates : _playStates;
                    peerStates.TryGetValue(database, out RuntimeState peer);

                    if (peer != null) state.NextToken = peer.NextToken;

                    book.Bindings = BuildBindings(state, database, peer == null ? book.Bindings : peer.Book.Bindings);
                    DialogDatabase saved = EndsSessionED.ForDatabase(database).ReadSavedDatabase();

                    try { state.Saved = BuildBindings(state, saved, peer == null ? book.Bindings : peer.Saved); }
                    finally { UnityEngine.Object.DestroyImmediate(saved); }

                    CaptureCurrentValues(_activeState);

                    if (Application.isPlaying || G.DefinitionSource == database) G.EnsureInitialized(database);
                    else
                    {
                        G.InitializeFromDefinitions(database);
                        state.LastFileResult = G.TryLoadGame();
                    }

                    states.Add(database, state);
                    state.Applied = CopyBindings(book.Bindings);
                    _activeState = state;
                    CaptureCurrentValues(state);

                    return;
                }
                catch
                {
                    UnityEngine.Object.DestroyImmediate(book);
                    UnityEngine.Object.DestroyImmediate(state);

                    throw;
                }
            }

            if (_activeState != state || G.DefinitionSource != database)
            {
                CaptureCurrentValues(_activeState);
                SynchronizeDefinitions(state);
                _activeState = state;
                ApplyCurrentValues(state);
            }
        }

        internal static G.FileResult LastFileResult(DialogDatabase database)
        {
            Dictionary<DialogDatabase, RuntimeState> states = Application.isPlaying ? _playStates : _editStates;

            return states.TryGetValue(database, out RuntimeState state) ? state.LastFileResult : null;
        }

        internal static void SetCurrent(DialogDatabase database, string name, object value)
        {
            if (Application.isPlaying) database = EndsSessionED.SourceAsset(database);
            else database = EndsSessionED.ForDatabase(database).WorkingDatabase;

            EnsureRuntime(database);
            VariableDefinition definition = EndsVariableCommandsED.Find(database, name);

            if (definition == null) throw new InvalidOperationException("Variable not found.");
            if (definition.variableType == ConditionVariableType.Bool && value is bool boolean) G.SetBool(name, boolean);
            else if (definition.variableType == ConditionVariableType.Int && value is int integer) G.SetInt(name, integer);
            else if (definition.variableType == ConditionVariableType.Float && value is float number && !float.IsNaN(number) && !float.IsInfinity(number)) G.SetFloat(name, number);
            else if (definition.variableType == ConditionVariableType.String && value is string text) G.SetString(name, text);
            else throw new InvalidOperationException("Current value must match the variable type and numeric values must be finite.");
        }

        private static void CaptureCurrentValues(RuntimeState state)
        {
            if (state == null || G.DefinitionSource != state.Database) return;

            foreach (Binding binding in state.Applied)
            {
                object value;

                if (binding.Type == ConditionVariableType.Bool) value = G.GetBool(binding.Name);
                else if (binding.Type == ConditionVariableType.Int) value = G.GetInt(binding.Name);
                else if (binding.Type == ConditionVariableType.Float) value = G.GetFloat(binding.Name);
                else value = G.GetString(binding.Name);

                state.Values[(binding.Token, binding.Type)] = value;
            }
        }

        private static void ApplyCurrentValues(RuntimeState state)
        {
            G.InitializeFromDefinitions(state.Database);

            foreach (Binding binding in state.Book.Bindings)
            {
                if (!state.Values.TryGetValue((binding.Token, binding.Type), out object value)) continue;
                if (binding.Type == ConditionVariableType.Bool) G.SetBool(binding.Name, (bool)value);
                else if (binding.Type == ConditionVariableType.Int) G.SetInt(binding.Name, (int)value);
                else if (binding.Type == ConditionVariableType.Float) G.SetFloat(binding.Name, (float)value);
                else G.SetString(binding.Name, (string)value);
            }

            state.Applied = CopyBindings(state.Book.Bindings);
        }

        #endregion CURRENT VALUES

        #region DEFINITION HISTORY

        internal static void PrepareDefinitions(DialogDatabase database, DialogDatabase target, string label, IReadOnlyList<DefinitionEdit> edits = null, bool discard = false)
        {
            foreach (RuntimeState state in StatesForDatabase(database))
            {
                if (state == _activeState) CaptureCurrentValues(state);

                List<Binding> candidates = CopyBindings(discard ? state.Saved : state.Book.Bindings);

                if (edits != null)
                {
                    foreach (DefinitionEdit edit in edits)
                    {
                        if (edit.Operation == "variable.remove" || edit.Operation == "variable.add") candidates.RemoveAll(binding => binding.Name == edit.Name);
                        else if (edit.Operation == "variable.rename")
                        {
                            foreach (Binding binding in candidates)
                            {
                                if (binding.Name == edit.Name) binding.Name = edit.NewName;
                            }
                        }
                    }
                }

                List<Binding> bindings = BuildBindings(state, target, candidates);

                if (SameBindings(state.Book.Bindings, bindings)) continue;

                Undo.RegisterCompleteObjectUndo(state.Book, label);
                state.Book.Bindings = bindings;
            }
        }

        internal static void RememberSavedDefinitions(DialogDatabase database)
        {
            foreach (RuntimeState state in StatesForDatabase(database))
            {
                SynchronizeDefinitions(state);
                state.Saved = CopyBindings(state.Book.Bindings);
            }
        }

        internal static void CancelDefinitionUndo(DialogDatabase database)
        {
            foreach (RuntimeState state in StatesForDatabase(database)) state.Book.Bindings = CopyBindings(state.Applied);
        }

        private static void HandleDatabaseChanged(DialogDatabase database)
        {
            foreach (RuntimeState state in StatesForDatabase(database)) SynchronizeDefinitions(state);
        }

        private static IEnumerable<RuntimeState> StatesForDatabase(DialogDatabase database)
        {
            if (_editStates.TryGetValue(database, out RuntimeState editState)) yield return editState;
            if (_playStates.TryGetValue(database, out RuntimeState playState)) yield return playState;
        }

        private static void SynchronizeDefinitions(RuntimeState state)
        {
            List<Binding> bindings = BuildBindings(state, state.Database, state.Book.Bindings);

            if (state == _activeState) CaptureCurrentValues(state);

            state.Book.Bindings = bindings;

            if (state == _activeState && !SameBindings(state.Applied, bindings)) ApplyCurrentValues(state);
        }

        private static List<Binding> BuildBindings(RuntimeState state, DialogDatabase database, List<Binding> candidates)
        {
            List<Binding> bindings = new List<Binding>();

            foreach (VariableDefinition definition in database.Variables ?? new List<VariableDefinition>())
            {
                if (definition == null || string.IsNullOrEmpty(definition.variableName)) throw new InvalidOperationException("Variable definition is missing its name.");

                Binding previous = candidates.Find(binding => binding.Name == definition.variableName);
                Binding binding = new Binding { Name = definition.variableName, Type = definition.variableType, Token = previous == null ? ++state.NextToken : previous.Token };
                bindings.Add(binding);

                if (state.Values.ContainsKey((binding.Token, binding.Type))) continue;

                object value;

                if (binding.Type == ConditionVariableType.Bool) value = definition.boolDefault;
                else if (binding.Type == ConditionVariableType.Int) value = definition.intDefault;
                else if (binding.Type == ConditionVariableType.Float) value = definition.floatDefault;
                else if (binding.Type == ConditionVariableType.String) value = definition.stringDefault ?? "";
                else throw new InvalidOperationException("Variable type is not supported.");

                state.Values[(binding.Token, binding.Type)] = value;
            }

            return bindings;
        }

        private static List<Binding> CopyBindings(List<Binding> bindings)
        {
            return bindings.ConvertAll(binding => new Binding { Name = binding.Name, Type = binding.Type, Token = binding.Token });
        }

        private static bool SameBindings(List<Binding> first, List<Binding> second)
        {
            if (first.Count != second.Count) return false;

            for (int i = 0; i < first.Count; i++)
            {
                if (first[i].Name != second[i].Name || first[i].Type != second[i].Type || first[i].Token != second[i].Token) return false;
            }

            return true;
        }

        #endregion DEFINITION HISTORY

        #region FILE OPERATIONS

        internal static G.FileResult LoadCurrent(DialogDatabase database, string filePath = null)
        {
            EnsureRuntime(database);
            _activeState.LastFileResult = G.TryLoadGame(filePath);
            CaptureCurrentValues(_activeState);

            return _activeState.LastFileResult;
        }

        internal static G.FileResult SaveCurrent(DialogDatabase database, string filePath = null)
        {
            EnsureRuntime(database);
            _activeState.LastFileResult = G.TrySaveGame(filePath);

            return _activeState.LastFileResult;
        }

        internal static void OpenSaveFile()
        {
            string path = G.GetSavePath();

            if (!File.Exists(path)) throw new InvalidOperationException("File not found:\n" + path);

            EditorUtility.OpenWithDefaultApp(path);
        }

        #endregion FILE OPERATIONS
    }
}
#endif
