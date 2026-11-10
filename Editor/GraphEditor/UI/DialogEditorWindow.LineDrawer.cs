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
        private static float MeasureLinePayload(DialogNode node, NodeDrawContext context)
        {
            return LINE_NODE_BASE_HEIGHT + LINE_NODE_HEADER_OFFSET
                + MeasureLineAnimations(node.LineData, context)
                + MeasureLineTranslations(node.LineData, context);
        }

        private static float MeasureLineAnimations(LineNode line, NodeDrawContext context)
        {
            if (line?.CharacterActions == null || line.CharacterActions.Count == 0) return 0;

            float height = LINE_NODE_ACTIONS_FOLDOUT;

            if (context.Window != null && context.Window.actionFoldouts.TryGetValue(context.NodeId, out bool expanded) && !expanded) return height;

            foreach (CharacterActionData animation in line.CharacterActions)
            {
                if (animation == null) height += LINE_NODE_ACTION_ITEM_HEIGHT + NODE_SPACE_OFFSET;
                else height += Mathf.RoundToInt(EndsAnimationDrawerED.Drawer.Measure(animation));
            }

            return height;
        }

        private static float MeasureLineTranslations(LineNode line, NodeDrawContext context)
        {
            float height = 0;

            foreach (string language in context.Languages)
            {
                height += LINE_NODE_LANG_FOLDOUT;

                if (context.Window != null && context.Window.languageTextFoldouts.TryGetValue(language, out bool expanded) && !expanded) continue;

                LineText translation = line?.DialogsText?.Find(entry => entry != null && entry.LanguageCode == language);
                height += LINE_NODE_TEXT_AREA_HEIGHT + context.TextMetrics.Height(translation?.Text) - LINE_TEXT_AREA_HEIGHT;

                if (context.VoiceClipsEnabled) height += LINE_NODE_AUDIO_ROW_HEIGHT;
            }

            return height;
        }

        private void DrawLinePayload(Rect bounds, DialogNode node, NodeDrawContext context)
        {
            if (node.LineData == null) node.LineData = EndsDialogRulesED.CreateLine();

            _lineBlockDrawers.Get(LineNodeSchema.Animations).Draw(bounds, node.LineData, context);
            _lineBlockDrawers.Get(LineNodeSchema.NameColor).Draw(bounds, node.LineData, context);
            _lineBlockDrawers.Get(LineNodeSchema.Translations).Draw(bounds, node.LineData, context);
        }

        private void DrawLineAnimations(Rect bounds, LineNode line, NodeDrawContext context)
        {
            var id = context.WindowId;
            var node = context.Node;
            var contentWidthZS = context.ContentWidth;
            var labelStyle = context.Styles.Label;
            var foldoutToggleStyle = context.Styles.Label;
            var popupStyle = context.Styles.Popup;
            var buttonStyle = context.Styles.Button;
            float currentY = context.CursorY;
            bool hasActions = line.CharacterActions != null && line.CharacterActions.Count > 0;

            if (hasActions)
            {
                if (!actionFoldouts.ContainsKey(node.nodeId)) actionFoldouts[node.nodeId] = true;

                Rect toggleRect = new Rect(0, currentY, FOLDOUT_ARROW_WIDTH + contentWidthZS - ACTION_ROW_HEIGHT * zoomScale, ACTION_ROW_HEIGHT * zoomScale);
                string actionsFoldoutLabel;

                if (actionFoldouts[node.nodeId]) actionsFoldoutLabel = "▽ Character actions";
                else actionsFoldoutLabel = "▶ Character actions";
                actionFoldouts[node.nodeId] = GUI.Toggle(toggleRect, actionFoldouts[node.nodeId], actionsFoldoutLabel, foldoutToggleStyle);
                currentY += ACTION_ROW_HEIGHT * zoomScale;

                if (actionFoldouts[node.nodeId])
                {
                    List<int> actionsToRemove = new List<int>();
                    EndsAnimationDrawerED.Styles animationStyles = new EndsAnimationDrawerED.Styles(labelStyle, popupStyle, buttonStyle, _actionNumberFieldStyle, _actionSliderStyle, _actionSliderThumbStyle);
                    EndsAnimationDrawerED.ICatalog animationCatalog = null;

                    if (database != null) animationCatalog = GetUiCatalog();

                    if (_animationHost == null) _animationHost = new AnimationHost(this);

                    for (int k = 0; k < line.CharacterActions.Count; k++)
                    {
                        CharacterActionData animation = line.CharacterActions[k];
                        EndsAnimationDrawerED.Context animationContext = new EndsAnimationDrawerED.Context(database, node, k, NodeControls(node).Child(NodeSchema.Line).Item(LineNodeSchema.Animations, animation, k), NodeShade(id, node),
                            zoomScale, animationStyles, animationCatalog, _animationHost, actionsToRemove);
                        float animationHeight = EndsAnimationDrawerED.Drawer.Measure(animation) * zoomScale;
                        EndsAnimationDrawerED.Drawer.Draw(new Rect(0, currentY, contentWidthZS, animationHeight), animation, animationContext);
                        currentY += EndsAnimationDrawerED.Drawer.Measure(animation) * zoomScale;
                    }

                    if (actionsToRemove.Count > 0)
                    {
                        RecordEditorIntent("Remove Character Action");
                        foreach (int index in actionsToRemove
                            .OrderByDescending(i => i))
                        {
                            EndsNodeContentCommandsED.RemoveAction(line, index);
                        }
                    }
                }
            }
            context.CursorY = currentY;
        }

        private void DrawLineSpeaker(Rect bounds, LineNode line, NodeDrawContext context)
        {
            var node = context.Node;
            var e = context.CurrentEvent;
            var contentWidthZS = context.ContentWidth;
            var spacingZS = NODE_SPACE_OFFSET * zoomScale;
            var labelStyle = context.Styles.Label;
            var popupStyle = context.Styles.Popup;
            float currentY = context.CursorY;
            currentY += spacingZS;

            Rect characterLabelRect2 = new Rect(0, currentY, LINE_CHARACTER_LABEL_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale);
            GUI.Label(characterLabelRect2, "Name color:", labelStyle);

            if (DialogReferenceUtility.IsMissing(line.CharacterReferenceState))
            {
                DrawMissingReferenceWarning(
                    new Rect(LINE_CHARACTER_LABEL_WIDTH * zoomScale, currentY, contentWidthZS - LINE_CHARACTER_LABEL_WIDTH * zoomScale, LINE_CHARACTER_ROW_HEIGHT * zoomScale),
                    DialogReferenceMessagesED.FormatMissingCharacterLabel(line.MissingCharacterReferenceLabel),
                    zoomScale,
                    labelStyle);
            }
            else
            {
                string[] characterNames = GetUiCatalog().SpeakerNames;
                int selectedCharacterIndex;

                if (string.IsNullOrEmpty(line.CharacterName)) selectedCharacterIndex = 0;
                else selectedCharacterIndex = Array.IndexOf(characterNames, line.CharacterName);

                if (selectedCharacterIndex == -1) selectedCharacterIndex = 0;

                Rect characterPopupRect = new Rect(LINE_CHARACTER_LABEL_WIDTH * zoomScale, currentY, LINE_CHARACTER_POPUP_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale);
                EditorGUI.BeginChangeCheck();
                string speakerKey = NodeControls(node).Child(NodeSchema.Line).Field(LineNodeSchema.NameColor);
                Color speakerColor = EndsAiHighlightED.BeginField(speakerKey);

                int newSelectedCharacterIndex = EditorGUI.Popup(characterPopupRect, selectedCharacterIndex, characterNames, popupStyle);
                GUI.backgroundColor = speakerColor;
                EndsAiHighlightED.ReleaseIfAltHovered(speakerKey, characterPopupRect);

                if (EditorGUI.EndChangeCheck() && newSelectedCharacterIndex != selectedCharacterIndex && e.button == 0)
                {

                    RecordEditorIntent("Change Character Name for Line");
                    string selectedName;

                    if (newSelectedCharacterIndex > 0) selectedName = characterNames[newSelectedCharacterIndex];
                    else selectedName = "";

                    EndsNodeContentCommandsED.SetSpeaker(database, line, selectedName);
                }

                if (!string.IsNullOrEmpty(line.CharacterName))
                {
                    var colorEntry = database.CharacterNameColors
                        ?.FirstOrDefault(c => c.Name == line.CharacterName);

                    if (colorEntry != null)
                    {
                        float size = COLOR_PREVIEW_SIZE * zoomScale;
                        float centerY = currentY + (LINE_CHARACTER_ROW_HEIGHT * zoomScale) * 0.5f;
                        Rect colorRect = new Rect(COLOR_PREVIEW_OFFSET * zoomScale, centerY - size * 0.5f, size, size);
                        EditorGUI.DrawRect(colorRect, colorEntry.Color);
                    }
                }

                currentY += LINE_CHARACTER_ROW_HEIGHT * zoomScale;
            }
            context.CursorY = currentY;
        }

        private void DrawLineTranslations(Rect bounds, LineNode line, NodeDrawContext context)
        {
            var node = context.Node;
            var styles = context.Styles;
            var e = context.CurrentEvent;
            var contentWidthZS = context.ContentWidth;
            var labelStyle = context.Styles.Label;
            var foldoutToggleStyle = context.Styles.Label;
            var textFieldStyle = context.Styles.TextField;
            var textAreaStyle = context.Styles.TextArea;
            var buttonStyle = context.Styles.Button;
            float currentY = context.CursorY;
            foreach (string lang in availableLanguages)
            {
                if (!languageTextFoldouts.ContainsKey(lang)) languageTextFoldouts[lang] = true;

                Rect langToggleRect = new Rect(0, currentY, contentWidthZS, ACTION_ROW_HEIGHT * zoomScale);
                bool langFolded = languageTextFoldouts[lang];

                if (e.button == 0)
                {
                    string langFoldoutLabel;

                    if (langFolded) langFoldoutLabel = $"▽ Language: {lang}";
                    else langFoldoutLabel = $"▶ Language: {lang}";

                    languageTextFoldouts[lang] = GUI.Toggle(langToggleRect, languageTextFoldouts[lang], langFoldoutLabel, foldoutToggleStyle);
                }
                currentY += ACTION_ROW_HEIGHT * zoomScale;

                if (languageTextFoldouts[lang])
                {
                    Rect sayerLabelRect = new Rect(0, currentY, LINE_SAYER_LABEL_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale);
                    GUI.Label(sayerLabelRect, "Speaker name:", labelStyle);
                    LineText localizedText = line.DialogsText
                        .FirstOrDefault(t => t != null && t.LanguageCode == lang);
                    bool missingText = localizedText == null;

                    if (localizedText == null)
                    {
                        localizedText = new LineText { LanguageCode = lang, SayerName = "", Text = "" };

                    }

                    Rect sayerFieldRect = new Rect((LINE_SAYER_LABEL_WIDTH + LINE_SAYER_LABEL_SPACING) * zoomScale, currentY, LINE_SAYER_FIELD_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale);
                    EditorGUI.BeginChangeCheck();
                    var translationControls = NodeControls(node).Child(NodeSchema.Line).Item(LineNodeSchema.Translations, localizedText, 0);
                    string sayerKey = translationControls.Field(LineTranslationSchema.SpeakerName);
                    Color sayerFieldColor = EndsAiHighlightED.BeginField(sayerKey);
                    string newSayerName = EditorGUI.TextField(sayerFieldRect, localizedText.SayerName ?? "", textFieldStyle);
                    EndsAiHighlightED.EndField(sayerKey, sayerFieldRect, sayerFieldColor);
                    Color speakerColor = Color.black;

                    if (!string.IsNullOrEmpty(line.CharacterName))
                    {
                        var colorEntry = GetUiCatalog().GetSpeakerColor(line.CharacterName);

                        if (colorEntry != null) speakerColor = colorEntry.Color;
                    }

                    GUIStyle styledLabel = styles.SpeakerLabel;

                    styledLabel.normal.textColor = speakerColor;
                    
                    Rect sayerPreviewRect = new Rect(SAYER_PREVIEW_X * zoomScale, currentY, SAYER_PREVIEW_WIDTH * zoomScale, FIELD_ROW_HEIGHT * zoomScale);
                    GUI.Label(sayerPreviewRect, localizedText.SayerName ?? "", styledLabel);

                    if (EditorGUI.EndChangeCheck() && e.button == 0)
                    {
                        RecordEditorIntent("Change Sayer Name");
                        localizedText.SayerName = newSayerName;
                    }

                    currentY += ACTION_ROW_HEIGHT * zoomScale;
                    Color textColor = GUI.backgroundColor;
                    GUI.backgroundColor = TEXT_AREA_BG;
                    string textKey = translationControls.Field(LineTranslationSchema.Text);
                    float textAreaHeight = GetNodeTextAreaHeight(localizedText.Text);
                    Rect textAreaRect = new Rect(0, currentY, contentWidthZS, textAreaHeight * zoomScale);
                    EditorGUI.BeginChangeCheck();
                    string newText = EditorGUI.TextArea(textAreaRect, localizedText.Text ?? "", textAreaStyle);

                    if (EndsAiHighlightED.IsMarked(textKey)) EndsAiHighlightED.Frame(textAreaRect, textKey);

                    EndsAiHighlightED.ReleaseIfAltHovered(textKey, textAreaRect);

                    if (EditorGUI.EndChangeCheck() && e.button == 0)
                    {
                        RecordEditorIntent("Change Dialog Text");
                        localizedText.Text = newText;
                    }

                    GUI.backgroundColor = textColor;
                    currentY += (textAreaHeight + NODE_SPACE_OFFSET) * zoomScale;

                    if (database.VoiceClipsEnabled) currentY = DrawLineVoiceClipRow(localizedText, currentY, contentWidthZS, zoomScale, labelStyle, buttonStyle, e, translationControls.Field(LineTranslationSchema.VoiceClip));

                    if (missingText && (!string.IsNullOrEmpty(localizedText.SayerName) || !string.IsNullOrEmpty(localizedText.Text) || localizedText.VoiceClip != null)) line.DialogsText.Add(localizedText);
                }
            }
            context.CursorY = currentY;
        }

    }
}
#endif