#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ALTUSHKA.EasyNodeDialogSystem.Editor.EndsNodeStructureED;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary> ENDS. Draws the existing character-animation block through typed root and branch registrations while retaining its manual layout and per-field highlight ownership. </summary>
    internal static class EndsAnimationDrawerED
    {
        #region FIELDS AND CONTEXT

        private const int SPACING = 5;
        private const int ROW_HEIGHT = 25;
        private const int FIELD_HEIGHT = 20;
        private const int BOX_HEIGHT = 60;
        private const int BUTTON_SIZE = 22;
        private const int BUTTON_GAP = 2;
        private const int HEADER_BUTTON_COUNT = 3;
        private const int LABEL_WIDTH = 70;
        private const int POPUP_WIDTH = 105;
        private const int EMOTION_LABEL_WIDTH = 55;
        private const int ZOOM_LABEL_WIDTH = 50;
        private const int XY_LABEL_WIDTH = 30;
        private const int NUMBER_FIELD_WIDTH = 60;

        private static readonly GUIContent _previewContent = new GUIContent("👁", "Preview sprite");
        private static readonly GUIContent _pasteContent = new GUIContent("📥", "Paste action parameters");
        private static readonly GUIContent _copyContent = new GUIContent("📋", "Copy action parameters");
        private static readonly GUIContent _deleteContent = new GUIContent("×", "Remove this character action from the line.");
        private static readonly EndsNodeDrawersED.Registry<CharacterActionData, Context> _drawers = new EndsNodeDrawersED.Registry<CharacterActionData, Context>();
        private static readonly Action<CharacterActionData, float> _setZoom = EndsNodeContentCommandsED.SetActionZoom;
        private static readonly Action<CharacterActionData, float> _setX = EndsNodeContentCommandsED.SetActionX;
        private static readonly Action<CharacterActionData, float> _setY = EndsNodeContentCommandsED.SetActionY;

        /// <summary> ENDS. Supplies the existing cached character and emotion choices without coupling the drawer to a particular window or creating catalog arrays every repaint. </summary>
        internal interface ICatalog
        {
            /// <summary> ENDS. Reports whether character selection is available for this database. </summary>
            bool HasCharacters { get; }
            /// <summary> ENDS. Returns cached choices excluding characters already used by other animation rows. </summary>
            string[] GetActionCharacters(IReadOnlyList<CharacterActionData> animations, int index);
            /// <summary> ENDS. Resolves a character's catalog entry for emotion selection. </summary>
            CharacterGroup GetCharacter(string name);
            /// <summary> ENDS. Returns the cached sprite names for the selected character. </summary>
            string[] GetEmotions(CharacterGroup character);
        }

        /// <summary> ENDS. Supplies editor intent tracking, clipboard, preview and shared diagnostics, all model writes continue through existing Rules and Commands. </summary>
        internal interface IHost
        {
            /// <summary> ENDS. Records the pending manual edit before its command mutates animation data. </summary>
            void RecordIntent(string description);
            /// <summary> ENDS. Returns the clipboard animation when paste is permitted for this node, null selects the copy button. </summary>
            CharacterActionData GetPasteSource(DialogNode node);
            /// <summary> ENDS. Copies an animation using the host's existing node identity and snapshot policy. </summary>
            void CopyAnimation(CharacterActionData animation, DialogNode node);
            /// <summary> ENDS. Opens live preview with the host's current database and dialog identity. </summary>
            void PreviewAnimation(CharacterActionData animation, DialogNode node, int index);
            /// <summary> ENDS. Draws the shared missing-reference warning in the animation's existing bounds. </summary>
            void DrawMissingReference(Rect bounds, string message, float scale, GUIStyle labelStyle);
        }

        /// <summary> ENDS. Reuses graph styles supplied by the caller, drawing creates no per-row GUIStyle instances. </summary>
        internal readonly struct Styles
        {
            /// <summary> ENDS. Supplies existing scaled labels, popups and buttons. </summary>
            internal readonly GUIStyle Label, Popup, Button;
            /// <summary> ENDS. Supplies the existing numeric field and scaled horizontal slider styles. </summary>
            internal readonly GUIStyle Number, Slider, Thumb;

            /// <summary> ENDS. Groups the caller's cached styles without changing them or owning their textures. </summary>
            internal Styles(GUIStyle label, GUIStyle popup, GUIStyle button, GUIStyle number, GUIStyle slider, GUIStyle thumb)
            {
                Label = label;
                Popup = popup;
                Button = button;
                Number = number;
                Slider = slider;
                Thumb = thumb;
            }
        }

        /// <summary> ENDS. Carries one row's resources and existing highlight identity, contains no graph-window reference and owns no persistence or selection state. </summary>
        internal readonly struct Context
        {
            /// <summary> ENDS. Supplies the database for existing character-change Rules. </summary>
            internal readonly DialogDatabase Database;
            /// <summary> ENDS. Identifies the row's line node for catalog, clipboard and preview services. </summary>
            internal readonly DialogNode Node;
            /// <summary> ENDS. Identifies the existing ordered animation row without inventing a persistent row ID. </summary>
            internal readonly int Index;
            /// <summary> ENDS. Addresses this animation and its fields through the shared schema, no control suffix registration is required. </summary>
            internal readonly EndsNodeDrawersED.ControlAddress<CharacterActionData> Address;
            /// <summary> ENDS. Supplies node shade to the existing plate helper without assigning its ownership to child fields. </summary>
            internal readonly EndsAiHighlightED.Shade NodeShade;
            /// <summary> ENDS. Supplies viewport scale separately from unscaled drawer measurement. </summary>
            internal readonly float Scale;
            /// <summary> ENDS. Supplies cached controls for this viewport scale. </summary>
            internal readonly Styles Controls;
            /// <summary> ENDS. Supplies cached database choices through a window-independent interface. </summary>
            internal readonly ICatalog Catalog;
            /// <summary> ENDS. Supplies the host's existing editor operations without embedding their implementation in the generic drawer API. </summary>
            internal readonly IHost Host;
            /// <summary> ENDS. Defers row deletion until after list drawing, preserving indices throughout this IMGUI pass. </summary>
            internal readonly List<int> RemovedIndices;

            /// <summary> ENDS. Binds one row's immutable drawing context, referenced services remain owned by the caller. </summary>
            internal Context(DialogDatabase database, DialogNode node, int index, EndsNodeDrawersED.ControlAddress<CharacterActionData> address, EndsAiHighlightED.Shade nodeShade, float scale, Styles controls, ICatalog catalog, IHost host, List<int> removedIndices)
            {
                Database = database;
                Node = node;
                Index = index;
                Address = address;
                NodeShade = nodeShade;
                Scale = scale;
                Controls = controls;
                Catalog = catalog;
                Host = host;
                RemovedIndices = removedIndices;
            }
        }

        #endregion FIELDS AND CONTEXT

        #region DRAWER COMPOSITION

        static EndsAnimationDrawerED()
        {
            _drawers.Register(AnimationSchema, new EndsNodeDrawersED.Drawer<CharacterActionData, Context>(MeasureAnimation, DrawAnimation));

            foreach (NodeBranchDefinition<CharacterActionData, CharacterAction> branch in AnimationSchema.ActionVariant.Branches)
            {
                if (branch.Value == CharacterAction.Show) _drawers.Register(branch, new EndsNodeDrawersED.Drawer<CharacterActionData, Context>(animation => ROW_HEIGHT, DrawShow));
                else if (branch.Value == CharacterAction.Hide) _drawers.Register(branch, new EndsNodeDrawersED.Drawer<CharacterActionData, Context>(animation => 0, (bounds, animation, context) => { }));
                else throw new InvalidOperationException("Animation branch requires a drawer: " + branch.Name);
            }
        }

        /// <summary> ENDS. Exposes the root registration so graph height and rendering use one layout contract. </summary>
        internal static EndsNodeDrawersED.Drawer<CharacterActionData, Context> Drawer => _drawers.Get(AnimationSchema);

        private static float MeasureAnimation(CharacterActionData animation)
        {
            if (animation == null) return ROW_HEIGHT;

            return BOX_HEIGHT + SPACING + _drawers.Get(AnimationSchema.ActionVariant.ActiveBranch(animation)).Measure(animation);
        }

        #endregion DRAWER COMPOSITION

        #region ANIMATION DRAWING

        private static void DrawAnimation(Rect bounds, CharacterActionData animation, Context context)
        {
            if (animation == null)
            {
                GUI.Label(bounds, "Missing action data", context.Controls.Label);

                if (GUI.Button(new Rect(bounds.xMax - BUTTON_SIZE * context.Scale, bounds.y, BUTTON_SIZE * context.Scale, FIELD_HEIGHT * context.Scale), _deleteContent, context.Controls.Button) && Event.current.button == 0) context.RemovedIndices.Add(context.Index);

                return;
            }

            float padding = SPACING * context.Scale;
            Rect box = new Rect(bounds.x, bounds.y, bounds.width, bounds.height - padding);
            Layout layout = new Layout(box, context.Scale);
            EndsAiHighlightED.PaintPlate(box, context.Address.Key, context.NodeShade);
            EndsAiHighlightED.ReleaseIfAltHovered(context.Address.Key, box);

            if (DialogReferenceUtility.IsMissing(AnimationSchema.ReferenceState.Read(animation)))
            {
                string message = AnimationSchema.MissingReferenceLabel.Read(animation);

                if (string.IsNullOrEmpty(message)) message = DialogReferenceMessagesED.MISSING_SPRITE_SLOT;

                context.Host.DrawMissingReference(new Rect(box.x + padding, box.y + padding, box.width - 2 * padding, box.height - 2 * padding), message, context.Scale, context.Controls.Label);

                if (GUI.Button(layout.RemoveButton, _deleteContent, context.Controls.Button) && Event.current.button == 0) context.RemovedIndices.Add(context.Index);

                return;
            }

            GUI.Label(layout.CharacterLabel, "Character:", context.Controls.Label);

            if (context.Catalog != null && context.Catalog.HasCharacters)
            {
                string[] characters = context.Catalog.GetActionCharacters(context.Node.LineData.CharacterActions, context.Index);
                int characterIndex = Array.IndexOf(characters, AnimationSchema.Character.Read(animation));

                if (characterIndex < 0) characterIndex = 0;

                EditorGUI.BeginChangeCheck();
                string characterKey = context.Address.Field(AnimationSchema.Character);
                Color characterColor = EndsAiHighlightED.BeginField(characterKey);
                int newCharacterIndex = EditorGUI.Popup(layout.CharacterPopup, characterIndex, characters, context.Controls.Popup);
                EndsAiHighlightED.EndField(characterKey, layout.CharacterPopup, characterColor);

                if (EditorGUI.EndChangeCheck() && newCharacterIndex != characterIndex && Event.current.button == 0)
                {
                    context.Host.RecordIntent("Change Character Name in Action");
                    string selected = "";

                    if (newCharacterIndex > 0) selected = characters[newCharacterIndex];

                    EndsDialogRulesED.ChangeActionCharacter(context.Database, animation, selected);
                }

                if (!string.IsNullOrEmpty(AnimationSchema.Character.Read(animation)))
                {
                    CharacterGroup character = context.Catalog.GetCharacter(characters[newCharacterIndex]);

                    if (character != null && character.sprites.Count > 0)
                    {
                        GUI.Label(layout.EmotionLabel, "Emotion:", context.Controls.Label);
                        string[] emotions = context.Catalog.GetEmotions(character);
                        int emotionIndex = Array.IndexOf(emotions, AnimationSchema.Emotion.Read(animation));

                        if (emotionIndex < 0) emotionIndex = 0;

                        EditorGUI.BeginChangeCheck();
                        string emotionKey = context.Address.Field(AnimationSchema.Emotion);
                        Color emotionColor = EndsAiHighlightED.BeginField(emotionKey);
                        int newEmotionIndex = EditorGUI.Popup(layout.EmotionPopup, emotionIndex, emotions, context.Controls.Popup);
                        EndsAiHighlightED.EndField(emotionKey, layout.EmotionPopup, emotionColor);

                        if (EditorGUI.EndChangeCheck() && newEmotionIndex != emotionIndex && Event.current.button == 0)
                        {
                            context.Host.RecordIntent("Change Emotion Name in Action");
                            EndsNodeContentCommandsED.SetActionEmotion(animation, emotions[newEmotionIndex]);
                        }
                    }
                }
            }

            if (GUI.Button(layout.PreviewButton, _previewContent, context.Controls.Button) && Event.current.button == 0) context.Host.PreviewAnimation(animation, context.Node, context.Index);

            CharacterActionData pasteSource = context.Host.GetPasteSource(context.Node);

            if (pasteSource != null)
            {
                if (GUI.Button(layout.CopyButton, _pasteContent, context.Controls.Button) && Event.current.button == 0)
                {
                    context.Host.RecordIntent("Paste Action Parameters");
                    EndsNodeContentCommandsED.CopyAction(pasteSource, animation);
                    GUI.changed = true;
                }
            }
            else if (GUI.Button(layout.CopyButton, _copyContent, context.Controls.Button) && Event.current.button == 0) context.Host.CopyAnimation(animation, context.Node);

            if (GUI.Button(layout.RemoveButton, _deleteContent, context.Controls.Button) && Event.current.button == 0) context.RemovedIndices.Add(context.Index);

            GUI.backgroundColor = Color.white;
            GUI.Label(layout.KindLabel, "Action:", context.Controls.Label);
            EditorGUI.BeginChangeCheck();
            string kindKey = context.Address.Field(AnimationSchema.Action);
            Color kindColor = EndsAiHighlightED.BeginField(kindKey);
            CharacterAction newAction = (CharacterAction)EditorGUI.EnumPopup(layout.KindPopup, AnimationSchema.Action.Read(animation), context.Controls.Popup);
            EndsAiHighlightED.EndField(kindKey, layout.KindPopup, kindColor);

            if (EditorGUI.EndChangeCheck() && Event.current.button == 0)
            {
                context.Host.RecordIntent("Change Action in Action");
                EndsNodeContentCommandsED.SetActionKind(animation, newAction);
            }

            _drawers.Get(AnimationSchema.ActionVariant.ActiveBranch(animation)).Draw(box, animation, context);
        }

        private static void DrawShow(Rect bounds, CharacterActionData animation, Context context)
        {
            Layout layout = new Layout(bounds, context.Scale);
            DrawNumber(animation, context, AnimationSchema.Zoom, layout.ZoomLabel, layout.ZoomSlider, "Zoom:", "Change Zoom in Action", EndsDialogRulesED.ActionZoom, _setZoom);
            DrawNumber(animation, context, AnimationSchema.X, layout.XLabel, layout.XSlider, "X:", "Change X Position in Action", EndsDialogRulesED.ActionPosition, _setX);
            DrawNumber(animation, context, AnimationSchema.Y, layout.YLabel, layout.YSlider, "Y:", "Change Y Position in Action", EndsDialogRulesED.ActionPosition, _setY);
        }

        private static void DrawNumber(CharacterActionData animation, Context context, NodeFieldDefinition<CharacterActionData, float> field, Rect labelBounds, Rect bounds,
            string label, string intent, EndsDialogRulesED.NumberRange range, Action<CharacterActionData, float> setValue)
        {
            GUI.Label(labelBounds, label, context.Controls.Label);
            EditorGUI.BeginChangeCheck();
            string key = context.Address.Field(field);
            Color previousColor = EndsAiHighlightED.BeginField(key);
            float numberWidth = Mathf.Min(NUMBER_FIELD_WIDTH * context.Scale, bounds.width);
            Rect numberRect = new Rect(bounds.xMax - numberWidth, bounds.y, numberWidth, bounds.height);
            Rect sliderRect = new Rect(bounds.x, bounds.y, Mathf.Max(0, numberRect.x - SPACING * context.Scale - bounds.x), bounds.height);
            float sliderValue = field.Read(animation);

            if (sliderRect.width > 0) sliderValue = GUI.HorizontalSlider(sliderRect, sliderValue, range.Minimum, range.Maximum, context.Controls.Slider, context.Controls.Thumb);

            float numberValue = Mathf.Clamp(EditorGUI.FloatField(numberRect, sliderValue, context.Controls.Number), range.Minimum, range.Maximum);
            EndsAiHighlightED.EndField(key, bounds, previousColor);

            if (EditorGUI.EndChangeCheck() && Event.current.button == 0)
            {
                context.Host.RecordIntent(intent);
                setValue(animation, numberValue);
            }
        }

        #endregion ANIMATION DRAWING

        #region MANUAL LAYOUT

        private readonly struct Layout
        {
            /// <summary> ENDS. Keeps the original header label and popup bounds. </summary>
            internal readonly Rect CharacterLabel, CharacterPopup, EmotionLabel, EmotionPopup;
            /// <summary> ENDS. Keeps the original preview, clipboard and deletion bounds. </summary>
            internal readonly Rect PreviewButton, CopyButton, RemoveButton;
            /// <summary> ENDS. Keeps the action selector and Show-only zoom bounds. </summary>
            internal readonly Rect KindLabel, KindPopup, ZoomLabel, ZoomSlider;
            /// <summary> ENDS. Keeps the original two-cell position row. </summary>
            internal readonly Rect XLabel, XSlider, YLabel, YSlider;

            /// <summary> ENDS. Reproduces the existing scaled geometry within explicit caller bounds without reading model data. </summary>
            internal Layout(Rect bounds, float scale)
            {
                float padding = SPACING * scale;
                float left = bounds.xMin + padding;
                float right = bounds.xMax - padding;
                float height = FIELD_HEIGHT * scale;
                float headerY = bounds.yMin + padding;
                float buttonWidth = BUTTON_SIZE * scale;
                float buttonGap = BUTTON_GAP * scale;
                float buttonsWidth = HEADER_BUTTON_COUNT * buttonWidth + (HEADER_BUTTON_COUNT - 1) * buttonGap;
                float buttonsLeft = right - buttonsWidth;
                float popupWidth = (buttonsLeft - padding - left - (LABEL_WIDTH + EMOTION_LABEL_WIDTH) * scale - 2 * padding) / 2;
                CharacterLabel = new Rect(left, headerY, LABEL_WIDTH * scale, height);
                CharacterPopup = new Rect(CharacterLabel.xMax, headerY, popupWidth, height);
                EmotionLabel = new Rect(CharacterPopup.xMax + padding, headerY, EMOTION_LABEL_WIDTH * scale, height);
                EmotionPopup = new Rect(EmotionLabel.xMax + padding, headerY, popupWidth, height);
                PreviewButton = new Rect(buttonsLeft, headerY, buttonWidth, height);
                CopyButton = new Rect(PreviewButton.xMax + buttonGap, headerY, buttonWidth, height);
                RemoveButton = new Rect(CopyButton.xMax + buttonGap, headerY, buttonWidth, height);
                float kindY = headerY + ROW_HEIGHT * scale;
                KindLabel = new Rect(left, kindY, LABEL_WIDTH * scale, height);
                KindPopup = new Rect(KindLabel.xMax, kindY, POPUP_WIDTH * scale, height);
                ZoomLabel = new Rect(KindPopup.xMax + padding, kindY, ZOOM_LABEL_WIDTH * scale, height);
                ZoomSlider = new Rect(ZoomLabel.xMax, kindY, right - ZoomLabel.xMax, height);
                float positionY = kindY + ROW_HEIGHT * scale;
                float cellWidth = (right - left - padding) / 2;
                XLabel = new Rect(left, positionY, XY_LABEL_WIDTH * scale, height);
                XSlider = new Rect(XLabel.xMax, positionY, cellWidth - XLabel.width, height);
                YLabel = new Rect(left + cellWidth + padding, positionY, XY_LABEL_WIDTH * scale, height);
                YSlider = new Rect(YLabel.xMax, positionY, right - YLabel.xMax, height);
            }
        }

        #endregion MANUAL LAYOUT
    }
}
#endif
