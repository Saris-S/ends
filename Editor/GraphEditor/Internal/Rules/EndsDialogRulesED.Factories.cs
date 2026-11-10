#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    internal static partial class EndsDialogRulesED
    {
        #region VARIABLE FIELD ACCESS

        private static readonly VariableFields<DialogCondition> _dialogConditionFields = new VariableFields<DialogCondition>(EndsNodeStructureED.DialogConditionSchema.VariableType, EndsNodeStructureED.DialogConditionSchema.VariableName,
            EndsNodeStructureED.DialogConditionSchema.ReferenceState, EndsNodeStructureED.DialogConditionSchema.MissingReferenceLabel, EndsNodeStructureED.DialogConditionSchema.Operator);
        private static readonly VariableFields<ChoiceCondition> _choiceConditionFields = new VariableFields<ChoiceCondition>(EndsNodeStructureED.ChoiceConditionSchema.VariableType, EndsNodeStructureED.ChoiceConditionSchema.VariableName,
            EndsNodeStructureED.ChoiceConditionSchema.ReferenceState, EndsNodeStructureED.ChoiceConditionSchema.MissingReferenceLabel, EndsNodeStructureED.ChoiceConditionSchema.Operator);
        private static readonly VariableFields<ChoiceConsequence> _choiceConsequenceFields = new VariableFields<ChoiceConsequence>(EndsNodeStructureED.ChoiceConsequenceSchema.VariableType, EndsNodeStructureED.ChoiceConsequenceSchema.VariableName,
            EndsNodeStructureED.ChoiceConsequenceSchema.ReferenceState, EndsNodeStructureED.ChoiceConsequenceSchema.MissingReferenceLabel);

        private readonly struct VariableFields<TModel> where TModel : class
        {
            /// <summary> ENDS. Uses the schema's exact type field for variable operations and reference validation. </summary>
            internal readonly EndsNodeStructureED.NodeFieldDefinition<TModel, ConditionVariableType> Type;
            /// <summary> ENDS. Uses the schema's exact name field without reflective member lookup. </summary>
            internal readonly EndsNodeStructureED.NodeFieldDefinition<TModel, string> Name;
            /// <summary> ENDS. Reads reference diagnostics from their existing typed description. </summary>
            internal readonly EndsNodeStructureED.NodeFieldDefinition<TModel, DialogReferenceState> State;
            /// <summary> ENDS. Reads the preserved missing name for reference error reporting. </summary>
            internal readonly EndsNodeStructureED.NodeFieldDefinition<TModel, string> MissingLabel;
            /// <summary> ENDS. Supplies a comparison operator for conditions, consequences have no operator and use null. </summary>
            internal readonly EndsNodeStructureED.NodeFieldDefinition<TModel, ConditionOperator> Operator;

            /// <summary> ENDS. Adapts three existing variable model shapes to shared Rules without defining fields, dependencies or storage again. </summary>
            internal VariableFields(EndsNodeStructureED.NodeFieldDefinition<TModel, ConditionVariableType> type, EndsNodeStructureED.NodeFieldDefinition<TModel, string> name,
                EndsNodeStructureED.NodeFieldDefinition<TModel, DialogReferenceState> state, EndsNodeStructureED.NodeFieldDefinition<TModel, string> missingLabel, EndsNodeStructureED.NodeFieldDefinition<TModel, ConditionOperator> conditionOperator = null)
            {
                Type = type;
                Name = name;
                State = state;
                MissingLabel = missingLabel;
                Operator = conditionOperator;
            }
        }

        #endregion VARIABLE FIELD ACCESS

        #region VARIABLE FACTORIES

        internal static bool TryGetFirstAvailableVariableType(DialogDatabase database, out ConditionVariableType type)
        {
            foreach (ConditionVariableType candidate in Enum.GetValues(typeof(ConditionVariableType)))
            {
                if (database != null && database.GetVariableNamesByType(candidate).Length > 0)
                {
                    type = candidate;

                    return true;
                }
            }

            type = ConditionVariableType.Bool;

            return false;
        }

        internal static DialogCondition CreateDialogCondition(DialogDatabase database)
        {
            TryGetFirstAvailableVariableType(database, out ConditionVariableType type);

            return new DialogCondition { VariableType = type };
        }

        internal static ChoiceCondition CreateChoiceCondition(DialogDatabase database)
        {
            TryGetFirstAvailableVariableType(database, out ConditionVariableType type);

            return new ChoiceCondition { VariableType = type, VariableName = null };
        }

        internal static ChoiceConsequence CreateChoiceConsequence(DialogDatabase database)
        {
            TryGetFirstAvailableVariableType(database, out ConditionVariableType type);

            return new ChoiceConsequence { VariableType = type, VariableName = null };
        }

        internal static void ChangeVariableType(object model, ConditionVariableType type)
        {
            if (model is DialogCondition condition) ChangeVariableType(condition, type, _dialogConditionFields);
            else if (model is ChoiceCondition choiceCondition) ChangeVariableType(choiceCondition, type, _choiceConditionFields);
            else if (model is ChoiceConsequence consequence) ChangeVariableType(consequence, type, _choiceConsequenceFields);
            else throw new ArgumentException("A condition or consequence is required.", nameof(model));
        }

        private static void ChangeVariableType<TModel>(TModel model, ConditionVariableType type, VariableFields<TModel> fields) where TModel : class
        {
            fields.Type.Write(model, type);
            fields.Name.Write(model, null);

            if (model is ChoiceConsequence consequence && !consequence.IsNumeric) EndsNodeStructureED.ChoiceConsequenceSchema.Operation.Write(consequence, ConsequenceOperation.Assign);

            if (fields.Operator != null)
            {
                ConditionOperator[] supported = GetSupportedConditionOperators(type);

                if (Array.IndexOf(supported, fields.Operator.Read(model)) < 0) fields.Operator.Write(model, supported[0]);
            }

            ResetReferenceState(model);
        }

        internal static void ChangeVariableName(object model, string name)
        {
            if (model is DialogCondition condition) _dialogConditionFields.Name.Write(condition, name);
            else if (model is ChoiceCondition choiceCondition) _choiceConditionFields.Name.Write(choiceCondition, name);
            else if (model is ChoiceConsequence consequence) _choiceConsequenceFields.Name.Write(consequence, name);
            else throw new ArgumentException("A condition or consequence is required.", nameof(model));

            ResetReferenceState(model);
        }

        #endregion VARIABLE FACTORIES

        #region CONTENT FACTORIES

        internal static CharacterActionData CreateAction()
        {
            return new CharacterActionData { Zoom = 1f };
        }

        internal static StartNode CreateStart()
        {
            return new StartNode();
        }

        internal static ConditionNode CreateConditionNode()
        {
            return new ConditionNode();
        }

        internal static ChoiceNode CreateChoice()
        {
            return new ChoiceNode();
        }

        internal static LineNode CreateLine()
        {
            return new LineNode { CharacterActions = new List<CharacterActionData>(), DialogsText = new List<LineText>() };
        }

        internal static RedirectNode CreateRedirect(string category)
        {
            if (string.IsNullOrEmpty(category)) category = EndsDatabaseMaintenanceED.DefaultCategoryName;

            return new RedirectNode { TargetCategory = category };
        }

        internal static string GetNodeDataField(NodeType type)
        {
            if (type == NodeType.Start) return EndsNodeStructureED.NodeSchema.Start.Name;
            if (type == NodeType.Line) return EndsNodeStructureED.NodeSchema.Line.Name;
            if (type == NodeType.Choice) return EndsNodeStructureED.NodeSchema.Options.Name;
            if (type == NodeType.Condition) return EndsNodeStructureED.NodeSchema.Condition.Name;
            if (type == NodeType.Redirect) return EndsNodeStructureED.NodeSchema.Redirect.Name;

            throw new InvalidOperationException("Unknown node type.");
        }

        internal static void RequireNodeFields(NodeType type, IEnumerable<string> fields)
        {
            string expected = GetNodeDataField(type);

            foreach (string field in fields)
            {
                if (field.EndsWith("Data", StringComparison.Ordinal) && field != expected) throw new InvalidOperationException(field + " does not belong to this node type.");
            }
        }

        internal static DialogNode CreateNode(NodeType type, string id, Vector2 position, string category)
        {
            DialogNode node = new DialogNode { nodeId = id, NodeType = type, Position = position };

            if (type == NodeType.Start) node.StartData = CreateStart();
            if (type == NodeType.Line) node.LineData = CreateLine();
            if (type == NodeType.Choice) node.ChoiceData = new List<ChoiceNode>();
            if (type == NodeType.Condition) node.ConditionData = CreateConditionNode();
            if (type == NodeType.Redirect) node.RedirectData = CreateRedirect(category);

            return node;
        }

        internal static string GetDefaultEmotion(DialogDatabase database, string characterName)
        {
            if (database?.CharacterGroups == null) return "";

            foreach (CharacterGroup character in database.CharacterGroups)
            {
                if (character == null || character.characterName != characterName || character.sprites == null) continue;

                foreach (CharacterSprite sprite in character.sprites)
                {
                    if (sprite != null) return sprite.spriteName ?? "";
                }
            }

            return "";
        }

        internal static void ChangeActionCharacter(DialogDatabase database, CharacterActionData action, string characterName)
        {
            action.CharacterName = characterName ?? "";
            action.EmotionName = GetDefaultEmotion(database, characterName);
            ResetReferenceState(action);
        }

        internal static object CreateModel(Type type, DialogDatabase database)
        {
            if (type == typeof(DialogCondition)) return CreateDialogCondition(database);
            if (type == typeof(ChoiceCondition)) return CreateChoiceCondition(database);
            if (type == typeof(ChoiceConsequence)) return CreateChoiceConsequence(database);
            if (type == typeof(CharacterActionData)) return CreateAction();
            if (type == typeof(LineNode)) return CreateLine();
            if (type == typeof(StartNode)) return CreateStart();
            if (type == typeof(ConditionNode)) return CreateConditionNode();
            if (type == typeof(ChoiceNode)) return CreateChoice();
            if (type == typeof(RedirectNode)) return CreateRedirect(null);

            return Activator.CreateInstance(type);
        }

        internal static void ChangeLineSpeaker(DialogDatabase database, LineNode line, string selectedName)
        {
            if (database == null) throw new InvalidOperationException("Dialog database is not assigned.");
            if (line == null) throw new InvalidOperationException("Line is missing.");

            line.CharacterName = selectedName ?? "";
            ResetReferenceState(line);

            if (line.DialogsText == null) line.DialogsText = new List<LineText>();

            if (string.IsNullOrEmpty(selectedName))
            {
                for (int i = 0; i < line.DialogsText.Count; i++)
                {
                    if (line.DialogsText[i] != null) line.DialogsText[i].SayerName = "";
                }

                return;
            }

            CharacterNameColor colorEntry = null;

            if (database.CharacterNameColors != null)
            {
                for (int i = 0; i < database.CharacterNameColors.Count; i++)
                {
                    CharacterNameColor color = database.CharacterNameColors[i];

                    if (color != null && color.Name == selectedName) colorEntry = color;
                }
            }

            if (colorEntry == null) return;
            if (database.AvailableLanguages == null) return;

            for (int i = 0; i < database.AvailableLanguages.Count; i++)
            {
                string language = database.AvailableLanguages[i];

                if (string.IsNullOrEmpty(language)) continue;

                LineText entry = null;

                for (int t = 0; t < line.DialogsText.Count; t++)
                {
                    if (line.DialogsText[t] != null && line.DialogsText[t].LanguageCode == language) entry = line.DialogsText[t];
                }

                if (entry == null)
                {
                    entry = new LineText();
                    entry.LanguageCode = language;
                    entry.SayerName = "";
                    entry.Text = "";
                    line.DialogsText.Add(entry);
                }

                entry.SayerName = colorEntry.GetDisplayNameForLanguage(language);
            }
        }

        internal static void PrepareModelChange(object before, object after, ISet<string> suppliedFields, DialogDatabase database)
        {
            if (after is CharacterNameColor nameColor)
            {
                RequireNameColor(nameColor.Color);

                if (suppliedFields.Contains(nameof(CharacterNameColor.Color))) EndsLegacyNameColorMigrationED.ClearLegacyColor(nameColor);
            }

            ModelField[] fields = Models[after.GetType()];
            ModelField typeField = null;

            foreach (ModelField field in fields)
            {
                if (field.Name == nameof(DialogCondition.VariableType)) typeField = field;
            }

            if (typeField != null && before != null && !Equals(typeField.Read(before), typeField.Read(after)))
            {
                ConditionVariableType type = (ConditionVariableType)typeField.Read(after);

                foreach (ModelField field in fields)
                {
                    if (field.Name == nameof(DialogCondition.VariableName) && !suppliedFields.Contains(field.Name)) field.Write(after, null);

                    if (field.Name == nameof(DialogCondition.ConditionOperator) && !suppliedFields.Contains(field.Name))
                    {
                        ConditionOperator value = (ConditionOperator)field.Read(after);

                        ConditionOperator[] supported = GetSupportedConditionOperators(type);

                        if (Array.IndexOf(supported, value) < 0) field.Write(after, supported[0]);
                    }
                }
            }

            if (after is ChoiceConsequence consequence && !consequence.IsNumeric && !suppliedFields.Contains(nameof(ChoiceConsequence.Operation))
                && (before == null || before is ChoiceConsequence originalConsequence && originalConsequence.VariableType != consequence.VariableType)) consequence.Operation = ConsequenceOperation.Assign;

            if (after is CharacterActionData action && (before == null || before is CharacterActionData previous && previous.CharacterName != action.CharacterName)
                && !suppliedFields.Contains(nameof(CharacterActionData.EmotionName))) action.EmotionName = GetDefaultEmotion(database, action.CharacterName);

            if (after is LineNode line && database != null && (before == null || before is LineNode previousLine && previousLine.CharacterName != line.CharacterName)
                && !suppliedFields.Contains(nameof(LineNode.DialogsText))) ChangeLineSpeaker(database, line, line.CharacterName);

            string identity = GetReferenceIdentity(after);

            if (identity == null) return;

            if (before == null || identity != GetReferenceIdentity(before)) ResetReferenceState(after);
            else
            {
                foreach (ModelField field in fields)
                {
                    if (field.Diagnostic) field.Write(after, field.Read(before));
                }
            }
        }

        #endregion CONTENT FACTORIES
    }
}
#endif