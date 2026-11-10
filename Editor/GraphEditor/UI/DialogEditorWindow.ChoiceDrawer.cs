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
        private static float MeasureChoicePayload(DialogNode node, NodeDrawContext context)
        {
            if (node.ChoiceData == null || node.ChoiceData.Count == 0) return CHOICE_NODE_EMPTY_HEIGHT;

            float height = LINE_NODE_BASE_HEIGHT;

            foreach (ChoiceNode choice in node.ChoiceData)
            {
                if (choice != null) height += MeasureChoiceOption(choice, context);
            }

            return height;
        }

        private static float MeasureChoiceOption(ChoiceNode choice, NodeDrawContext context)
        {
            return CHOICE_HEADER_HEIGHT
                + MeasureChoiceConditions(choice, context)
                + MeasureChoiceConsequences(choice, context)
                + MeasureChoiceTranslations(choice, context);
        }

        private static float MeasureChoiceConditions(ChoiceNode choice, NodeDrawContext context)
        {
            if (choice.ChoiceCondition == null || choice.ChoiceCondition.Count == 0) return 0;

            float height = CHOICE_HEADER_HEIGHT;

            if (context.Window != null && context.Window.conditionFoldouts.TryGetValue(context.NodeId, out bool expanded) && !expanded) return height;

            return height + choice.ChoiceCondition.Count * CHOICE_EVENT_ITEM_HEIGHT;
        }

        private static float MeasureChoiceConsequences(ChoiceNode choice, NodeDrawContext context)
        {
            if (choice.ChoiceConsequence == null || choice.ChoiceConsequence.Count == 0) return 0;

            float height = CHOICE_HEADER_HEIGHT;

            if (context.Window != null && context.Window.variableChangeFoldouts.TryGetValue(context.NodeId, out bool expanded) && !expanded) return height;

            return height + choice.ChoiceConsequence.Count * CHOICE_EVENT_ITEM_HEIGHT;
        }

        private static float MeasureChoiceTranslations(ChoiceNode choice, NodeDrawContext context)
        {
            float height = 0;

            foreach (string language in context.Languages)
            {
                height += CHOICE_LANG_FOLDOUT;

                if (context.Window != null && context.Window.choiceLanguageFoldouts.TryGetValue(language, out bool expanded) && !expanded) continue;

                ChoiceText translation = choice.Translations?.Find(entry => entry != null && entry.LanguageCode == language);
                height += CHOICE_TEXT_AREA_HEIGHT + context.TextMetrics.Height(translation?.Text) - LINE_TEXT_AREA_HEIGHT;
            }

            return height;
        }

        private void DrawChoicePayload(Rect bounds, DialogNode node, NodeDrawContext context)
        {
            var spacingZS = NODE_SPACE_OFFSET * zoomScale;
            float currentY = context.CursorY;
            currentY += spacingZS; // Initial spacing
            List<int> choicesToRemove = new List<int>();

            for (int i = 0; i < node.ChoiceData.Count; i++)
            {
                ChoiceNode choice = node.ChoiceData[i];

                if (choice == null) continue;

                context.CursorY = currentY;
                context.ChoiceIndex = i;
                context.ChoicesToRemove = choicesToRemove;
                _choiceOptionDrawers.Get(ChoiceOptionSchema).Draw(bounds, choice, context);
                currentY = context.CursorY;
            }

            if (choicesToRemove.Count > 0)
            {
                RecordEditorIntent("Remove Option");
                foreach (int index in choicesToRemove
                    .OrderByDescending(idx => idx))
                {
                    if (pendingPort != null && pendingPort.Node == node && pendingPort.ChoiceIndex >= 0)
                    {
                        if (pendingPort.ChoiceIndex == index) { selectedPorts.Clear(); pendingPort = null; }
                        else if (pendingPort.ChoiceIndex > index) pendingPort = new SelectedPort(node, false, pendingPort.ChoiceIndex - 1);
                    }
                    EndsNodeContentCommandsED.RemoveChoice(node, index);
                }
            }

            currentY += spacingZS; // Final spacing
            context.CursorY = currentY;
        }

        private void DrawChoiceOption(Rect bounds, ChoiceNode choice, NodeDrawContext context)
        {
            var id = context.WindowId;
            var node = context.Node;
            var contentWidthZS = context.ContentWidth;
            var spacingZS = NODE_SPACE_OFFSET * zoomScale;
            var labelStyle = context.Styles.Label;
            var buttonStyle = context.Styles.Button;
            int i = context.ChoiceIndex;
            List<int> choicesToRemove = context.ChoicesToRemove;
            float currentY = context.CursorY;
            Rect horizontalRect = new Rect(0, currentY, contentWidthZS, FIELD_ROW_HEIGHT * zoomScale);
            string choiceKey = OptionControls(node, i).Key;
            EndsAiHighlightED.PaintPlate(horizontalRect, choiceKey, NodeShade(id, node));
            EndsAiHighlightED.ReleaseIfAltHovered(choiceKey, horizontalRect);

            GUI.BeginGroup(horizontalRect);
            {
                float x = spacingZS * 2;

                GUI.Label(new Rect(x, 0, CHOICE_ROW_LABEL_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale), "Option " + (i + 1), labelStyle);
                //x += 115 * zoomScale; // 110+5

                if (GUI.Button(new Rect(contentWidthZS - CHOICE_CLOSE_BUTTON_OFFSET * zoomScale, 0, ACTION_BUTTON_SIZE * zoomScale, FIELD_ROW_HEIGHT * zoomScale), _deleteChoiceContent, buttonStyle)) // -35 = -20 (button width) -10 (1/4 port width) - 5 (padding)
                {
                    RecordEditorIntent("Remove Option");
                    choicesToRemove.Add(i);
                }

                if (GUI.Button(new Rect(contentWidthZS - CHOICE_CONSEQUENCE_BUTTON_OFFSET * zoomScale, 0, CHOICE_CONSEQUENCE_BUTTON_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale), _addConsequenceContent, buttonStyle)) // contentWidthZS  - (35 - 95 - 5) * zoomScale
                {
                    RecordEditorIntent("Add Option Consequence");
                    EndsNodeContentCommandsED.AddChoiceConsequence(choice, EndsDialogRulesED.CreateChoiceConsequence(database));
                }

                if (GUI.Button(new Rect(contentWidthZS - CHOICE_CONDITION_BUTTON_OFFSET * zoomScale, 0, CHOICE_CONDITION_BUTTON_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale), _addChoiceConditionContent, buttonStyle)) // contentWidthZS  - (35 - 95 - 5 - 80 - 5) * zoomScale
                {
                    RecordEditorIntent("Add Option Condition");
                    EndsNodeContentCommandsED.AddChoiceCondition(choice, EndsDialogRulesED.CreateChoiceCondition(database));
                }
            }

            GUI.EndGroup();
            currentY += FIELD_ROW_HEIGHT * zoomScale;

            DrawChoiceBlock(ChoiceOptionSchema.Conditions, bounds, choice, context, ref currentY);
            DrawChoiceBlock(ChoiceOptionSchema.Consequences, bounds, choice, context, ref currentY);
            DrawChoiceBlock(ChoiceOptionSchema.Translations, bounds, choice, context, ref currentY);
            currentY += spacingZS;
            context.CursorY = currentY;
        }

        private void DrawChoiceBlock(NodeElementDefinition<ChoiceNode> definition, Rect bounds, ChoiceNode choice, NodeDrawContext context, ref float currentY)
        {
            context.CursorY = currentY;
            _choiceOptionDrawers.Get(definition).Draw(bounds, choice, context);
            currentY = context.CursorY;
        }

        private void DrawChoiceConditions(Rect bounds, ChoiceNode choice, NodeDrawContext context)
        {
            var id = context.WindowId;
            var node = context.Node;
            var styles = context.Styles;
            var blockNodeInteraction = context.BlockInteraction;
            var contentWidthZS = context.ContentWidth;
            var spacingZS = NODE_SPACE_OFFSET * zoomScale;
            var labelStyle = context.Styles.Label;
            var foldoutToggleStyle = context.Styles.Label;
            var popupStyle = context.Styles.Popup;
            var textAreaStyle = context.Styles.TextArea;
            var buttonStyle = context.Styles.Button;
            int i = context.ChoiceIndex;
            var optionControls = OptionControls(context.Node, i);
            float currentY = context.CursorY;

            if (choice.ChoiceCondition != null && choice.ChoiceCondition.Count > 0) // Conditions
            {
                string varFoldoutKey = node.nodeId;

                if (!conditionFoldouts.ContainsKey(varFoldoutKey)) conditionFoldouts[varFoldoutKey] = true;

                Rect varToggleRect = new Rect(0, currentY, CHOICE_FOLDOUT_WIDTH * zoomScale, ACTION_ROW_HEIGHT * zoomScale);
                bool varFolded = conditionFoldouts[varFoldoutKey];

                if (Event.current.button == 0 && Event.current.type == EventType.MouseDown && varToggleRect.Contains(Event.current.mousePosition))
                {
                    conditionFoldouts[varFoldoutKey] = !conditionFoldouts[varFoldoutKey];
                    Event.current.Use();
                }

                string choiceConditionsFoldoutLabel;

                if (varFolded) choiceConditionsFoldoutLabel = "▽ Choice visibility conditions";
                else choiceConditionsFoldoutLabel = "▶ Choice visibility conditions";

                GUI.Toggle(varToggleRect, varFolded, choiceConditionsFoldoutLabel, foldoutToggleStyle);

                if (conditionFoldouts[varFoldoutKey])
                {

                    if (EndsNodeDrawersED.ShowConditionLogic(choice.ChoiceCondition))
                    {
                        float logicRowY = currentY + CHOICE_LOGIC_ROW_Y_OFFSET * zoomScale;
                        float logicToggleSize = CHOICE_LOGIC_TOGGLE_SIZE * zoomScale;
                        float logicGap = CHOICE_LOGIC_GAP * zoomScale;
                        float logicLabelWidth = CHOICE_LOGIC_LABEL_WIDTH * zoomScale;
                        float logicToggleX = contentWidthZS - (CHOICE_LOGIC_RIGHT_PADDING + CHOICE_LOGIC_TOGGLE_SIZE) * zoomScale;
                        float logicLabelX = logicToggleX - logicGap - logicLabelWidth;
                        GUIStyle logicLabelStyle = styles.LogicLabel;
                        GUI.Label(new Rect(logicLabelX, logicRowY, logicLabelWidth, logicToggleSize), "AND/OR toggle:", logicLabelStyle);
                        Rect varToggleOrAnd = new Rect(logicToggleX, logicRowY, logicToggleSize, logicToggleSize);
                        Color choiceLogicColor = EndsAiHighlightED.BeginField(optionControls.Field(ChoiceOptionSchema.AllConditionsRequired));

                        if (choice.IsAndLogic)
                        {
                            if (GUI.Button(varToggleOrAnd, _andLogicContent, buttonStyle))
                            {
                                RecordEditorIntent("Toggle Choice Condition Logic");
                                EndsNodeContentCommandsED.SetChoiceLogic(choice, false);
                            }
                        }
                        else
                        {
                            if (GUI.Button(varToggleOrAnd, _orLogicContent, buttonStyle))
                            {
                                RecordEditorIntent("Toggle Choice Condition Logic");
                                EndsNodeContentCommandsED.SetChoiceLogic(choice, true);
                            }
                        }

                        EndsAiHighlightED.EndField(optionControls.Field(ChoiceOptionSchema.AllConditionsRequired), varToggleOrAnd, choiceLogicColor);
                    }
                }

                currentY += ACTION_ROW_HEIGHT * zoomScale;

                if (conditionFoldouts[varFoldoutKey])
                {
                    List<int> varsToRemove = new List<int>();

                    for (int j = 0; j < choice.ChoiceCondition.Count; j++)
                    {
                        var varChange = choice.ChoiceCondition[j];
                        float eventHeight = ACTION_ROW_HEIGHT * zoomScale;
                        Rect eventBoxRect = new Rect(0, currentY, contentWidthZS, eventHeight);
                        var conditionControls = optionControls.Item(ChoiceOptionSchema.Conditions, choice.ChoiceCondition[j], j);
                        string conditionKey = conditionControls.Key;
                        EndsAiHighlightED.PaintPlate(eventBoxRect, conditionKey, NodeShade(id, node));
                        EndsAiHighlightED.ReleaseIfAltHovered(conditionKey, eventBoxRect);
                        float fieldY = currentY + CHOICE_EVENT_FIELD_TOP_PADDING * zoomScale;

                        var shellParams = new ConditionRowShellParams
                        {
                            Layout = ConditionRowShellLayout.ChoiceConditionRow,
                            Index = j,
                            ReferenceState = varChange.ReferenceState,
                            VariableName = varChange.VariableName,
                            MissingReferenceLabel = varChange.MissingReferenceLabel,
                            ContentWidth = contentWidthZS,
                            RowBaseY = currentY,
                            ZoomScale = zoomScale,
                            WarningBoundsRect = eventBoxRect,
                            LabelStyle = labelStyle,
                            ButtonStyle = buttonStyle,
                            DeleteTooltip = "Delete condition"
                        };

                        if (DrawConditionRowShell(shellParams, varsToRemove, out fieldY))
                        {
                            currentY += eventHeight + CHOICE_EVENT_ITEM_SPACING * zoomScale;
                            continue;
                        }

                        float fieldX = spacingZS;
                        DrawChoiceConditionEditableFields(
                            varChange,
                            j,
                            choice.ChoiceCondition,
                            fieldY,
                            ref fieldX,
                            new ConditionFieldsDrawContext
                            {
                                ZoomScale = zoomScale,
                                ContentWidth = contentWidthZS,
                                LabelStyle = labelStyle,
                                PopupStyle = popupStyle,
                                TextFieldStyle = textAreaStyle,
                                ButtonStyle = buttonStyle,
                                BlockInteraction = blockNodeInteraction,
                                CompactOperatorLabels = true,
                            }, conditionControls);

                        currentY += eventHeight + CHOICE_EVENT_ITEM_SPACING * zoomScale;
                    }

                    if (varsToRemove.Count > 0)
                    {
                        RecordEditorIntent("Remove Condition");

                        foreach (int index in varsToRemove
                            .OrderByDescending(idx => idx))
                        {
                            EndsNodeContentCommandsED.RemoveChoiceCondition(choice, index);
                        }
                    }
                }
            }
            context.CursorY = currentY;
        }

        private void DrawChoiceConsequences(Rect bounds, ChoiceNode choice, NodeDrawContext context)
        {
            var id = context.WindowId;
            var node = context.Node;
            var blockNodeInteraction = context.BlockInteraction;
            var contentWidthZS = context.ContentWidth;
            var spacingZS = NODE_SPACE_OFFSET * zoomScale;
            var labelStyle = context.Styles.Label;
            var foldoutToggleStyle = context.Styles.Label;
            var popupStyle = context.Styles.Popup;
            var textAreaStyle = context.Styles.TextArea;
            var buttonStyle = context.Styles.Button;
            int i = context.ChoiceIndex;
            float currentY = context.CursorY;

            if (choice.ChoiceConsequence != null && choice.ChoiceConsequence.Count > 0) // variable consequences
            {
                string varFoldoutKey = node.nodeId;

                if (!variableChangeFoldouts.ContainsKey(varFoldoutKey)) variableChangeFoldouts[varFoldoutKey] = true;

                Rect varToggleRect = new Rect(0, currentY, contentWidthZS, ACTION_ROW_HEIGHT * zoomScale);
                bool varFolded = variableChangeFoldouts[varFoldoutKey];

                if (Event.current.button == 0 && Event.current.type == EventType.MouseDown && varToggleRect.Contains(Event.current.mousePosition))
                {
                    variableChangeFoldouts[varFoldoutKey] = !variableChangeFoldouts[varFoldoutKey];
                    Event.current.Use();
                }

                string choiceConsequencesFoldoutLabel;

                if (varFolded) choiceConsequencesFoldoutLabel = "▽ Choice consequences";
                else choiceConsequencesFoldoutLabel = "▶ Choice consequences";

                GUI.Toggle(varToggleRect, varFolded, choiceConsequencesFoldoutLabel, foldoutToggleStyle);
                currentY += ACTION_ROW_HEIGHT * zoomScale;

                if (variableChangeFoldouts[varFoldoutKey])
                {
                    List<int> varsToRemove = new List<int>();

                    for (int j = 0; j < choice.ChoiceConsequence.Count; j++)
                    {
                        var varChange = choice.ChoiceConsequence[j];
                        float eventHeight = ACTION_ROW_HEIGHT * zoomScale;
                        Rect eventBoxRect = new Rect(0, currentY, contentWidthZS, eventHeight);

                        var consequenceControls = OptionControls(node, i).Item(ChoiceOptionSchema.Consequences, choice.ChoiceConsequence[j], j);
                        string consequenceKey = consequenceControls.Key;
                        EndsAiHighlightED.PaintPlate(eventBoxRect, consequenceKey, NodeShade(id, node));
                        EndsAiHighlightED.ReleaseIfAltHovered(consequenceKey, eventBoxRect);

                        float fieldY = currentY + CHOICE_EVENT_FIELD_TOP_PADDING * zoomScale;

                        var shellParams = new ConditionRowShellParams
                        {
                            Layout = ConditionRowShellLayout.ChoiceConditionRow,
                            Index = j,
                            ReferenceState = varChange.ReferenceState,
                            VariableName = varChange.VariableName,
                            MissingReferenceLabel = varChange.MissingReferenceLabel,
                            ContentWidth = contentWidthZS,
                            RowBaseY = currentY,
                            ZoomScale = zoomScale,
                            WarningBoundsRect = eventBoxRect,
                            LabelStyle = labelStyle,
                            ButtonStyle = buttonStyle,
                            DeleteTooltip = "Delete consequence"
                        };

                        if (DrawConditionRowShell(shellParams, varsToRemove, out fieldY))
                        {
                            currentY += eventHeight + CHOICE_EVENT_ITEM_SPACING * zoomScale;
                            continue;
                        }

                        float fieldX = spacingZS;

                        EnsureConditionFieldDrawers();
                        var fieldContext = _choiceConsequenceFieldContext;
                        fieldContext.Index = j;
                        fieldContext.Siblings = choice.ChoiceConsequence;
                        fieldContext.Controls = consequenceControls;
                        fieldContext.X = fieldX;
                        fieldContext.Y = fieldY;
                        fieldContext.Fields = new ConditionFieldsDrawContext
                        {
                            ZoomScale = zoomScale,
                            ContentWidth = contentWidthZS,
                            LabelStyle = labelStyle,
                            PopupStyle = popupStyle,
                            TextFieldStyle = textAreaStyle,
                            ButtonStyle = buttonStyle,
                            BlockInteraction = blockNodeInteraction
                        };
                        _choiceConsequenceDrawers.Get(ChoiceConsequenceSchema).Draw(eventBoxRect, varChange, fieldContext);

                        currentY += eventHeight + CHOICE_EVENT_ITEM_SPACING * zoomScale;
                    }

                    if (varsToRemove.Count > 0)
                    {
                        RecordEditorIntent("Remove Consequence");

                        foreach (int index in varsToRemove
                            .OrderByDescending(idx => idx))
                        {
                            EndsNodeContentCommandsED.RemoveChoiceConsequence(choice, index);
                        }
                    }
                }
            }
            context.CursorY = currentY;
        }

        private void DrawChoiceTranslations(Rect bounds, ChoiceNode choice, NodeDrawContext context)
        {
            var node = context.Node;
            var contentWidthZS = context.ContentWidth;
            var spacingZS = NODE_SPACE_OFFSET * zoomScale;
            var foldoutToggleStyle = context.Styles.Label;
            var textAreaStyle = context.Styles.TextArea;
            int i = context.ChoiceIndex;
            float currentY = context.CursorY;
            foreach (string lang in availableLanguages) // Translations
            {
                if (!choiceLanguageFoldouts.ContainsKey(lang)) choiceLanguageFoldouts[lang] = true;

                Rect langToggleRect = new Rect(0, currentY, contentWidthZS, ACTION_ROW_HEIGHT * zoomScale);    // Language toggle
                bool langFolded = choiceLanguageFoldouts[lang];

                if (Event.current.button == 0 && Event.current.type == EventType.MouseDown && langToggleRect.Contains(Event.current.mousePosition))
                {
                    choiceLanguageFoldouts[lang] = !choiceLanguageFoldouts[lang];
                    Event.current.Use();
                }

                string langFoldoutLabel;

                if (langFolded) langFoldoutLabel = $"▽ Language: {lang}";
                else langFoldoutLabel = $"▶ Language: {lang}";

                GUI.Toggle(langToggleRect, langFolded, langFoldoutLabel, foldoutToggleStyle);
                currentY += ACTION_ROW_HEIGHT * zoomScale;

                if (choiceLanguageFoldouts[lang])
                {
                    var textEntry = choice.Translations?.Find(t => t != null && t.LanguageCode == lang);
                    bool missingText = textEntry == null;

                    if (textEntry == null)
                    {
                        textEntry = new ChoiceText { LanguageCode = lang };

                    }

                    Color choiceTextColor = GUI.backgroundColor;
                    GUI.backgroundColor = TEXT_AREA_BG;
                    string choiceTextKey = OptionControls(node, i).Item(ChoiceOptionSchema.Translations, textEntry, 0).Field(ChoiceTranslationSchema.Text);
                    EditorGUI.BeginChangeCheck();
                    float textAreaHeight = GetNodeTextAreaHeight(textEntry.Text);
                    Rect choiceTextRect = new Rect(0, currentY, contentWidthZS, textAreaHeight * zoomScale);
                    string newText = EditorGUI.TextArea(choiceTextRect, textEntry.Text ?? "", textAreaStyle);

                    if (EndsAiHighlightED.IsMarked(choiceTextKey)) EndsAiHighlightED.Frame(choiceTextRect, choiceTextKey);

                    EndsAiHighlightED.ReleaseIfAltHovered(choiceTextKey, choiceTextRect);

                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordEditorIntent("Edit Choice Text");
                        textEntry.Text = newText;

                        if (missingText)
                        {
                            if (choice.Translations == null) choice.Translations = new List<ChoiceText>();

                            choice.Translations.Add(textEntry);
                        }
                    }

                    GUI.backgroundColor = choiceTextColor;
                    currentY += textAreaHeight * zoomScale + spacingZS;
                }
            }
            context.CursorY = currentY;
        }

    }
}
#endif