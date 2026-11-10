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
        private void DrawRedirectPayload(Rect bounds, DialogNode node, NodeDrawContext context)
        {
            var contentWidthZS = context.ContentWidth;
            var labelStyle = context.Styles.Label;
            var popupStyle = context.Styles.Popup;
            float currentY = context.CursorY;

            if (node.RedirectData == null) node.RedirectData = EndsDialogRulesED.CreateRedirect(selectedDialog?.initiator);

            DrawRedirectNodeContent(node, contentWidthZS, zoomScale, labelStyle, popupStyle);
            context.CursorY = currentY;
        }

        private void DrawRedirectNodeToolbar(DialogNode node, float nodeWidthZS, float spacingZS, float zoomScale, GUIStyle buttonStyle, Event e)
        {
            GUI.backgroundColor = DELETE_BUTTON_COLOR;

            if (GUI.Button(new Rect(nodeWidthZS - spacingZS - NODE_BUTTON_SIZE * zoomScale, 0, NODE_BUTTON_SIZE * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale), _deleteNodeContent, buttonStyle) && e.button == 0)
            {
                RecordEditorIntent("Remove Node");
                RemoveDialogNode(selectedDialog, node);
                GUIUtility.ExitGUI();
            }

            GUI.backgroundColor = Color.white;

            if (GUI.Button(new Rect(nodeWidthZS - 2 * spacingZS - 2 * NODE_BUTTON_SIZE * zoomScale, 0, NODE_BUTTON_SIZE * zoomScale, NODE_TOOLBAR_HEIGHT * zoomScale), _duplicateNodeContent, buttonStyle) && e.button == 0)
            {
                RecordEditorIntent("Duplicate Node");
                EndsGraphCommandsED.Duplicate(selectedDialog, node, EndsNodeIdentityED.Allocate(database, selectedDialog, node.NodeType), new Vector2(DUPLICATE_NODE_OFFSET, DUPLICATE_NODE_OFFSET));
            }
        }

        private void DrawRedirectNodeContent(DialogNode node, float contentWidthZS, float zoomScale, GUIStyle labelStyle, GUIStyle popupStyle)
        {
            if (node.RedirectData == null) return;

            RedirectNode redirect = node.RedirectData;
            float topPadZS = REDIRECT_CONTENT_TOP_PADDING * zoomScale;
            float sectionGapZS = REDIRECT_SECTION_GAP * zoomScale;
            float fieldGapZS = REDIRECT_FIELD_GAP * zoomScale;
            float hintLeftZS = REDIRECT_HINT_LEFT_PADDING * zoomScale;
            float hintWidthZS = contentWidthZS - hintLeftZS;
            float currentY = topPadZS;

            DrawRedirectHelpBoxArea(
                new Rect(hintLeftZS, currentY, hintWidthZS, REDIRECT_HINT_HEIGHT * zoomScale),
                redirect,
                zoomScale,
                labelStyle);
            currentY += REDIRECT_HINT_HEIGHT * zoomScale + sectionGapZS;

            List<string> categories = dialogCategories != null ? dialogCategories : new List<string> { DEFAULT_CATEGORY_NAME };
            string[] categoryArray = GetUiCatalog().Categories;
            int categoryIndex = categories.IndexOf(redirect.TargetCategory ?? "");

            if (categoryIndex < 0) categoryIndex = 0;

            GUI.Label(new Rect(0, currentY, REDIRECT_CATEGORY_LABEL_WIDTH * zoomScale, REDIRECT_FIELD_ROW_HEIGHT * zoomScale), "Category:", labelStyle);
            EditorGUI.BeginChangeCheck();
            Rect categoryPopupRect = new Rect(REDIRECT_CATEGORY_LABEL_WIDTH * zoomScale, currentY, contentWidthZS - REDIRECT_CATEGORY_LABEL_WIDTH * zoomScale, REDIRECT_FIELD_ROW_HEIGHT * zoomScale);
            string categoryKey = NodeControls(node).Child(NodeSchema.Redirect).Field(RedirectNodeSchema.Category);
            Color categoryColor = EndsAiHighlightED.BeginField(categoryKey);

            int newCategoryIndex = EditorGUI.Popup(
                categoryPopupRect,
                categoryIndex,
                categoryArray,
                popupStyle);
            GUI.backgroundColor = categoryColor;
            EndsAiHighlightED.ReleaseIfAltHovered(categoryKey, categoryPopupRect);

            if (EditorGUI.EndChangeCheck())
            {
                RecordEditorIntent("Change Redirect Category");
                EndsGraphCommandsED.SetRedirectCategory(database, redirect, categoryArray[newCategoryIndex]);
            }

            currentY += REDIRECT_FIELD_ROW_HEIGHT * zoomScale + fieldGapZS;

            RedirectChoices redirectChoices = GetUiCatalog().GetRedirectChoices(redirect.TargetCategory);
            List<DialogEntry> redirectTargets = redirectChoices.Dialogs;
            string[] dialogArray = redirectChoices.Labels;
            int dialogIndex;
            List<string> dialogNames = redirectChoices.Names;

            if (redirectTargets.Count == 0)
            {
                dialogIndex = 0;
                EditorGUI.BeginDisabledGroup(true);
            }
            else
            {
                dialogIndex = dialogNames.IndexOf(redirect.TargetDialogName ?? "");

                if (dialogIndex < 0) dialogIndex = 0;
            }

            GUI.Label(new Rect(0, currentY, REDIRECT_DIALOG_LABEL_WIDTH * zoomScale, REDIRECT_FIELD_ROW_HEIGHT * zoomScale), "Dialog:", labelStyle);

            EditorGUI.BeginChangeCheck();
            Rect dialogPopupRect = new Rect(REDIRECT_DIALOG_LABEL_WIDTH * zoomScale, currentY, contentWidthZS - REDIRECT_DIALOG_LABEL_WIDTH * zoomScale, REDIRECT_FIELD_ROW_HEIGHT * zoomScale);
            string dialogKey = NodeControls(node).Child(NodeSchema.Redirect).Field(RedirectNodeSchema.TargetDialog);
            Color dialogColor = EndsAiHighlightED.BeginField(dialogKey);

            int newDialogIndex = EditorGUI.Popup(
                dialogPopupRect,
                dialogIndex,
                dialogArray,
                popupStyle);
            GUI.backgroundColor = dialogColor;
            EndsAiHighlightED.ReleaseIfAltHovered(dialogKey, dialogPopupRect);

            if (EditorGUI.EndChangeCheck() && dialogNames.Count > 0)
            {
                RecordEditorIntent("Change Redirect Target Dialog");
                EndsGraphCommandsED.SetRedirectTarget(database, redirect, redirect.TargetCategory, dialogNames[newDialogIndex]);
            }

            if (dialogNames.Count == 0) EditorGUI.EndDisabledGroup();
        }

        private static int GetRedirectNodeContentHeight()
        {
            int height = REDIRECT_CONTENT_TOP_PADDING + REDIRECT_HINT_HEIGHT + REDIRECT_SECTION_GAP;
            height += REDIRECT_FIELD_ROW_HEIGHT + REDIRECT_FIELD_GAP + REDIRECT_FIELD_ROW_HEIGHT + REDIRECT_CONTENT_BOTTOM_PADDING;
            return height;
        }
    }
}
#endif