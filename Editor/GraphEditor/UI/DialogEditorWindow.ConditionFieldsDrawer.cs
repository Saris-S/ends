#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public partial class DialogEditorWindowED
    {
        private static readonly string[] _consequenceOperationLabels = { "=", "+=", "-=" };
        private readonly ConditionFieldContext<DialogCondition> _dialogConditionFieldContext = new ConditionFieldContext<DialogCondition>();
        private readonly ConditionFieldContext<ChoiceCondition> _choiceConditionFieldContext = new ConditionFieldContext<ChoiceCondition>();
        private readonly ConditionFieldContext<ChoiceConsequence> _choiceConsequenceFieldContext = new ConditionFieldContext<ChoiceConsequence>();
        private EndsNodeDrawersED.Registry<DialogCondition, ConditionFieldContext<DialogCondition>> _dialogConditionDrawers;
        private EndsNodeDrawersED.Registry<ChoiceCondition, ConditionFieldContext<ChoiceCondition>> _choiceConditionDrawers;
        private EndsNodeDrawersED.Registry<ChoiceConsequence, ConditionFieldContext<ChoiceConsequence>> _choiceConsequenceDrawers;

        private sealed class ConditionFieldContext<TCondition> where TCondition : class
        {
            public int Index;
            public IReadOnlyList<TCondition> Siblings;
            public float X;
            public float Y;
            public ConditionFieldsDrawContext Fields;
            public EndsNodeDrawersED.ControlAddress<TCondition> Controls;
        }

        private void EnsureConditionFieldDrawers()
        {
            if (_dialogConditionDrawers != null) return;

            _dialogConditionDrawers = new EndsNodeDrawersED.Registry<DialogCondition, ConditionFieldContext<DialogCondition>>();
            _dialogConditionDrawers.Register(DialogConditionSchema, new EndsNodeDrawersED.Drawer<DialogCondition, ConditionFieldContext<DialogCondition>>(condition => CONDITION_NODE_CONDITION_HEIGHT, DrawDialogConditionFields));
            _choiceConditionDrawers = new EndsNodeDrawersED.Registry<ChoiceCondition, ConditionFieldContext<ChoiceCondition>>();
            _choiceConditionDrawers.Register(ChoiceConditionSchema, new EndsNodeDrawersED.Drawer<ChoiceCondition, ConditionFieldContext<ChoiceCondition>>(condition => CHOICE_EVENT_ITEM_HEIGHT, DrawChoiceConditionFields));
            _choiceConsequenceDrawers = new EndsNodeDrawersED.Registry<ChoiceConsequence, ConditionFieldContext<ChoiceConsequence>>();
            _choiceConsequenceDrawers.Register(ChoiceConsequenceSchema, new EndsNodeDrawersED.Drawer<ChoiceConsequence, ConditionFieldContext<ChoiceConsequence>>(consequence => CHOICE_EVENT_ITEM_HEIGHT, DrawChoiceConsequenceFields));
        }

        private void DrawDialogConditionEditableFields(DialogCondition condition, int conditionIndex, IReadOnlyList<DialogCondition> siblingConditions, ref float fieldY, ConditionFieldsDrawContext ctx, EndsNodeDrawersED.ControlAddress<DialogCondition> controls)
        {
            EnsureConditionFieldDrawers();
            var context = _dialogConditionFieldContext;
            context.Index = conditionIndex;
            context.Siblings = siblingConditions;
            context.Y = fieldY;
            context.Fields = ctx;
            context.Controls = controls;
            _dialogConditionDrawers.Get(DialogConditionSchema).Draw(new Rect(0, fieldY, ctx.ContentWidth, CONDITION_NODE_CONDITION_HEIGHT * ctx.ZoomScale), condition, context);
            fieldY = context.Y;
        }

        private void DrawChoiceConditionEditableFields(ChoiceCondition condition, int conditionIndex, IReadOnlyList<ChoiceCondition> siblingConditions, float fieldY, ref float fieldX, ConditionFieldsDrawContext ctx, EndsNodeDrawersED.ControlAddress<ChoiceCondition> controls)
        {
            EnsureConditionFieldDrawers();
            var context = _choiceConditionFieldContext;
            context.Index = conditionIndex;
            context.Siblings = siblingConditions;
            context.X = fieldX;
            context.Y = fieldY;
            context.Fields = ctx;
            context.Controls = controls;
            _choiceConditionDrawers.Get(ChoiceConditionSchema).Draw(new Rect(fieldX, fieldY, ctx.ContentWidth, CHOICE_EVENT_ITEM_HEIGHT * ctx.ZoomScale), condition, context);
            fieldX = context.X;
        }

        private void DrawDialogConditionFields(Rect bounds, DialogCondition condition, ConditionFieldContext<DialogCondition> context)
        {
            var ctx = context.Fields;
            ctx.OperatorKey = context.Controls.Field(DialogConditionSchema.Operator);
            ctx.ValueKey = context.Controls.Field(DialogConditionSchema.Value);
            int conditionIndex = context.Index;
            IReadOnlyList<DialogCondition> siblingConditions = context.Siblings;
            float fieldY = context.Y;

            float zs = ctx.ZoomScale;
            GUIStyle labelStyle = ctx.LabelStyle ?? EditorStyles.label;
            float fieldWidth = ctx.ContentWidth - CONDITION_FIELD_WIDTH_OFFSET * zs;

            string[] variableNamesForType = GetVariableNamesByType(condition.VariableType);
            GUI.Label(new Rect(CONDITION_LABEL_X * zs, fieldY, CONDITION_LABEL_WIDTH * zs, FIELD_ROW_HEIGHT * zs), "Type:", labelStyle);

            DrawVariableTypeDropdown(new Rect(CONDITION_FIELD_OFFSET * zs, fieldY, fieldWidth, FIELD_ROW_HEIGHT * zs), condition.VariableType, newVariableType =>
                {
                    if (ctx.OnChanged == null) RecordEditorIntent("Change Condition Type");

                    EndsDialogRulesED.ChangeVariableType(condition, newVariableType);
                    ctx.OnChanged?.Invoke();
                },
            ctx.PopupStyle, context.Controls.Field(DialogConditionSchema.VariableType));

            fieldY += ACTION_ROW_HEIGHT * zs;

            GUI.Label(new Rect(CONDITION_LABEL_X * zs, fieldY, CONDITION_LABEL_WIDTH * zs, FIELD_ROW_HEIGHT * zs), "Name:", labelStyle);

            EditorGUI.BeginChangeCheck();
            string newVarName = DrawVariableNamePopup(
                new Rect(CONDITION_FIELD_OFFSET * zs, fieldY, fieldWidth, FIELD_ROW_HEIGHT * zs),
                variableNamesForType,
                condition.VariableName,
                ctx.PopupStyle,
            CollectUsedVariableNames(siblingConditions, conditionIndex), context.Controls.Field(DialogConditionSchema.VariableName));

            bool variableChanged = EditorGUI.EndChangeCheck();

            if (variableChanged && newVarName != condition.VariableName)
            {
                if (ctx.OnChanged == null) RecordEditorIntent("Change Condition Variable");

                EndsDialogRulesED.ChangeVariableName(condition, newVarName);
                InvalidateReferenceIndex();
                ctx.OnChanged?.Invoke();
            }

            fieldY += ACTION_ROW_HEIGHT * zs;
            GUI.Label(new Rect(CONDITION_LABEL_X * zs, fieldY, CONDITION_LABEL_WIDTH * zs, FIELD_ROW_HEIGHT * zs), "Operator:", labelStyle);
            Rect operatorFieldRect = new Rect(CONDITION_FIELD_OFFSET * zs, fieldY, fieldWidth, FIELD_ROW_HEIGHT * zs);
            fieldY += ACTION_ROW_HEIGHT * zs;
            GUI.Label(new Rect(CONDITION_LABEL_X * zs, fieldY, CONDITION_LABEL_WIDTH * zs, FIELD_ROW_HEIGHT * zs), "Value:", labelStyle);
            Rect valueFieldRect = new Rect(CONDITION_FIELD_OFFSET * zs, fieldY, fieldWidth, FIELD_ROW_HEIGHT * zs);

            DrawConditionOperatorValueUI(
                condition.VariableType,
                condition.VariableName,
                condition.ConditionOperator,
                condition.BoolValue,
                condition.IntValue,
                condition.FloatValue,
                condition.StringValue,
                operatorFieldRect,
                valueFieldRect,
                ctx,
                op => { condition.ConditionOperator = op; ctx.OnChanged?.Invoke(); },
                val => { condition.BoolValue = val; ctx.OnChanged?.Invoke(); },
                val => { condition.IntValue = val; ctx.OnChanged?.Invoke(); },
                val => { condition.FloatValue = val; ctx.OnChanged?.Invoke(); },
                val => { condition.StringValue = val; ctx.OnChanged?.Invoke(); },
                "Change Condition Operator",
                "Change Condition",
                GetAllowedNumericConditionOperators(siblingConditions, conditionIndex, condition.VariableName));

            fieldY += ACTION_ROW_HEIGHT * zs;
            context.Y = fieldY;
        }

        private void DrawChoiceConditionFields(Rect bounds, ChoiceCondition condition, ConditionFieldContext<ChoiceCondition> context)
        {
            var ctx = context.Fields;
            ctx.OperatorKey = context.Controls.Field(ChoiceConditionSchema.Operator);
            ctx.ValueKey = context.Controls.Field(ChoiceConditionSchema.Value);
            int conditionIndex = context.Index;
            IReadOnlyList<ChoiceCondition> siblingConditions = context.Siblings;
            float fieldX = context.X;
            float fieldY = context.Y;

            float zs = ctx.ZoomScale;
            GUIStyle labelStyle = ctx.LabelStyle ?? EditorStyles.label;


            GUI.Label(new Rect(fieldX, fieldY, CHOICE_EVENT_TYPE_LABEL_WIDTH * zs, FIELD_ROW_HEIGHT * zs), "Type:", labelStyle);
            fieldX += CHOICE_EVENT_TYPE_LABEL_WIDTH * zs;

            DrawVariableTypeDropdown(
                new Rect(fieldX, fieldY, CHOICE_EVENT_TYPE_POPUP_WIDTH * zs, FIELD_ROW_HEIGHT * zs),
                condition.VariableType,
                newType =>
                {
                    RecordEditorIntent("Change Variable Type");
                    EndsDialogRulesED.ChangeVariableType(condition, newType);
                },
                ctx.PopupStyle, context.Controls.Field(ChoiceConditionSchema.VariableType));

            fieldX += (CHOICE_EVENT_TYPE_POPUP_WIDTH + CHOICE_EVENT_VAR_SPACING)  * zs;

            GUI.Label(new Rect(fieldX, fieldY, CHOICE_EVENT_NAME_LABEL_WIDTH * zs, FIELD_ROW_HEIGHT * zs), "Name:", labelStyle);
            fieldX += CHOICE_EVENT_NAME_LABEL_WIDTH * zs;
            string[] varNames = GetVariableNamesByType(condition.VariableType);

            string newCondVarName = DrawVariableNamePopup(
                new Rect(fieldX, fieldY, CHOICE_EVENT_VAR_COND_POPUP_WIDTH * zs, FIELD_ROW_HEIGHT * zs),
                varNames,
                condition.VariableName,
                ctx.PopupStyle,
                CollectUsedVariableNames(siblingConditions, conditionIndex), context.Controls.Field(ChoiceConditionSchema.VariableName));

            fieldX += (CHOICE_EVENT_VAR_COND_POPUP_WIDTH + CHOICE_EVENT_VAR_SPACING) * zs;
            

            if (newCondVarName != condition.VariableName)
            {
                RecordEditorIntent("Change Variable Name");
                EndsDialogRulesED.ChangeVariableName(condition, newCondVarName);
                InvalidateReferenceIndex();
            }


            Rect operatorFieldRect = new Rect(fieldX, fieldY, CHOICE_EVENT_OP_POPUP_WIDTH * zs, FIELD_ROW_HEIGHT * zs);
            fieldX += (CHOICE_EVENT_OP_POPUP_WIDTH + CHOICE_EVENT_VAR_SPACING) * zs;
            Rect valueFieldRect = new Rect(fieldX, fieldY, CHOICE_EVENT_VALUE_FIELD_WIDTH * zs, FIELD_ROW_HEIGHT * zs);

            if (condition.VariableType == ConditionVariableType.Bool) valueFieldRect = new Rect(fieldX, fieldY, FIELD_ROW_HEIGHT * zs, FIELD_ROW_HEIGHT * zs);

            DrawConditionOperatorValueUI(
                condition.VariableType,
                condition.VariableName,
                condition.ConditionOperator,
                condition.BoolValue,
                condition.IntValue,
                condition.FloatValue,
                condition.StringValue,
                operatorFieldRect,
                valueFieldRect,
                ctx,
                op => condition.ConditionOperator = op,
                val => condition.BoolValue = val,
                val => condition.IntValue = val,
                val => condition.FloatValue = val,
                val => condition.StringValue = val,
                "Change Condition Operator",
                "Change",
                GetAllowedNumericConditionOperators(siblingConditions, conditionIndex, condition.VariableName));

            fieldX += CHOICE_EVENT_VALUE_FIELD_WIDTH * zs;
            context.X = fieldX;
        }

        private void DrawChoiceConsequenceFields(Rect bounds, ChoiceConsequence varChange, ConditionFieldContext<ChoiceConsequence> context)
        {
            var ctx = context.Fields;
            float fieldX = context.X;
            float fieldY = context.Y;
            float zoomScale = ctx.ZoomScale;
            var labelStyle = ctx.LabelStyle;
            var labelCenterStyle = GetGraphStyles(zoomScale).CenterLabel;
            var popupStyle = ctx.PopupStyle;
            var textAreaStyle = ctx.TextFieldStyle;
            var buttonStyle = ctx.ButtonStyle;
            bool blockNodeInteraction = ctx.BlockInteraction;
            string consequenceValueKey = context.Controls.Field(ChoiceConsequenceSchema.Value);
            GUI.Label(new Rect(fieldX, fieldY, CHOICE_EVENT_TYPE_LABEL_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale), "Type:", labelStyle);

            fieldX += CHOICE_EVENT_TYPE_LABEL_WIDTH * zoomScale;
            var varChangeRefCons = varChange;
            DrawVariableTypeDropdown(
                new Rect(fieldX, fieldY, CHOICE_EVENT_TYPE_POPUP_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale),
                varChange.VariableType,
                newType =>
                {
                    RecordEditorIntent("Change Variable Type");
                    EndsDialogRulesED.ChangeVariableType(varChangeRefCons, newType);
                },
                popupStyle, context.Controls.Field(ChoiceConsequenceSchema.VariableType));

            fieldX += (CHOICE_EVENT_TYPE_POPUP_WIDTH + CHOICE_EVENT_VAR_SPACING) * zoomScale;

            GUI.Label(new Rect(fieldX, fieldY, CHOICE_EVENT_NAME_LABEL_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale), "Name:", labelStyle);

            fieldX += CHOICE_EVENT_NAME_LABEL_WIDTH * zoomScale;
            string[] varNames = GetVariableNamesByType(varChange.VariableType);

            string newConsVarName = DrawVariableNamePopup(
                new Rect(fieldX, fieldY, CHOICE_EVENT_VAR_POPUP_WIDTH_CONS * zoomScale, FIELD_ROW_HEIGHT * zoomScale),
                varNames,
                varChange.VariableName,
                popupStyle,
                CollectUsedVariableNames(context.Siblings, context.Index), context.Controls.Field(ChoiceConsequenceSchema.VariableName));

            if (newConsVarName != varChange.VariableName)
            {
                RecordEditorIntent("Change Variable Name");
                varChange.VariableName = newConsVarName;
                InvalidateReferenceIndex();
            }

            fieldX += (CHOICE_EVENT_OP_TO_VAL_GAP + CHOICE_EVENT_VAR_SPACING) * zoomScale;

            if (blockNodeInteraction) GUI.enabled = false;
            else GUI.enabled = !string.IsNullOrEmpty(varChange.VariableName);

            Rect consequenceOperationRect = new Rect(fieldX, fieldY, CHOICE_EVENT_STANET_LABEL_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale);

            if (varChange.IsNumeric)
            {
                string operationKey = context.Controls.Field(ChoiceConsequenceSchema.Operation);
                Color operationColor = EndsAiHighlightED.BeginField(operationKey);
                EditorGUI.BeginChangeCheck();
                int operation = EditorGUI.Popup(consequenceOperationRect, (int)varChange.Operation, _consequenceOperationLabels, popupStyle);
                GUI.backgroundColor = operationColor;
                EndsAiHighlightED.ReleaseIfAltHovered(operationKey, consequenceOperationRect);

                if (EditorGUI.EndChangeCheck())
                {
                    RecordEditorIntent("Change Consequence Operation");
                    ChoiceConsequenceSchema.Operation.Write(varChange, (ConsequenceOperation)operation);
                }
            }
            else GUI.Label(consequenceOperationRect, "Becomes:", labelCenterStyle);

            fieldX += (CHOICE_EVENT_STANET_LABEL_WIDTH + CHOICE_EVENT_VAR_SPACING) * zoomScale;
            Rect consequenceValueRect = new Rect(fieldX, fieldY, CHOICE_EVENT_VALUE_FIELD_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale);

            if (varChange.VariableType == ConditionVariableType.Bool) consequenceValueRect.width = FIELD_ROW_HEIGHT * zoomScale;

            Color consequenceValueColor = EndsAiHighlightED.BeginField(consequenceValueKey);

            switch (varChange.VariableType)
            {
                case ConditionVariableType.Bool:
                    bool newBool = DrawZoomScaledBoolToggle(
                        new Rect(fieldX, fieldY, FIELD_ROW_HEIGHT * zoomScale, FIELD_ROW_HEIGHT * zoomScale),
                        varChange.BoolValue,
                        zoomScale,
                        buttonStyle);

                    if (newBool != varChange.BoolValue)
                    {
                        RecordEditorIntent("Change Bool Value");
                        varChange.BoolValue = newBool;
                    }

                    break;

                case ConditionVariableType.Int:

                    int newInt = EditorGUI.IntField(new Rect(fieldX, fieldY, CHOICE_EVENT_VALUE_FIELD_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale), varChange.IntValue, textAreaStyle);

                    if (newInt != varChange.IntValue)
                    {
                        RecordEditorIntent("Change Int Value");
                        varChange.IntValue = newInt;
                    }

                    break;

                case ConditionVariableType.Float:

                    float newFloat = EditorGUI.FloatField(new Rect(fieldX, fieldY, CHOICE_EVENT_VALUE_FIELD_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale), varChange.FloatValue, textAreaStyle);

                    if (newFloat != varChange.FloatValue)
                    {
                        RecordEditorIntent("Change Float Value");
                        varChange.FloatValue = newFloat;
                    }

                    break;

                case ConditionVariableType.String:

                    EditorGUI.BeginChangeCheck();
                    string newString = EditorGUI.TextField(new Rect(fieldX, fieldY, CHOICE_EVENT_VALUE_FIELD_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale), varChange.StringValue, textAreaStyle);

                    if (EditorGUI.EndChangeCheck() && newString != varChange.StringValue)
                    {
                        RecordEditorIntent("Change String Value");
                        varChange.StringValue = newString;
                    }

                    break;
            }

            EndsAiHighlightED.EndField(consequenceValueKey, consequenceValueRect, consequenceValueColor);

            if (blockNodeInteraction) GUI.enabled = false;
            else GUI.enabled = true;
            context.X = fieldX;
        }

    }
}
#endif