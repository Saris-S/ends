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
        private static float MeasureConditionPayload(DialogNode node, NodeDrawContext context)
        {
            float height = CONDITION_NODE_BASE_HEIGHT;

            if (node.ConditionData?.DialogConditions == null || node.ConditionData.DialogConditions.Count == 0) return height;

            height += CONDITION_NODE_FOLDOUT_OFFSET;

            if (context.Window != null && context.Window.conditionFoldouts.TryGetValue(node.nodeId, out bool expanded) && !expanded) return height;

            return height + node.ConditionData.DialogConditions.Count * CONDITION_NODE_CONDITION_HEIGHT;
        }

        private void DrawConditionPayload(Rect bounds, DialogNode node, NodeDrawContext context)
        {
            var id = context.WindowId;
            var e = context.CurrentEvent;
            var blockNodeInteraction = context.BlockInteraction;
            var nodeWidthZS = context.NodeWidth;
            var contentWidthZS = context.ContentWidth;
            var conditionAreaRect = bounds;
            var labelStyle = context.Styles.Label;
            var labelCenterStyle = context.Styles.CenterLabel;
            var foldoutToggleStyle = context.Styles.Label;
            var textFieldStyle = context.Styles.TextField;
            var popupStyle = context.Styles.Popup;
            var buttonStyle = context.Styles.Button;
            float currentY = context.CursorY;

            if (node.ConditionData == null)
            {
                GUI.Label(conditionAreaRect, "Condition data is missing. Add a condition to repair this node.", labelStyle);
                return;
            }

            ConditionNode conditionSnapshot = EndsDialogConditionContractED.ReadSnapshot(node.ConditionData);
            int conditionRevision = EndsSessionED.ForDatabase(database).ObservedRevision;
            string conditionDialogName = selectedDialogName;
            string conditionNodeId = node.nodeId;
            currentY += CONDITION_HEADER_OFFSET * zoomScale;

            // Move the AND/OR button under the label

            if (EndsNodeDrawersED.ShowConditionLogic(conditionSnapshot.DialogConditions))
            {

                GUI.Label(new Rect(CONDITION_LOGIC_LABEL_X * zoomScale, currentY, CONDITION_LOGIC_LABEL_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale), "AND/OR toggle:", labelCenterStyle);
                bool currentLogic = conditionSnapshot.IsAndLogic;

                Rect logicToggleRect = new Rect(CONDITION_LOGIC_TOGGLE_X * zoomScale, currentY, FIELD_ROW_HEIGHT * zoomScale, FIELD_ROW_HEIGHT * zoomScale);

                GUIContent logicButtonLabel;

                if (currentLogic) logicButtonLabel = _andLogicContent;
                else logicButtonLabel = _orLogicContent;
                var conditionNodeControls = NodeControls(node).Child(NodeSchema.Condition);
                string logicKey = conditionNodeControls.Field(ConditionNodeSchema.AllConditionsRequired);
                Color logicColor = EndsAiHighlightED.BeginField(logicKey);

                if (GUI.Button(logicToggleRect, logicButtonLabel, buttonStyle) && e.button == 0) SubmitConditionEdit(conditionDialogName, conditionNodeId, conditionSnapshot, false, !currentLogic, conditionRevision);

                EndsAiHighlightED.EndField(logicKey, logicToggleRect, logicColor);
            }

            float truePortY_ZS = CONDITION_TRUE_PORT_Y * zoomScale;
            float falsePortY_ZS = (CONDITION_TRUE_PORT_Y + NODE_SPACE_OFFSET + NODE_PORT_SIZE) * zoomScale;

            Rect trueLabelRect = new Rect(nodeWidthZS - CONDITION_PORT_LABEL_OFFSET * zoomScale, truePortY_ZS, CONDITION_PORT_LABEL_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale);
            EditorGUI.DrawRect(new Rect(nodeWidthZS - CONDITION_PORT_LABEL_OFFSET * zoomScale, truePortY_ZS, CONDITION_PORT_LABEL_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale), CONDITION_TRUE_COLOR);
            GUI.Label(trueLabelRect, "TRUE", labelCenterStyle);

            Rect falseLabelRect = new Rect(nodeWidthZS - CONDITION_PORT_LABEL_OFFSET * zoomScale, falsePortY_ZS, CONDITION_PORT_LABEL_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale);
            EditorGUI.DrawRect(new Rect(nodeWidthZS - CONDITION_PORT_LABEL_OFFSET * zoomScale, falsePortY_ZS, CONDITION_PORT_LABEL_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale), CONDITION_FALSE_COLOR);
            GUI.Label(falseLabelRect, "FALSE", labelCenterStyle);

            currentY += CONDITION_TOGGLE_OFFSET * zoomScale;


            if (conditionSnapshot.DialogConditions != null && conditionSnapshot.DialogConditions.Count > 0)   // Condition list
            {
                string foldoutKey = node.nodeId;

                if (!conditionFoldouts.ContainsKey(foldoutKey)) conditionFoldouts[foldoutKey] = true;

                Rect toggleRect = new Rect(0, currentY, contentWidthZS, ACTION_ROW_HEIGHT * zoomScale);
                bool isFolded = conditionFoldouts[foldoutKey];

                if (e.button == 0 && e.type == EventType.MouseDown && toggleRect.Contains(e.mousePosition))
                {
                    conditionFoldouts[foldoutKey] = !conditionFoldouts[foldoutKey];
                    e.Use();
                }
                string conditionsFoldoutLabel;

                if (isFolded) conditionsFoldoutLabel = "▽ Conditions";
                else conditionsFoldoutLabel = "▶ Conditions";
                GUI.Toggle(toggleRect, isFolded, conditionsFoldoutLabel, foldoutToggleStyle);
                currentY += ACTION_ROW_HEIGHT * zoomScale;

                if (conditionFoldouts[foldoutKey])
                {
                    List<int> conditionsToRemove = new List<int>();
                    for (int i = 0; i < conditionSnapshot.DialogConditions.Count; i++)
                    {
                        DialogCondition condition = conditionSnapshot.DialogConditions[i];

                        if (condition == null) continue;

                        float conditionHeight = CONDITION_NODE_CONDITION_HEIGHT * zoomScale;
                        Rect conditionRect = new Rect(0, currentY, contentWidthZS, conditionHeight);
                        var conditionControls = NodeControls(node).Child(NodeSchema.Condition).Item(ConditionNodeSchema.Conditions, condition, i);
                        string conditionKey = conditionControls.Key;
                        EndsAiHighlightED.PaintPlate(conditionRect, conditionKey, NodeShade(id, node));
                        EndsAiHighlightED.ReleaseIfAltHovered(conditionKey, conditionRect);

                        float fieldY = currentY + 5 * zoomScale;

                        var shellParams = new ConditionRowShellParams
                        {
                            Layout = ConditionRowShellLayout.GraphConditionNode,
                            Index = i,
                            ReferenceState = condition.ReferenceState,
                            VariableName = condition.VariableName,
                            MissingReferenceLabel = condition.MissingReferenceLabel,
                            ContentWidth = contentWidthZS,
                            RowBaseY = currentY,
                            ZoomScale = zoomScale,
                            WarningBoundsRect = conditionRect,
                            LabelStyle = labelStyle,
                            ButtonStyle = buttonStyle,
                            RequireMouseButton = true,
                            CurrentEvent = e
                        };

                        if (DrawConditionRowShell(shellParams, conditionsToRemove, out fieldY))
                        {
                            currentY += conditionHeight;
                            continue;
                        }

                        DrawDialogConditionEditableFields(
                            condition,
                            i,
                            conditionSnapshot.DialogConditions,
                            ref fieldY,
                            new ConditionFieldsDrawContext
                            {
                                ZoomScale = zoomScale,
                                ContentWidth = contentWidthZS,
                                LabelStyle = labelStyle,
                                PopupStyle = popupStyle,
                                TextFieldStyle = textFieldStyle,
                                ButtonStyle = buttonStyle,
                                BlockInteraction = blockNodeInteraction,
                                RequireMouseButton = true,
                                CurrentEvent = e,
                                OnChanged = () => SubmitConditionEdit(conditionDialogName, conditionNodeId, conditionSnapshot, true, null, conditionRevision)
                            }, conditionControls);

                        currentY += conditionHeight;
                    }

                    if (conditionsToRemove.Count > 0)
                    {
                        foreach (int index in conditionsToRemove
                            .OrderByDescending(index => index))
                        {
                            conditionSnapshot.DialogConditions.RemoveAt(index);
                        }

                        SubmitConditionEdit(conditionDialogName, conditionNodeId, conditionSnapshot, true, null, conditionRevision);
                    }
                }
            }
            context.CursorY = currentY;
        }

    }
}
#endif