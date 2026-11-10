#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public static class DialogReferenceMessagesED
    {
        public const string MISSING_VARIABLE_SLOT = "Variable was removed";
        public const string MISSING_CHARACTER_SLOT = "Character was removed.";
        public const string MISSING_SPRITE_SLOT = "Sprite was removed.";
        public const string MISSING_DIALOG_SLOT = "Dialog was removed";

        public static string FormatMissingVariableLabel(string previousName)
        {   
            if (string.IsNullOrEmpty(previousName)) return MISSING_VARIABLE_SLOT;
            else return $"Variable \"{previousName}\" was removed.";
        }

        public static string FormatMissingCharacterLabel(string previousName)
        {       
            if (string.IsNullOrEmpty(previousName)) return MISSING_CHARACTER_SLOT;
            else return $"Character \"{previousName}\" was removed.";    
        }

        public static string FormatMissingSpriteLabel(string characterName, string emotionName)
        {
            if (string.IsNullOrEmpty(characterName) && string.IsNullOrEmpty(emotionName)) return MISSING_SPRITE_SLOT;
            return $"Sprite \"{emotionName}\" of character \"{characterName}\" was removed.";
        }

        public static string FormatMissingDialogLabel(string previousName)
        {
            if (string.IsNullOrEmpty(previousName)) return MISSING_DIALOG_SLOT;
            return $"Dialog \"{previousName}\" was removed.";
        }
    }

    public static class DialogReferenceEditorUtility
    {
        public static void MarkVariableMissing(DialogCondition condition, string previousName)
        {
            if (condition == null) return;

            condition.ReferenceState = DialogReferenceState.Missing;
            condition.MissingReferenceLabel = previousName ?? condition.VariableName ?? "";
            condition.VariableName = "";
        }

        public static void MarkVariableMissing(ChoiceCondition condition, string previousName)
        {
            if (condition == null) return;

            condition.ReferenceState = DialogReferenceState.Missing;
            condition.MissingReferenceLabel = previousName ?? condition.VariableName ?? "";
            condition.VariableName = "";
        }

        public static void MarkVariableMissing(ChoiceConsequence consequence, string previousName)
        {
            if (consequence == null) return;

            consequence.ReferenceState = DialogReferenceState.Missing;
            consequence.MissingReferenceLabel = previousName ?? consequence.VariableName ?? "";
            consequence.VariableName = "";
        }

        public static void MarkCharacterMissing(LineNode line, string previousName)
        {
            if (line == null) return;

            line.CharacterReferenceState = DialogReferenceState.Missing;
            line.MissingCharacterReferenceLabel = previousName ?? line.CharacterName ?? "";
            line.CharacterName = "";
        }

        public static void MarkSpriteActionMissing(CharacterActionData action, string characterName, string emotionName)
        {
            if (action == null) return;

            action.ReferenceState = DialogReferenceState.Missing;
            action.MissingReferenceLabel = DialogReferenceMessagesED.FormatMissingSpriteLabel(characterName, emotionName);
            action.CharacterName = "";
            action.EmotionName = "";
        }

        public static void MarkDialogMissing(RedirectNode redirect, string previousName)
        {
            if (redirect == null) return;

            redirect.DialogReferenceState = DialogReferenceState.Missing;
            redirect.MissingDialogReferenceLabel = previousName ?? redirect.TargetDialogName ?? "";
            redirect.TargetDialogName = "";
        }

        public static void RestoreVariableReference(DialogCondition condition, string variableName)
        {
            if (condition == null) return;

            condition.ReferenceState = DialogReferenceState.Valid;
            condition.MissingReferenceLabel = "";
            condition.VariableName = variableName;
        }

        public static void RestoreVariableReference(ChoiceCondition condition, string variableName)
        {
            if (condition == null) return;

            condition.ReferenceState = DialogReferenceState.Valid;
            condition.MissingReferenceLabel = "";
            condition.VariableName = variableName;
        }

        public static void RestoreVariableReference(ChoiceConsequence consequence, string variableName)
        {
            if (consequence == null) return;

            consequence.ReferenceState = DialogReferenceState.Valid;
            consequence.MissingReferenceLabel = "";
            consequence.VariableName = variableName;
        }

        public static void RestoreCharacterReference(LineNode line, string characterName)
        {
            if (line == null) return;

            line.CharacterReferenceState = DialogReferenceState.Valid;
            line.MissingCharacterReferenceLabel = "";
            line.CharacterName = characterName;
        }

        public static void RestoreSpriteAction(CharacterActionData action, string characterName, string emotionName)
        {
            if (action == null) return;

            action.ReferenceState = DialogReferenceState.Valid;
            action.MissingReferenceLabel = "";
            action.CharacterName = characterName;
            action.EmotionName = emotionName;
        }

        public static void RestoreDialogReference(RedirectNode redirect, string dialogName, string category)
        {
            if (redirect == null) return;

            redirect.DialogReferenceState = DialogReferenceState.Valid;
            redirect.MissingDialogReferenceLabel = "";
            redirect.TargetDialogName = dialogName;
            redirect.TargetCategory = category ?? "";
        }

        public static void ResetVariableReferenceState(DialogCondition condition)
        {
            if (condition == null) return;

            condition.ReferenceState = DialogReferenceState.Valid;
            condition.MissingReferenceLabel = "";
        }

        public static void ResetVariableReferenceState(ChoiceCondition condition)
        {
            if (condition == null) return;

            condition.ReferenceState = DialogReferenceState.Valid;
            condition.MissingReferenceLabel = "";
        }

        public static void ResetVariableReferenceState(ChoiceConsequence consequence)
        {
            if (consequence == null) return;

            consequence.ReferenceState = DialogReferenceState.Valid;
            consequence.MissingReferenceLabel = "";
        }
    }

    public enum DialogReferenceUsageKind
    {
        DialogStartCondition,
        NodeCondition,
        ChoiceCondition,
        ChoiceConsequence,
        LineCharacterName,
        CharacterAction,
        RedirectTarget
    }

    public readonly struct DialogReferenceUsage
    {
        /// <summary> ENDS. Separates the readable location from stable dialog and block identity so dependency diagnostics survive list moves. </summary>
        public DialogReferenceUsage(string dialogName, DialogReferenceUsageKind kind, string nodeId, string detail, string dialogKey = null, string blockAddress = null)
        {
            DialogName = dialogName;
            Kind = kind;
            NodeId = nodeId;
            Detail = detail;
            DialogKey = dialogKey;
            BlockAddress = blockAddress;
        }

        public string DialogName { get; }
        public DialogReferenceUsageKind Kind { get; }
        public string NodeId { get; }
        public string Detail { get; }
        public string DialogKey { get; }
        public string BlockAddress { get; }

        /// <summary> ENDS. Builds a stable comparison key independently of display labels and row numbers. </summary>
        public DialogReferenceUsageKey ToKey()
        {
            return new DialogReferenceUsageKey(DialogName, Kind, NodeId, Detail, DialogKey, BlockAddress);
        }
    }

    public readonly struct DialogReferenceUsageKey : IEquatable<DialogReferenceUsageKey>
    {
        /// <summary> ENDS. Uses stable identities when supplied, legacy callers without metadata retain location-based comparison. </summary>
        public DialogReferenceUsageKey(string dialogName, DialogReferenceUsageKind kind, string nodeId, string detail, string dialogKey = null, string blockAddress = null)
        {
            DialogName = dialogName ?? "";
            Kind = kind;
            NodeId = nodeId ?? "";
            Detail = detail ?? "";
            DialogKey = dialogKey;
            BlockAddress = blockAddress;

            if (string.IsNullOrEmpty(DialogKey)) DialogKey = DialogName;
            if (string.IsNullOrEmpty(BlockAddress)) BlockAddress = Detail;
        }

        public string DialogName { get; }
        public DialogReferenceUsageKind Kind { get; }
        public string NodeId { get; }
        public string Detail { get; }
        public string DialogKey { get; }
        public string BlockAddress { get; }

        /// <summary> ENDS. Compares the reference owner and field address independently of display names and row positions. </summary>
        public bool Equals(DialogReferenceUsageKey other)
        {
            return DialogKey == other.DialogKey && Kind == other.Kind && NodeId == other.NodeId && BlockAddress == other.BlockAddress;
        }

        public override bool Equals(object obj)
        {
            if (obj is DialogReferenceUsageKey other) return Equals(other);
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DialogKey.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ NodeId.GetHashCode();
                hash = (hash * 397) ^ BlockAddress.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class DialogReferenceIndex
    {
        private readonly Dictionary<string, List<DialogReferenceUsage>> _variableUsages = new Dictionary<string, List<DialogReferenceUsage>>();
        private readonly Dictionary<string, List<DialogReferenceUsage>> _characterUsages = new Dictionary<string, List<DialogReferenceUsage>>();
        private readonly Dictionary<string, List<DialogReferenceUsage>> _spriteUsages = new Dictionary<string, List<DialogReferenceUsage>>();
        private readonly Dictionary<string, List<DialogReferenceUsage>> _redirectTargetUsages = new Dictionary<string, List<DialogReferenceUsage>>();

        /// <summary> ENDS. Discovers active dependencies through the common typed schema and retains stable owner addresses independently of display labels. </summary>
        public void Rebuild(DialogDatabase database)
        {
            _variableUsages.Clear();
            _characterUsages.Clear();
            _spriteUsages.Clear();
            _redirectTargetUsages.Clear();
            EndsNodeReferencesED.Visit(database, reference =>
            {
                if (reference.Kind == NodeReferenceKind.Node || reference.Kind == NodeReferenceKind.Category || reference.Kind == NodeReferenceKind.Language || string.IsNullOrEmpty(reference.Value)) return;

                DialogEntry dialog = reference.Dialog;

                if (dialog == null || string.IsNullOrEmpty(dialog.dialogName)) return;

                string nodeId = reference.Node?.nodeId;
                string address = reference.OwnerAddress.ToString();
                string detail = reference.Path;
                string displayId = "?";

                if (!string.IsNullOrEmpty(nodeId)) displayId = EndsNodeRefED.ShortRef(nodeId);

                string nodeLabel = reference.Node?.NodeType + " (" + displayId + ")";
                int rowNumber = reference.OwnerAddress.Index + 1;

                if (reference.Kind == NodeReferenceKind.Variable)
                {
                    if (reference.Model is ChoiceCondition || reference.Model is ChoiceConsequence)
                    {
                        int choiceNumber = reference.OwnerAddress.Parent.Parent.Index + 1;
                        string rowLabel = "condition";

                        if (reference.Model is ChoiceConsequence) rowLabel = "consequence";

                        detail = nodeLabel + ", choice " + choiceNumber + ", " + rowLabel + " " + rowNumber;
                    }
                    else if (reference.Node == null) detail = "Dialog start condition #" + rowNumber;
                    else detail = nodeLabel + " #" + rowNumber;
                }
                else if (reference.Kind == NodeReferenceKind.Speaker) detail = nodeLabel + ": line character";
                else if (reference.Kind == NodeReferenceKind.Actor) detail = nodeLabel + ": action #" + rowNumber;
                else if (reference.Kind == NodeReferenceKind.Sprite) detail = nodeLabel + ": sprite #" + rowNumber;
                else if (reference.Kind == NodeReferenceKind.Dialog) detail = nodeLabel + ": redirect";

                DialogReferenceUsageKind kind = DialogReferenceUsageKind.NodeCondition;

                if (reference.Model is ChoiceCondition) kind = DialogReferenceUsageKind.ChoiceCondition;
                else if (reference.Model is ChoiceConsequence) kind = DialogReferenceUsageKind.ChoiceConsequence;
                else if (reference.Node == null) kind = DialogReferenceUsageKind.DialogStartCondition;

                if (reference.Kind == NodeReferenceKind.Variable) AddVariableUsage(reference.Value, dialog.dialogName, kind, nodeId, detail, dialog.NodeIdentityKey, address);
                else if (reference.Kind == NodeReferenceKind.Speaker) AddCharacterUsage(reference.Value, dialog.dialogName, DialogReferenceUsageKind.LineCharacterName, nodeId, detail, dialog.NodeIdentityKey, address);
                else if (reference.Kind == NodeReferenceKind.Actor) AddCharacterUsage(reference.Value, dialog.dialogName, DialogReferenceUsageKind.CharacterAction, nodeId, detail, dialog.NodeIdentityKey, address);
                else if (reference.Kind == NodeReferenceKind.Sprite && !string.IsNullOrEmpty(reference.Qualifier)) AddSpriteUsage(reference.Qualifier, reference.Value, dialog.dialogName, nodeId, detail, dialog.NodeIdentityKey, address);
                else if (reference.Kind == NodeReferenceKind.Dialog) AddRedirectTargetUsage(reference.Value, dialog.dialogName, nodeId, detail, dialog.NodeIdentityKey, address);
            });
        }

        public IReadOnlyList<DialogReferenceUsage> GetVariableUsages(string variableName)
        {
            if (string.IsNullOrEmpty(variableName)) return Array.Empty<DialogReferenceUsage>();
            if (!_variableUsages.TryGetValue(variableName, out List<DialogReferenceUsage> usages)) return Array.Empty<DialogReferenceUsage>();

            return usages;
        }

        public IReadOnlyList<DialogReferenceUsage> GetCharacterUsages(string characterName)
        {
            if (string.IsNullOrEmpty(characterName)) return Array.Empty<DialogReferenceUsage>();
            if (!_characterUsages.TryGetValue(characterName, out List<DialogReferenceUsage> usages)) return Array.Empty<DialogReferenceUsage>();

            return usages;
        }

        public IReadOnlyList<DialogReferenceUsage> GetLineCharacterNameUsages(string characterName)
        {
            return FilterCharacterUsages(characterName, DialogReferenceUsageKind.LineCharacterName);
        }

        public IReadOnlyList<DialogReferenceUsage> GetCharacterActionUsages(string characterName)
        {
            return FilterCharacterUsages(characterName, DialogReferenceUsageKind.CharacterAction);
        }

        private IReadOnlyList<DialogReferenceUsage> FilterCharacterUsages(string characterName, DialogReferenceUsageKind kind)
        {
            IReadOnlyList<DialogReferenceUsage> usages = GetCharacterUsages(characterName);
            if (usages.Count == 0) return usages;

            List<DialogReferenceUsage> filtered = new List<DialogReferenceUsage>(usages.Count);
            for (int i = 0; i < usages.Count; i++)
            {
                DialogReferenceUsage usage = usages[i];
                if (usage.Kind != kind) continue;
                filtered.Add(usage);
            }

            return filtered;
        }

        public IReadOnlyList<DialogReferenceUsage> GetSpriteUsages(string characterName, string emotionName)
        {
            if (string.IsNullOrEmpty(characterName) || string.IsNullOrEmpty(emotionName)) return Array.Empty<DialogReferenceUsage>();
            if (!_spriteUsages.TryGetValue(SpriteKey(characterName, emotionName), out List<DialogReferenceUsage> usages)) return Array.Empty<DialogReferenceUsage>();

            return usages;
        }

        public IReadOnlyList<DialogReferenceUsage> GetRedirectTargetUsages(string dialogName)
        {
            if (string.IsNullOrEmpty(dialogName)) return Array.Empty<DialogReferenceUsage>();
            if (!_redirectTargetUsages.TryGetValue(dialogName, out List<DialogReferenceUsage> usages)) return Array.Empty<DialogReferenceUsage>();

            return usages;
        }

        public static string BuildUsageSummary(IReadOnlyList<DialogReferenceUsage> liveUsages, IReadOnlyList<DialogReferenceUsage> savedUsages, int maxLines = 8)
        {
            if (liveUsages == null || liveUsages.Count == 0) return "Not used in any dialogs.";

            HashSet<DialogReferenceUsageKey> savedKeys = new HashSet<DialogReferenceUsageKey>();

            if (savedUsages != null)
            {
                foreach (DialogReferenceUsage usage in savedUsages)
                {
                    savedKeys.Add(usage.ToKey()); 
                }

            }

            int savedUsageCount = 0;
            int unsavedUsageCount = 0;
            HashSet<string> savedDialogNames = new HashSet<string>();
            HashSet<string> unsavedDialogNames = new HashSet<string>();

            foreach (DialogReferenceUsage usage in liveUsages)
            {
                if (savedKeys.Contains(usage.ToKey()))
                {
                    savedUsageCount++;
                    savedDialogNames.Add(usage.DialogName);
                }
                else
                {
                    unsavedUsageCount++;
                    unsavedDialogNames.Add(usage.DialogName);
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Dependencies found!");

            if (savedUsageCount > 0) sb.AppendLine($"({savedUsageCount}) usages in ({savedDialogNames.Count}) saved dialogs.");
            if (unsavedUsageCount > 0) sb.AppendLine($"({unsavedUsageCount}) usages in ({unsavedDialogNames.Count}) unsaved dialogs.");

            int lines = Math.Min(liveUsages.Count, maxLines);

            for (int i = 0; i < lines; i++)
            {
                sb.AppendLine($"- {liveUsages[i].DialogName}: {liveUsages[i].Detail}");
            }

            if (liveUsages.Count > maxLines) sb.AppendLine($"... and {liveUsages.Count - maxLines} more.");

            return sb.ToString().TrimEnd();
        }

        private void AddVariableUsage(string variableName, string dialogName, DialogReferenceUsageKind kind, string nodeId, string detail, string dialogKey, string blockAddress)
        {
            if (!_variableUsages.TryGetValue(variableName, out List<DialogReferenceUsage> list))
            {
                list = new List<DialogReferenceUsage>();
                _variableUsages[variableName] = list;
            }

            list.Add(new DialogReferenceUsage(dialogName, kind, nodeId, detail, dialogKey, blockAddress));
        }

        private void AddCharacterUsage(string characterName, string dialogName, DialogReferenceUsageKind kind, string nodeId, string detail, string dialogKey, string blockAddress)
        {
            if (!_characterUsages.TryGetValue(characterName, out List<DialogReferenceUsage> list))
            {
                list = new List<DialogReferenceUsage>();
                _characterUsages[characterName] = list;
            }

            list.Add(new DialogReferenceUsage(dialogName, kind, nodeId, detail, dialogKey, blockAddress));
        }

        private void AddSpriteUsage(string characterName, string emotionName, string dialogName, string nodeId, string detail, string dialogKey, string blockAddress)
        {
            string key = SpriteKey(characterName, emotionName);

            if (!_spriteUsages.TryGetValue(key, out List<DialogReferenceUsage> list))
            {
                list = new List<DialogReferenceUsage>();
                _spriteUsages[key] = list;
            }

            list.Add(new DialogReferenceUsage(dialogName, DialogReferenceUsageKind.CharacterAction, nodeId, detail, dialogKey, blockAddress));
        }

        private void AddRedirectTargetUsage(string targetDialogName, string dialogName, string nodeId, string detail, string dialogKey, string blockAddress)
        {
            if (!_redirectTargetUsages.TryGetValue(targetDialogName, out List<DialogReferenceUsage> list))
            {
                list = new List<DialogReferenceUsage>();
                _redirectTargetUsages[targetDialogName] = list;
            }

            list.Add(new DialogReferenceUsage(dialogName, DialogReferenceUsageKind.RedirectTarget, nodeId, detail, dialogKey, blockAddress));
        }

        private static string SpriteKey(string characterName, string emotionName)
        {
            return characterName + "\u001f" + emotionName;
        }

    }

    public static class DialogReferenceCleanup
    {
        /// <summary> ENDS. Marks every described variable reference missing, retaining its previous name for repair. </summary>
        public static int MarkVariableMissingInDialogs(DialogDatabase database, string variableName) => Mark(database, NodeReferenceKind.Variable, variableName);

        /// <summary> ENDS. Marks described speaker references missing without changing sprite actor references. </summary>
        public static int MarkLineCharacterNameMissingInDialogs(DialogDatabase database, string characterName) => Mark(database, NodeReferenceKind.Speaker, characterName);

        /// <summary> ENDS. Marks described Show/Hide actor references missing and clears the dependent sprite selection. </summary>
        public static int MarkCharacterActionsMissingInDialogs(DialogDatabase database, string characterName)
        {
            return Apply(database, NodeReferenceKind.Actor, reference => reference.State != DialogReferenceState.Missing && reference.Value == characterName,
                reference => reference.MarkMissing(DialogReferenceMessagesED.FormatMissingSpriteLabel(reference.Value, reference.Qualifier), true), characterName);
        }

        /// <summary> ENDS. Marks stored sprite selections missing for the given actor, including inactive Hide storage. </summary>
        public static int MarkSpriteMissingInDialogs(DialogDatabase database, string characterName, string emotionName)
        {
            if (string.IsNullOrEmpty(characterName)) return 0;

            return Apply(database, NodeReferenceKind.Sprite, reference => reference.State != DialogReferenceState.Missing && reference.Qualifier == characterName && reference.Value == emotionName,
                reference => reference.MarkMissing(DialogReferenceMessagesED.FormatMissingSpriteLabel(characterName, emotionName), true), emotionName);
        }

        /// <summary> ENDS. Marks redirect dialog references missing through their shared schema descriptors. </summary>
        public static int MarkDialogMissingInDialogs(DialogDatabase database, string dialogName) => Mark(database, NodeReferenceKind.Dialog, dialogName);

        private static int Mark(DialogDatabase database, NodeReferenceKind kind, string target)
        {
            return Apply(database, kind, reference => reference.State != DialogReferenceState.Missing && reference.Value == target, reference => reference.MarkMissing(target), target);
        }

        /// <summary> ENDS. Applies one domain policy to references discovered by the common tree, permits inactive reference values inside the active payload. </summary>
        internal static int Apply(DialogDatabase database, NodeReferenceKind kind, Func<EndsNodeReferencesED.Reference, bool> predicate, Action<EndsNodeReferencesED.Reference> action, string requiredTarget)
        {
            if (string.IsNullOrEmpty(requiredTarget)) return 0;

            int count = 0;
            EndsNodeReferencesED.Visit(database, reference =>
            {
                if (reference.Kind != kind || !predicate(reference)) return;

                action(reference);
                count++;
            }, includeInactiveReferences: true);

            return count;
        }
    }

    public static class DialogReferenceRename
    {
        /// <summary> ENDS. Renames or restores variable references by their declared role without knowing their nested collection locations. </summary>
        public static int RenameVariableInDialogs(DialogDatabase database, string oldName, string newName) => Rename(database, NodeReferenceKind.Variable, oldName, newName, true);

        /// <summary> ENDS. Renames valid sprite actor references, line speaker names remain a separate reference role. </summary>
        public static int RenameCharacterActorInDialogs(DialogDatabase database, string oldName, string newName) => Rename(database, NodeReferenceKind.Actor, oldName, newName, false);

        /// <summary> ENDS. Renames or restores a sprite slot for its actor, preserving inactive Hide selections for later Show operations. </summary>
        public static int RenameSpriteInDialogs(DialogDatabase database, string characterName, string oldEmotionName, string newEmotionName)
        {
            if (string.IsNullOrEmpty(characterName) || string.IsNullOrEmpty(newEmotionName) || oldEmotionName == newEmotionName) return 0;

            string missing = DialogReferenceMessagesED.FormatMissingSpriteLabel(characterName, oldEmotionName);
            return DialogReferenceCleanup.Apply(database, NodeReferenceKind.Sprite,
                reference => (reference.State != DialogReferenceState.Missing && reference.Qualifier == characterName && reference.Value == oldEmotionName) || (reference.State == DialogReferenceState.Missing && reference.MissingLabel == missing),
                reference => reference.Restore(newEmotionName, characterName), oldEmotionName);
        }

        /// <summary> ENDS. Renames or restores redirect targets while retaining their category qualifiers. </summary>
        public static int RenameDialogInRedirectNodes(DialogDatabase database, string oldName, string newName) => Rename(database, NodeReferenceKind.Dialog, oldName, newName, true);

        private static int Rename(DialogDatabase database, NodeReferenceKind kind, string oldName, string newName, bool restoreMissing)
        {
            if (string.IsNullOrEmpty(newName) || oldName == newName) return 0;

            return DialogReferenceCleanup.Apply(database, kind,
                reference => (reference.State != DialogReferenceState.Missing && reference.Value == oldName) || (restoreMissing && reference.State == DialogReferenceState.Missing && reference.MissingLabel == oldName),
                reference => reference.Restore(newName), oldName);
        }
    }
}
#endif
