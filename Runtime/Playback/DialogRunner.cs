using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>Dialog graph session core. No UI, input, audio, or save-file policy. Peripherals subscribe to events and call commands; this class never references them. Runner reads language from it when present; without it, a default language code is used.</summary>
    public sealed class DialogRunner : MonoBehaviour
    {
        #region Developer API

            // Public API for your game and for replaceable modules (View / Input / Audio / Save). Editing the body of this class is not recommended.

            #region General - status, start/stop, shared events, gameplay freeze 

                // Used by: gameplay scripts, DialogActivator, and any module that shares these events.

                /// <summary> Returns true if a dialog is currently running (including while the UI is opening or closing).</summary>
                public bool IsTalking => _isTalking;

                /// <summary> Returns the DialogDatabase asset assigned in the Inspector.</summary>
                public DialogDatabase DialogDatabase => _dialogDatabase;

                /// <summary> Starts the given dialog. On success fires DialogStarted.</summary>
                public void StartDialog(DialogEntry dialog) => StartDialogImpl(dialog);

                // /// <summary> Starts a dialog by name from the assigned DialogDatabase. Returns false if not found (raises DiagnosticReported StartFailed).</summary>
                // public bool TryStartDialogByName(string dialogName) => TryStartDialogByNameImpl(dialogName);

                /// <summary>Picks and starts a dialog for an initiator (NPC category) using start conditions. Returns false if none match (raises DiagnosticReported StartFailed).</summary>
                public bool TryStartDialogForInitiator(string initiator, bool logSelection = false) => TryStartDialogForInitiatorImpl(initiator, logSelection);

                /// <summary>Aborts the current dialog and fires DialogCancelled. View closes UI then NotifySessionClosed - same as normal end. CancelDialog alone does not close the panel or clear IsTalking.</summary>
                public void CancelDialog() => CancelDialogImpl();

                /// <summary>Event: line ready (text, speaker, VoiceClip, sprites, IsInstant). View shows and calls NotifyLinePresentationReady; Audio plays VoiceClip.</summary>
                public event Action<DialogLinePresentation> LineReady;

                /// <summary>Event: show choice buttons (index + label). View spawns buttons and SelectChoice; Audio stops voice.</summary>
                public event Action<IReadOnlyList<DialogChoicePresentation>> ChoicesReady;

                /// <summary> Fires after choice consequences are applied and before the next node or dialog completion, for View feedback and quest/game code. </summary>
                public event Action<DialogChoicePresentation> ChoiceSelected;

                /// <summary>Event: dialog finished normally (dialog name + last node id). View closes UI and NotifySessionClosed; Save optional auto-save; Audio stops voice.</summary>
                public event Action<DialogSessionInfo> DialogEnded;

                /// <summary>Event: dialog aborted via CancelDialog (dialog name + last node id). View closes UI and NotifySessionClosed; Audio stops voice.</summary>
                public event Action<DialogSessionInfo> DialogCancelled;

                /// <summary>Event: freeze or unfreeze gameplay while dialog UI is up (freeze, showCursor). Your code handles freeze - this package does not set Time.timeScale.</summary>
                public static event Action<bool, bool> GameplayFreezeRequested;

                /// <summary>Fires GameplayFreezeRequested with the same args. Use from samples or custom View to raise freeze manually.</summary>
                public static void RequestGameplayFreeze(bool freeze, bool showCursor) => GameplayFreezeRequested?.Invoke(freeze, showCursor);

                /// <summary>Optional: return true if gameplay already blocked (menu, cutscene). Skips duplicate GameplayFreezeRequested. Query = answer; RequestGameplayFreeze = broadcast.</summary>
                public static Func<bool> QueryGameplayBlocked;

                /// <summary>Event: content/runtime issue (failed TryStart*, missing node, empty choice, etc.). Subscribe from DialogDebugger; passes Kind + Context only.</summary>
                public event Action<DialogDiagnosticReport> DiagnosticReported;

            #endregion General - status, start/stop, shared events, gameplay freeze

            #region View - UI only

                // Used by: DialogWindow (default View) or your own UI only.
                // Shared events (LineReady, ChoicesReady, DialogEnded, ...) live in General - do not remove those when swapping View.

                /// <summary>Event: dialog started (dialog name + start node id). View opens panel then calls NotifySessionOpened or the first line will not appear.</summary>
                public event Action<DialogSessionInfo> DialogStarted;

                /// <summary>Call from View after DialogStarted when open animation finished and the UI can show lines.</summary>
                public void NotifySessionOpened() => NotifySessionOpenedImpl();

                /// <summary>Event: continue pressed while typewriter is running. View skips reveal then calls NotifyLinePresentationReady.</summary>
                public event Action LineSkipRequested;

                /// <summary>Call from View when the line is fully shown (typewriter done/skipped or instant). Next Advance goes to the next node.</summary>
                public void NotifyLinePresentationReady() => NotifyLinePresentationReadyImpl();

                /// <summary>Call from View after DialogEnded or DialogCancelled when close animation finished. Clears IsTalking.</summary>
                public void NotifySessionClosed() => NotifySessionClosedImpl();

            #endregion View - UI only

            #region Input - player continue / choose

                // Used by: DialogLegacyInput (default) or your Input System / UI buttons.

                /// <summary>Player wants to continue. Still revealing fires LineSkipRequested; fully shown advances to next node. No-op on choice screens.</summary>
                public void Advance() => AdvanceImpl();

                /// <summary>Player picked a choice by index from last ChoicesReady. Applies consequences and continues. Call from View buttons or custom input.</summary>
                public void SelectChoice(int index) => SelectChoiceImpl(index);

            #endregion Input - player continue / choose

            #region Audio - voice

                // No methods here. Default DialogAudioPlayer (or your audio) subscribes to General events:
                // LineReady (VoiceClip), ChoicesReady / DialogEnded / DialogCancelled (stop). Replace DialogAudioPlayer for your own mixer / Addressables stack.

            #endregion Audio - voice

            #region Save - variables file

                // No methods here. Load / Save / InitializeVariables live on DialogSave (or your own save component).
                // Optional: DialogSave listens to General DialogEnded for auto-save.Replace DialogSave when progress is stored elsewhere.

            #endregion Save - variables file

            #region Portraits - character sprites

                // No methods here. Optional DialogCharacterPortraitPresenter subscribes to LineReady (CharacterActions)
                // and clears on DialogEnded / DialogCancelled. Omit for RPG text-only setups; typical for visual novels.

            #endregion Portraits - character sprites

        #endregion Developer API



        [Tooltip("Dialog database used for graph walk, sprites lookup data, and initiator selection.")][SerializeField] private DialogDatabase _dialogDatabase;

        private const int MAX_TRANSITIONS_PER_BURST = 4096;

        private DialogEntry _currentDialog;
        private DialogNode _currentNode;
        private readonly List<ChoiceNode> _availableChoices = new List<ChoiceNode>();
        private bool _isTalking;
        private bool _waitingForSessionOpen;
        private bool _waitingForLineAdvance;
        private bool _linePresentationReady;
        private bool _waitingForSessionClose;
        private bool _wasFrozenBeforeDialog;
        private bool _processingTransitions;
        private bool _hasPendingNode;
        private bool _requestingLineSkip;
        private string _pendingNodeId;
        private int _sessionVersion;

        public static DialogRunner Instance { get; private set; }


        private void StartDialogImpl(DialogEntry dialog)
        {
            if (dialog == null || dialog.nodes == null || dialog.nodes.Count == 0) throw new ArgumentException("DialogEntry is null or has no nodes.", nameof(dialog));

            StartDialogInternal(dialog);
        }

        private bool TryStartDialogByNameImpl(string dialogName)
        {
            if (_dialogDatabase == null) throw new InvalidOperationException("TryStartDialogByName: DialogDatabase is not assigned on DialogRunner.");

            DialogEntry dialogEntry = FindDialogEntryByName(dialogName);

            if (dialogEntry == null || dialogEntry.nodes == null || dialogEntry.nodes.Count == 0)
            {
                RaiseDiagnostic(DialogDiagnosticKind.StartFailed, dialogName);

                return false;
            }

            StartDialogInternal(dialogEntry);

            return true;
        }

        private bool TryStartDialogForInitiatorImpl(string initiator, bool logSelection)
        {
            if (string.IsNullOrEmpty(initiator)) throw new ArgumentException("initiator is empty.", nameof(initiator));
            if (_dialogDatabase == null) throw new InvalidOperationException("TryStartDialogForInitiator: DialogDatabase is not assigned on DialogRunner.");

            G.EnsureInitialized(_dialogDatabase);

            if (logSelection)
            {
                Debug.Log($"[Dialog] ========== Dialog activated for initiator '{initiator}' ==========");
                LogAllDialogVariables();
                int dialogCount = 0;

                if (_dialogDatabase.Dialogs != null) dialogCount = _dialogDatabase.Dialogs.Count;

                Debug.Log($"[Dialog] Dialogs in DB: {dialogCount}");
            }

            DialogEntry dialogEntry = ResolveDialogEntryForInitiator(initiator, logSelection);

            if (dialogEntry != null)
            {
                if (logSelection)
                {
                    int nodeCount = 0;

                    if (dialogEntry.nodes != null) nodeCount = dialogEntry.nodes.Count;

                    Debug.Log($"[Dialog] >>> Selected dialog: '{dialogEntry.dialogName}' (nodes: {nodeCount})");
                }

                StartDialogInternal(dialogEntry);

                return true;
            }

            RaiseDiagnostic(DialogDiagnosticKind.StartFailed, initiator);

            return false;
        }

        private void CancelDialogImpl()
        {
            if (!_isTalking) return;

            EndDialogue(DialogCloseReason.Cancelled);
        }

        private void NotifySessionOpenedImpl()
        {
            if (!_isTalking || !_waitingForSessionOpen) return;

            _waitingForSessionOpen = false;
            GoToNode(_currentNode.nodeId);
        }

        private void NotifyLinePresentationReadyImpl()
        {
            if (!_waitingForLineAdvance) return;

            _linePresentationReady = true;
        }

        private void NotifySessionClosedImpl()
        {
            if (!_waitingForSessionClose) return;

            _waitingForSessionClose = false;
            _isTalking = false;
            _currentNode = null;
            _currentDialog = null;
            _availableChoices.Clear();
        }

        private void AdvanceImpl()
        {
            if (!_isTalking) return;
            if (_waitingForSessionOpen || _waitingForSessionClose) return;
            if (_currentNode != null && _currentNode.NodeType == NodeType.Choice) return;
            if (_waitingForLineAdvance && !_linePresentationReady)
            {
                if (_requestingLineSkip) return;

                _requestingLineSkip = true;

                try { LineSkipRequested?.Invoke(); }
                finally { _requestingLineSkip = false; }

                return;
            }

            if (_waitingForLineAdvance && _linePresentationReady)
            {
                AdvanceFromCurrentLine();

                return;
            }
        }

        private void SelectChoiceImpl(int index)
        {
            if (!_isTalking) return;
            if (_waitingForSessionOpen || _waitingForSessionClose) return;
            if (_currentNode == null || _currentNode.NodeType != NodeType.Choice) return;
            if (index < 0 || index >= _availableChoices.Count) return;

            ChoiceNode choice = _availableChoices[index];
            string choiceText = GetChoiceDisplayText(choice);
            var presentation = new DialogChoicePresentation { Index = index, Text = choiceText };
            int sessionVersion = _sessionVersion;
            _availableChoices.Clear();
            ApplyChoiceConsequences(choice);

            if (!IsCurrentSession(sessionVersion)) return;

            ChoiceSelected?.Invoke(presentation);

            if (!IsCurrentSession(sessionVersion)) return;

            GoToNode(choice.NextNodeId);
        }

        private void RaiseDiagnostic(DialogDiagnosticKind kind, string context = null)
        {
            DiagnosticReported?.Invoke(new DialogDiagnosticReport { Kind = kind, Context = context });
        }

        private bool IsCurrentSession(int sessionVersion)
        {
            return _sessionVersion == sessionVersion && _isTalking && !_waitingForSessionOpen && !_waitingForSessionClose;
        }

        private void ReportAndEnd(DialogDiagnosticKind kind, string context, DialogCloseReason reason = DialogCloseReason.Completed)
        {
            int sessionVersion = _sessionVersion;
            RaiseDiagnostic(kind, context);

            if (IsCurrentSession(sessionVersion)) EndDialogue(reason);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("DialogRunner: duplicate instance - destroying this component.", this);
                Destroy(this);

                return;
            }

            Instance = this;

            if (_dialogDatabase != null) G.EnsureInitialized(_dialogDatabase);
            
            if (DialogPreferences.Instance != null && _dialogDatabase != null) DialogPreferences.Instance.RegisterDialogDatabase(_dialogDatabase);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            if (DialogPreferences.Instance != null && _dialogDatabase != null) DialogPreferences.Instance.UnregisterDialogDatabase(_dialogDatabase);

            Instance = null;
        }

        private void Update()
        {
            if (_hasPendingNode) ProcessPendingNodes();
        }

        private void StartDialogInternal(DialogEntry dialog)
        {
            G.EnsureInitialized(_dialogDatabase);
            DialogNode startNode = ResolveStartNode(dialog);

            if (startNode == null) throw new InvalidOperationException($"DialogRunner: start node not found in dialog '{dialog.dialogName}'.");

            int sessionVersion = ++_sessionVersion;
            _hasPendingNode = false;
            _pendingNodeId = null;
            _wasFrozenBeforeDialog = IsGameplayAlreadyBlocked();
            _isTalking = true;
            _waitingForSessionOpen = true;
            _waitingForSessionClose = false;
            _waitingForLineAdvance = false;
            _linePresentationReady = false;
            _availableChoices.Clear();
            _currentDialog = dialog;
            _currentNode = startNode;

            if (!_wasFrozenBeforeDialog)
            {
                RequestGameplayFreeze(true, true);
                EnsureDialogUiCursorVisible();
            }

            if (_sessionVersion != sessionVersion || !_isTalking || _waitingForSessionClose) return;

            DialogStarted?.Invoke(CreateSessionInfo());
        }

        private DialogNode ResolveStartNode(DialogEntry dialog)
        {
            DialogNode startNode = null;

            if (_dialogDatabase != null) startNode = _dialogDatabase.GetStartNode(dialog.dialogName);
            if (startNode == null) startNode = dialog.nodes.Find(n => n.NodeType == NodeType.Start);

            return startNode;
        }

        private void AdvanceFromCurrentLine()
        {
            _waitingForLineAdvance = false;
            _linePresentationReady = false;

            string nextNodeId = null;

            if (_currentNode != null && _currentNode.LineData != null) nextNodeId = _currentNode.LineData.NextNodeId;

            GoToNode(nextNodeId);
        }

        private void GoToNode(string nextNodeId)
        {
            _pendingNodeId = nextNodeId;
            _hasPendingNode = true;
            ProcessPendingNodes();
        }

        // A nested event callback queues its successor instead of entering another graph walk.
        private void ProcessPendingNodes()
        {
            if (_processingTransitions) return;

            _processingTransitions = true;
            int transitions = 0;

            try
            {
                while (_hasPendingNode && _isTalking && !_waitingForSessionOpen && !_waitingForSessionClose)
                {
                    string nextNodeId = _pendingNodeId;
                    _pendingNodeId = null;
                    _hasPendingNode = false;

                    if (string.IsNullOrEmpty(nextNodeId))
                    {
                        EndDialogue(DialogCloseReason.Completed);

                        continue;
                    }

                    if (transitions >= MAX_TRANSITIONS_PER_BURST)
                    {
                        string context = $"Dialog '{_currentDialog.dialogName}', node '{nextNodeId}', limit {MAX_TRANSITIONS_PER_BURST} transitions without yielding for player input.";
                        ReportAndEnd(DialogDiagnosticKind.TransitionLimitExceeded, context, DialogCloseReason.Cancelled);

                        // A diagnostic listener may start another session, resume it on the next frame.
                        break;
                    }

                    transitions++;
                    DisplayNextNode(nextNodeId);
                }
            }
            catch
            {
                _hasPendingNode = false;
                _pendingNodeId = null;

                throw;
            }
            finally { _processingTransitions = false; }
        }

        private void DisplayNextNode(string nextNodeId)
        {
            _availableChoices.Clear();
            _waitingForLineAdvance = false;
            _linePresentationReady = false;

            _currentNode = _currentDialog.nodes.Find(n => n.nodeId == nextNodeId);

            if (_currentNode == null)
            {
                ReportAndEnd(DialogDiagnosticKind.MissingNode, nextNodeId);

                return;
            }

            DisplayNode();
        }

        private void DisplayNode()
        {
            if (_currentNode == null)
            {
                EndDialogue(DialogCloseReason.Completed);

                return;
            }

            switch (_currentNode.NodeType)
            {
                case NodeType.Start:
                    string startNextId = null;

                    if (_currentNode.StartData != null) startNextId = _currentNode.StartData.NextNodeId;

                    GoToNode(startNextId);
                    break;
                case NodeType.Line:
                    PresentLineNode();
                    break;
                case NodeType.Condition:
                    int sessionVersion = _sessionVersion;
                    string conditionNextId = EvaluateConditionNode(_currentNode.ConditionData);

                    if (IsCurrentSession(sessionVersion)) GoToNode(conditionNextId);

                    break;
                case NodeType.Choice:
                    PresentChoiceNode();
                    break;
                case NodeType.Redirect:
                    ExecuteRedirectNode(_currentNode);
                    break;
            }
        }

        private void PresentLineNode()
        {
            LineNode lineData = _currentNode.LineData;

            if (lineData == null)
            {
                ReportAndEnd(DialogDiagnosticKind.MissingLineData, _currentNode.nodeId);

                return;
            }

            DialogLinePresentation presentation = BuildLinePresentation(lineData);

            if (lineData.isInstant)
            {
                int sessionVersion = _sessionVersion;
                LineReady?.Invoke(presentation);

                if (IsCurrentSession(sessionVersion)) GoToNode(lineData.NextNodeId);

                return;
            }

            _waitingForLineAdvance = true;
            _linePresentationReady = false;
            LineReady?.Invoke(presentation);
        }

        private DialogLinePresentation BuildLinePresentation(LineNode lineData)
        {
            string language = GetCurrentLanguageCode();
            string text = lineData.GetTextForLanguage(language);
            string sayer = lineData.GetSayerNameForLanguage(language);
            IReadOnlyList<CharacterActionData> actions = lineData.CharacterActions;

            if (actions == null) actions = Array.Empty<CharacterActionData>();

            string dialogName = "";
            string nodeId = "";

            if (_currentDialog != null) dialogName = _currentDialog.dialogName;
            if (_currentNode != null) nodeId = _currentNode.nodeId;

            return new DialogLinePresentation
            {
                DialogName = dialogName,
                NodeId = nodeId,
                SpeakerKey = lineData.CharacterName,
                SpeakerDisplayName = sayer,
                Text = text,
                SpeakerColor = GetCharacterColor(lineData.CharacterName),
                VoiceClip = lineData.GetVoiceClipForLanguage(language),
                IsInstant = lineData.isInstant,
                CharacterActions = actions
            };
        }

        private void PresentChoiceNode()
        {
            int sessionVersion = _sessionVersion;
            List<ChoiceNode> choices = _currentNode.ChoiceData;
            _availableChoices.Clear();
            List<DialogChoicePresentation> presentations = new List<DialogChoicePresentation>();

            if (choices == null)
            {
                ReportAndEnd(DialogDiagnosticKind.EmptyChoices, null);

                return;
            }

            for (int i = 0; i < choices.Count; i++)
            {
                ChoiceNode choice = choices[i];

                if (choice == null) continue;

                bool available = AreChoiceConditionsMet(choice);

                if (!IsCurrentSession(sessionVersion)) return;
                if (!available) continue;

                int index = _availableChoices.Count;
                _availableChoices.Add(choice);
                presentations.Add(new DialogChoicePresentation
                {
                    Index = index,
                    Text = GetChoiceDisplayText(choice)
                });
            }

            if (presentations.Count == 0)
            {
                ReportAndEnd(DialogDiagnosticKind.EmptyChoices, null);

                return;
            }

            ChoicesReady?.Invoke(presentations);
        }

        private string EvaluateConditionNode(ConditionNode conditionNode)
        {
            if (conditionNode == null) return null;

            bool result = DialogConditionUtility.EvaluateWithAndOrLogic(conditionNode.DialogConditions, conditionNode.IsAndLogic, c => IsConditionEvaluable(c.ReferenceState, c.VariableName), EvaluateDialogCondition);
           
            if (result) return conditionNode.TrueNodeId;

            return conditionNode.FalseNodeId;
        }

        private void ExecuteRedirectNode(DialogNode redirectNode)
        {
            if (redirectNode == null || redirectNode.NodeType != NodeType.Redirect)
            {
                ReportAndEnd(DialogDiagnosticKind.InvalidRedirect, "invalid_node");

                return;
            }

            RedirectNode redirectData = redirectNode.RedirectData;

            if (redirectData == null)
            {
                ReportAndEnd(DialogDiagnosticKind.InvalidRedirect, "empty_data");

                return;
            }

            if (DialogReferenceUtility.IsMissing(redirectData.DialogReferenceState))
            {
                ReportAndEnd(DialogDiagnosticKind.InvalidRedirect, "removed:" + redirectData.MissingDialogReferenceLabel);

                return;
            }

            if (string.IsNullOrEmpty(redirectData.TargetDialogName))
            {
                ReportAndEnd(DialogDiagnosticKind.InvalidRedirect, "empty_target_name");

                return;
            }

            DialogEntry targetDialog = FindDialogEntryByName(redirectData.TargetDialogName);

            if (targetDialog == null) throw new InvalidOperationException($"DialogRunner: redirect target dialog '{redirectData.TargetDialogName}' not found.");
            if (!DialogEntryUtility.IsRedirectTargetCandidate(targetDialog)) throw new InvalidOperationException($"DialogRunner: redirect to '{redirectData.TargetDialogName}' is blocked - not a redirect target.");

            _currentDialog = targetDialog;
            DialogNode startNode = ResolveStartNode(targetDialog);

            if (startNode == null)
            {
                ReportAndEnd(DialogDiagnosticKind.InvalidRedirect, "missing_start:" + redirectData.TargetDialogName);

                return;
            }

            if (startNode.StartData == null)
            {
                ReportAndEnd(DialogDiagnosticKind.InvalidRedirect, "missing_start_data:" + redirectData.TargetDialogName);

                return;
            }

            string firstNodeId = startNode.StartData.NextNodeId;

            if (string.IsNullOrEmpty(firstNodeId))
            {
                ReportAndEnd(DialogDiagnosticKind.InvalidRedirect, "no_start_link:" + redirectData.TargetDialogName);

                return;
            }

            GoToNode(firstNodeId);
        }

        private void EndDialogue(DialogCloseReason reason)
        {
            if (!_isTalking || _waitingForSessionClose) return;

            int sessionVersion = _sessionVersion;
            _hasPendingNode = false;
            _pendingNodeId = null;
            _availableChoices.Clear();
            _waitingForLineAdvance = false;
            _linePresentationReady = false;
            _waitingForSessionOpen = false;
            _waitingForSessionClose = true;

            DialogSessionInfo info = CreateSessionInfo();

            if (!_wasFrozenBeforeDialog) RequestGameplayFreeze(false, true);
            if (_sessionVersion != sessionVersion) return;

            if (reason == DialogCloseReason.Cancelled) DialogCancelled?.Invoke(info);
            else DialogEnded?.Invoke(info);
        }

        private DialogSessionInfo CreateSessionInfo()
        {
            string dialogName = "";
            string nodeId = "";

            if (_currentDialog != null) dialogName = _currentDialog.dialogName;
            if (_currentNode != null) nodeId = _currentNode.nodeId;

            return new DialogSessionInfo
            {
                DialogName = dialogName,
                NodeId = nodeId
            };
        }

        private bool AreChoiceConditionsMet(ChoiceNode choice)
        {
            return DialogConditionUtility.EvaluateWithAndOrLogic(
                choice.ChoiceCondition,
                choice.IsAndLogic,
                c => IsConditionEvaluable(c.ReferenceState, c.VariableName),
                EvaluateChoiceCondition);
        }

        private string GetChoiceDisplayText(ChoiceNode choice)
        {
            if (choice == null || choice.Translations == null) return "";

            ChoiceText localized = choice.Translations.Find(t => t.LanguageCode == GetCurrentLanguageCode());

            if (localized != null && !string.IsNullOrEmpty(localized.Text)) return localized.Text;

            return "";
        }

        private void ApplyChoiceConsequences(ChoiceNode choice)
        {
            if (choice.ChoiceConsequence == null) return;

            int sessionVersion = _sessionVersion;

            for (int i = 0; i < choice.ChoiceConsequence.Count; i++)
            {
                ChoiceConsequence consequence = choice.ChoiceConsequence[i];

                if (consequence == null) continue;

                if (DialogReferenceUtility.IsMissing(consequence.ReferenceState))
                {
                    RaiseDiagnostic(DialogDiagnosticKind.MissingReference, "consequence:" + consequence.MissingReferenceLabel);

                    if (!IsCurrentSession(sessionVersion)) return;

                    continue;
                }

                if (string.IsNullOrEmpty(consequence.VariableName)) continue;
                if (!consequence.IsOperationSupported) throw new InvalidOperationException("Unsupported consequence operation for " + consequence.VariableType + ": " + consequence.Operation + ".");

                if (consequence.VariableType == ConditionVariableType.Bool) G.SetBool(consequence.VariableName, consequence.BoolValue);
                else if (consequence.VariableType == ConditionVariableType.Int) G.SetInt(consequence.VariableName, consequence.Evaluate(G.GetInt(consequence.VariableName)));
                else if (consequence.VariableType == ConditionVariableType.Float) G.SetFloat(consequence.VariableName, consequence.Evaluate(G.GetFloat(consequence.VariableName)));
                else if (consequence.VariableType == ConditionVariableType.String) G.SetString(consequence.VariableName, consequence.StringValue);
            }
        }

        private Color GetCharacterColor(string characterName)
        {
            if (_dialogDatabase == null || _dialogDatabase.CharacterNameColors == null) return Color.white;

            CharacterNameColor colorEntry = _dialogDatabase.CharacterNameColors.Find(c => c != null && c.Name == characterName);

            if (colorEntry == null) return Color.white;

            return colorEntry.Color;
        }

        private string GetCurrentLanguageCode()
        {
            if (DialogPreferences.Instance != null) return DialogPreferences.Instance.CurrentLanguage;
            if (_dialogDatabase != null && !string.IsNullOrEmpty(_dialogDatabase.CurrentLanguage)) return _dialogDatabase.CurrentLanguage;

            return DialogPreferences.DEFAULT_LANGUAGE_CODE;
        }

        private static void EnsureDialogUiCursorVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static bool IsGameplayAlreadyBlocked()
        {
            if (QueryGameplayBlocked == null) return false;

            return QueryGameplayBlocked();
        }

        private static void LogAllDialogVariables()
        {
            Debug.Log("[Dialog] --- G variables ---");

            foreach (string name in G.GetBoolVariableNames())
            {
                Debug.Log($"[Dialog] G bool {name} = {G.GetBool(name)}");
            }

            foreach (string name in G.GetIntVariableNames())
            {
                Debug.Log($"[Dialog] G int {name} = {G.GetInt(name)}");
            }

            foreach (string name in G.GetFloatVariableNames())
            {
                Debug.Log($"[Dialog] G float {name} = {G.GetFloat(name)}");
            }

            foreach (string name in G.GetStringVariableNames())
            {
                Debug.Log($"[Dialog] G string {name} = '{G.GetString(name)}'");
            }

            Debug.Log("[Dialog] --- end G variables ---");
        }

        private DialogEntry FindDialogEntryByName(string dialogName)
        {
            if (_dialogDatabase == null || _dialogDatabase.Dialogs == null || string.IsNullOrEmpty(dialogName)) return null;

            return _dialogDatabase.Dialogs.Find(d => d != null && d.dialogName == dialogName);
        }

        private DialogEntry ResolveDialogEntryForInitiator(string initiator, bool logSelection)
        {
            if (_dialogDatabase == null || _dialogDatabase.Dialogs == null || string.IsNullOrEmpty(initiator)) return null;

            DialogEntry defaultWithoutConditions = null;

            foreach (DialogEntry dialogEntry in _dialogDatabase.Dialogs.Where(d => d != null))
            {
                if (dialogEntry.initiator != initiator) continue;
                if (dialogEntry.nodes == null || dialogEntry.nodes.Count == 0) continue;

                if (DialogEntryUtility.IsExcludedFromInitiatorSelection(dialogEntry))
                {
                    if (logSelection) Debug.Log($"[Dialog] Dialog '{dialogEntry.dialogName}': locked - skipped for initiator.");

                    continue;
                }

                if (dialogEntry.conditions == null || dialogEntry.conditions.Count == 0 || !HasEvaluableDialogConditions(dialogEntry.conditions))
                {
                    if (logSelection) Debug.Log($"[Dialog] Dialog '{dialogEntry.dialogName}': no conditions (default candidate)");
                    if (defaultWithoutConditions == null) defaultWithoutConditions = dialogEntry;

                    continue;
                }

                bool dialogMatches = DialogConditionUtility.EvaluateWithAndOrLogic(
                    dialogEntry.conditions,
                    dialogEntry.isAndLogic,
                    c => IsConditionEvaluable(c.ReferenceState, c.VariableName),
                    EvaluateDialogCondition);

                if (logSelection) LogDialogEntryConditions(dialogEntry);

                if (dialogMatches)
                {
                    if (logSelection)
                    {
                        string matchMode;

                        if (dialogEntry.isAndLogic) matchMode = "ALL";
                        else matchMode = "ANY";

                        Debug.Log($"[Dialog] Dialog '{dialogEntry.dialogName}': {matchMode} conditions matched.");
                    }

                    return dialogEntry;
                }
            }

            if (logSelection)
            {
                if (defaultWithoutConditions != null) Debug.Log($"[Dialog] Using default without conditions: '{defaultWithoutConditions.dialogName}'");
                else Debug.Log($"[Dialog] No dialog found for initiator '{initiator}'.");
            }

            return defaultWithoutConditions;
        }

        private static void LogDialogEntryConditions(DialogEntry dialogEntry)
        {
            if (dialogEntry == null || dialogEntry.conditions == null) return;

            foreach (DialogCondition condition in dialogEntry.conditions)
            {
                if (condition == null) continue;
                if (!IsConditionEvaluable(condition.ReferenceState, condition.VariableName)) continue;

                string valueStr = FormatDialogConditionValue(condition);
                Debug.Log($"[Dialog] Dialog '{dialogEntry.dialogName}' condition: {condition.VariableName} ({condition.VariableType}) {condition.ConditionOperator} {valueStr}");
            }
        }

        private static string FormatDialogConditionValue(DialogCondition condition)
        {
            if (condition.VariableType == ConditionVariableType.Bool) return condition.BoolValue.ToString();
            if (condition.VariableType == ConditionVariableType.Int) return condition.IntValue.ToString();
            if (condition.VariableType == ConditionVariableType.Float) return condition.FloatValue.ToString();
            if (condition.StringValue == null) return "";

            return condition.StringValue;
        }

        private static bool IsConditionEvaluable(DialogReferenceState referenceState, string variableName)
        {
            if (DialogReferenceUtility.IsMissing(referenceState)) return false;

            return !string.IsNullOrEmpty(variableName);
        }

        private static bool HasEvaluableDialogConditions(IEnumerable<DialogCondition> conditions)
        {
            if (conditions == null) return false;

            foreach (DialogCondition condition in conditions)
            {
                if (condition == null) continue;
                if (IsConditionEvaluable(condition.ReferenceState, condition.VariableName)) return true;
            }

            return false;
        }

        private bool EvaluateDialogCondition(DialogCondition condition)
        {
            if (condition == null)
            {
                RaiseDiagnostic(DialogDiagnosticKind.InvalidCondition, "empty");

                return false;
            }

            if (DialogReferenceUtility.IsMissing(condition.ReferenceState))
            {
                RaiseDiagnostic(DialogDiagnosticKind.MissingReference, condition.MissingReferenceLabel);

                return false;
            }

            if (string.IsNullOrEmpty(condition.VariableName))
            {
                RaiseDiagnostic(DialogDiagnosticKind.InvalidCondition, "empty_variable");

                return false;
            }

            return EvaluateVariableCondition(
                condition.VariableName,
                condition.VariableType,
                condition.ConditionOperator,
                condition.BoolValue,
                condition.IntValue,
                condition.FloatValue,
                condition.StringValue);
        }

        private bool EvaluateChoiceCondition(ChoiceCondition condition)
        {
            if (condition == null)
            {
                RaiseDiagnostic(DialogDiagnosticKind.InvalidCondition, "empty_choice");

                return false;
            }

            if (DialogReferenceUtility.IsMissing(condition.ReferenceState))
            {
                RaiseDiagnostic(DialogDiagnosticKind.MissingReference, condition.MissingReferenceLabel);

                return false;
            }

            if (string.IsNullOrEmpty(condition.VariableName))
            {
                RaiseDiagnostic(DialogDiagnosticKind.InvalidCondition, "empty_choice_variable");

                return false;
            }

            return EvaluateVariableCondition(
                condition.VariableName,
                condition.VariableType,
                condition.ConditionOperator,
                condition.BoolValue,
                condition.IntValue,
                condition.FloatValue,
                condition.StringValue);
        }

        private bool EvaluateVariableCondition( string variableName, ConditionVariableType variableType, ConditionOperator conditionOperator, bool boolValue, int intValue, float floatValue, string stringValue)
        {
            if (variableType == ConditionVariableType.Bool) return DialogConditionUtility.CompareConditionValues(G.GetBool(variableName), boolValue, conditionOperator);
            if (variableType == ConditionVariableType.Int) return DialogConditionUtility.CompareConditionValues(G.GetInt(variableName), intValue, conditionOperator);
            if (variableType == ConditionVariableType.Float) return DialogConditionUtility.CompareConditionValues(G.GetFloat(variableName), floatValue, conditionOperator);
            if (variableType == ConditionVariableType.String) return DialogConditionUtility.CompareConditionValues(G.GetString(variableName), stringValue, conditionOperator);

            RaiseDiagnostic(DialogDiagnosticKind.InvalidCondition, "unknown_type:" + variableType);

            return false;
        }
    }
}