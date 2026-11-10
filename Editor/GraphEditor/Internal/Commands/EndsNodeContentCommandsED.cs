#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static class EndsNodeContentCommandsED
    {
        #region LINE

        internal static void SetInstant(LineNode line, bool instant)
        {
            if (line == null) throw new InvalidOperationException("Line is missing.");

            line.isInstant = instant;
        }

        internal static void SetSpeaker(DialogDatabase database, LineNode line, string selectedName)
        {
            EndsDialogRulesED.ChangeLineSpeaker(database, line, selectedName);
        }

        internal static int AddAction(LineNode line, CharacterActionData action)
        {
            if (line == null) throw new InvalidOperationException("Line is missing.");
            if (action == null) throw new InvalidOperationException("Action is missing.");
            if (line.CharacterActions == null) line.CharacterActions = new List<CharacterActionData>();

            line.CharacterActions.Add(action);

            return line.CharacterActions.Count - 1;
        }

        internal static CharacterActionData ActionAt(LineNode line, int index, bool createIfMissing)
        {
            if (line == null || line.CharacterActions == null) throw new InvalidOperationException("index is out of range.");
            if (index < 0 || index >= line.CharacterActions.Count) throw new InvalidOperationException("index is out of range.");

            CharacterActionData action = line.CharacterActions[index];

            if (action != null) return action;
            if (!createIfMissing) throw new InvalidOperationException("index is out of range.");

            action = EndsDialogRulesED.CreateAction();
            line.CharacterActions[index] = action;

            return action;
        }

        internal static void SetActionEmotion(CharacterActionData action, string emotionName)
        {
            if (action == null) throw new InvalidOperationException("Action is missing.");

            action.EmotionName = emotionName ?? "";
        }

        internal static void SetActionKind(CharacterActionData action, CharacterAction kind)
        {
            if (action == null) throw new InvalidOperationException("Action is missing.");

            action.Action = kind;
        }

        internal static void SetActionZoom(CharacterActionData action, float zoom)
        {
            if (action == null) throw new InvalidOperationException("Action is missing.");

            action.Zoom = zoom;
        }

        internal static void SetActionX(CharacterActionData action, float x)
        {
            if (action == null) throw new InvalidOperationException("Action is missing.");

            action.PosX = x;
        }

        internal static void SetActionY(CharacterActionData action, float y)
        {
            if (action == null) throw new InvalidOperationException("Action is missing.");

            action.PosY = y;
        }

        internal static void RemoveAction(LineNode line, int index)
        {
            if (line == null || line.CharacterActions == null) throw new InvalidOperationException("index is out of range.");
            if (index < 0 || index >= line.CharacterActions.Count) throw new InvalidOperationException("index is out of range.");

            line.CharacterActions.RemoveAt(index);
        }

        internal static void CopyAction(CharacterActionData source, CharacterActionData target)
        {
            if (source == null || target == null) throw new InvalidOperationException("Action is missing.");

            string identity = target.BlockId;
            EndsDialogRulesED.CopyInto(source, target);
            target.BlockId = identity;
            EndsDialogRulesED.ResetReferenceState(target);
        }

        #endregion LINE

        #region CHOICE

        internal static int AddChoice(DialogNode node)
        {
            if (node == null) throw new InvalidOperationException("Node is missing.");
            if (node.ChoiceData == null) node.ChoiceData = new List<ChoiceNode>();

            node.ChoiceData.Add(EndsDialogRulesED.CreateChoice());

            return node.ChoiceData.Count - 1;
        }

        internal static void RemoveChoice(DialogNode node, int index)
        {
            if (node == null || node.ChoiceData == null) throw new InvalidOperationException("index is out of range.");
            if (index < 0 || index >= node.ChoiceData.Count) throw new InvalidOperationException("index is out of range.");

            node.ChoiceData.RemoveAt(index);
        }

        internal static void SetChoiceLogic(ChoiceNode choice, bool andLogic)
        {
            if (choice == null) throw new InvalidOperationException("Choice is missing.");

            choice.IsAndLogic = andLogic;
        }

        internal static void AddChoiceCondition(ChoiceNode choice, ChoiceCondition condition)
        {
            if (choice == null) throw new InvalidOperationException("Choice is missing.");
            if (condition == null) throw new InvalidOperationException("Condition is missing.");
            if (choice.ChoiceCondition == null) choice.ChoiceCondition = new List<ChoiceCondition>();

            choice.ChoiceCondition.Add(condition);
        }

        internal static void RemoveChoiceCondition(ChoiceNode choice, int index)
        {
            if (choice == null || choice.ChoiceCondition == null) throw new InvalidOperationException("index is out of range.");
            if (index < 0 || index >= choice.ChoiceCondition.Count) throw new InvalidOperationException("index is out of range.");

            choice.ChoiceCondition.RemoveAt(index);
        }

        internal static void AddChoiceConsequence(ChoiceNode choice, ChoiceConsequence consequence)
        {
            if (choice == null) throw new InvalidOperationException("Choice is missing.");
            if (consequence == null) throw new InvalidOperationException("Consequence is missing.");
            if (choice.ChoiceConsequence == null) choice.ChoiceConsequence = new List<ChoiceConsequence>();

            choice.ChoiceConsequence.Add(consequence);
        }

        internal static void RemoveChoiceConsequence(ChoiceNode choice, int index)
        {
            if (choice == null || choice.ChoiceConsequence == null) throw new InvalidOperationException("index is out of range.");
            if (index < 0 || index >= choice.ChoiceConsequence.Count) throw new InvalidOperationException("index is out of range.");

            choice.ChoiceConsequence.RemoveAt(index);
        }

        #endregion CHOICE
    }
}
#endif
