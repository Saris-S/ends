#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    /// <summary>Sprite preview window on a 1920x1080 canvas.</summary>
    public class EndsSpritePreviewWindowED : EditorWindow
    {
        private const int EDITOR_SPACE_SMALL = 5;
        private const int MIN_WIDTH = 500;
        private const int MIN_HEIGHT = 350;
        private const int PREVIEW_MODE_BUTTON_WIDTH = 72;
        private const int PREVIEW_MODE_BUTTON_HEIGHT = 22;
        private CharacterActionData _actionRef;
        private IReadOnlyList<CharacterActionData> _nodeActions;
        private Func<IReadOnlyList<CharacterActionData>> _actionSource;
        private int _actionIndex = -1;
        private DialogDatabase _charDB;
        private Sprite _cachedSprite;
        private string _lastCharName;
        private string _lastEmotionName;
        private bool _showAllNodeSprites;
        private const float REF_WIDTH = 1920f;
        private const float REF_HEIGHT = 1080f;

        /// <summary> Opens the selected character action and optionally all sprites on its node. </summary>
        public static void ShowPreview(CharacterActionData actionData, DialogDatabase charDB, IReadOnlyList<CharacterActionData> nodeActions = null, Func<IReadOnlyList<CharacterActionData>> actionSource = null, int actionIndex = -1)
        {
            var window = GetWindow<EndsSpritePreviewWindowED>(false, "Sprite preview", true);
            window._actionRef = actionData;
            window._nodeActions = nodeActions;
            window._actionSource = actionSource;
            window._actionIndex = actionIndex;
            window._charDB = charDB;
            window._cachedSprite = null;
            window._showAllNodeSprites = false;
            window.minSize = new Vector2(MIN_WIDTH, MIN_HEIGHT);
            window.Show();
            window.Repaint();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EndsSessionED.StateChanged += HandleDatabaseChanged;
            EditorApplication.projectChanged += HandlePreviewChanged;
            Undo.undoRedoPerformed += HandlePreviewChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EndsSessionED.StateChanged -= HandleDatabaseChanged;
            EditorApplication.projectChanged -= HandlePreviewChanged;
            Undo.undoRedoPerformed -= HandlePreviewChanged;
        }

        private void OnEditorUpdate()
        {
            RefreshActionBinding();
            Repaint();
        }

        private void RefreshActionBinding()
        {
            if (_actionSource == null) return;

            _nodeActions = _actionSource();
            _actionRef = null;

            if (_nodeActions != null && _actionIndex >= 0 && _actionIndex < _nodeActions.Count) _actionRef = _nodeActions[_actionIndex];
        }

        private void HandleDatabaseChanged(DialogDatabase changedDatabase)
        {
            if (changedDatabase == _charDB) HandlePreviewChanged();
        }

        private void HandlePreviewChanged()
        {
            _cachedSprite = null;
            Repaint();
        }

        private void RefreshSprite()
        {
            if (_actionRef == null || _charDB == null) { _cachedSprite = null; return; }
            if (_cachedSprite == null || _lastCharName != _actionRef.CharacterName || _lastEmotionName != _actionRef.EmotionName)
            {
                _lastCharName = _actionRef.CharacterName;
                _lastEmotionName = _actionRef.EmotionName;
                _cachedSprite = _charDB.GetSprite(_actionRef.CharacterName, _actionRef.EmotionName);
            }
        }

        private int CountPreviewableNodeSprites()
        {
            if (_nodeActions == null) return 0;

            int count = 0;

            for (int i = 0; i < _nodeActions.Count; i++)
            {
                CharacterActionData action = _nodeActions[i];

                if (!IsPreviewableShowAction(action)) continue;

                count++;
            }

            return count;
        }

        private bool IsPreviewableShowAction(CharacterActionData action)
        {
            if (action == null || action.Action != CharacterAction.Show) return false;
            if (DialogReferenceUtility.IsMissing(action.ReferenceState)) return false;
            if (string.IsNullOrEmpty(action.CharacterName) || string.IsNullOrEmpty(action.EmotionName)) return false;
            if (_charDB == null) return false;

            return _charDB.GetSprite(action.CharacterName, action.EmotionName) != null;
        }

        private static void DrawScreenFrame(Rect screenRect)
        {
            Color borderColor = new Color(0.5f, 0.5f, 0.5f);
            EditorGUI.DrawRect(new Rect(screenRect.x, screenRect.y, screenRect.width, 1), borderColor);
            EditorGUI.DrawRect(new Rect(screenRect.x, screenRect.yMax - 1, screenRect.width, 1), borderColor);
            EditorGUI.DrawRect(new Rect(screenRect.x, screenRect.y, 1, screenRect.height), borderColor);
            EditorGUI.DrawRect(new Rect(screenRect.xMax - 1, screenRect.y, 1, screenRect.height), borderColor);

            Color crossColor = new Color(1f, 1f, 1f, 0.15f);
            float cx = screenRect.x + screenRect.width * 0.5f;
            float cy = screenRect.y + screenRect.height * 0.5f;
            EditorGUI.DrawRect(new Rect(cx, screenRect.y, 1, screenRect.height), crossColor);
            EditorGUI.DrawRect(new Rect(screenRect.x, cy, screenRect.width, 1), crossColor);
        }

        private void DrawActionSpriteOnScreen(Rect screenRect, float scale, CharacterActionData action, Sprite sprite)
        {
            if (action == null) return;

            Texture2D texture = GetPreviewTexture(sprite, out Rect textureCoordinates);

            if (texture == null) return;

            float spriteNativeW = sprite.rect.width;
            float spriteNativeH = sprite.rect.height;
            float spriteAspect = spriteNativeW / spriteNativeH;
            float rectAspect = REF_WIDTH / REF_HEIGHT;
            float fitW;
            float fitH;

            if (spriteAspect > rectAspect)
            {
                fitW = REF_WIDTH;
                fitH = REF_WIDTH / spriteAspect;
            }
            else
            {
                fitH = REF_HEIGHT;
                fitW = REF_HEIGHT * spriteAspect;
            }

            float spriteW = fitW * action.Zoom * scale;
            float spriteH = fitH * action.Zoom * scale;
            float centerX = screenRect.x + screenRect.width * 0.5f + action.PosX * scale;
            float centerY = screenRect.y + screenRect.height * 0.5f - action.PosY * scale;
            Rect spriteRect = new Rect(centerX - spriteW * 0.5f, centerY - spriteH * 0.5f, spriteW, spriteH);
            GUI.DrawTextureWithTexCoords(spriteRect, texture, textureCoordinates, true);
        }

        /// <summary> ENDS. Uses the original editor texture and sprite rectangle so atlas packing, trimming and rotation cannot select neighboring atlas sprites. </summary>
        internal static Texture2D GetPreviewTexture(Sprite sprite, out Rect textureCoordinates)
        {
            textureCoordinates = default;

            if (sprite == null) return null;

            Texture2D texture = UnityEditor.Sprites.SpriteUtility.GetSpriteTexture(sprite, false);

            if (texture == null) return null;

            Rect rectangle = sprite.rect;
            textureCoordinates = new Rect(rectangle.x / texture.width, rectangle.y / texture.height, rectangle.width / texture.width, rectangle.height / texture.height);

            return texture;
        }

        private void OnGUI()
        {
            RefreshActionBinding();

            if (_actionRef == null)
            {
                EditorGUILayout.HelpBox("No action selected. Click the eye icon on the sprite row.", MessageType.Info);
                return;
            }

            RefreshSprite();

            int previewableCount = CountPreviewableNodeSprites();
            bool showAllMode = _showAllNodeSprites && previewableCount >= 2;
            float spriteNativeW = 0f;
            float spriteNativeH = 0f;

            if (_cachedSprite != null)
            {
                spriteNativeW = _cachedSprite.rect.width;
                spriteNativeH = _cachedSprite.rect.height;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Character: {_actionRef.CharacterName}  |  Emotion: {_actionRef.EmotionName}", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

            if (previewableCount >= 2)
            {
                GUIContent modeButtonContent;

                if (_showAllNodeSprites) modeButtonContent = new GUIContent("Show solo", "Show only the selected sprite");
                else modeButtonContent = new GUIContent("Show all", "Show all sprites on the node");

                if (GUILayout.Button(modeButtonContent, GUILayout.Width(PREVIEW_MODE_BUTTON_WIDTH), GUILayout.Height(PREVIEW_MODE_BUTTON_HEIGHT))) _showAllNodeSprites = !_showAllNodeSprites;
            }

            EditorGUILayout.EndHorizontal();

            string spriteSizeText = "-";

            if (_cachedSprite != null) spriteSizeText = $"{spriteNativeW:F0}×{spriteNativeH:F0}px";

            EditorGUILayout.LabelField($"X: {_actionRef.PosX:F0}  Y: {_actionRef.PosY:F0}  Zoom: {_actionRef.Zoom:F2}  |  Sprite: {spriteSizeText}");
            EditorGUILayout.Space(EDITOR_SPACE_SMALL);

            if (!showAllMode && GetPreviewTexture(_cachedSprite, out _) == null)
            {
                EditorGUILayout.HelpBox("Sprite is not assigned or missing.", MessageType.Warning);
                return;
            }

            Rect previewArea = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (previewArea.width < 10 || previewArea.height < 10) return;

            EditorGUI.DrawRect(previewArea, new Color(0.12f, 0.12f, 0.12f));
            float areaAspect = previewArea.width / previewArea.height;
            float refAspect = REF_WIDTH / REF_HEIGHT;
            float drawW;
            float drawH;

            if (areaAspect > refAspect)
            {
                drawH = previewArea.height;
                drawW = drawH * refAspect;
            }
            else
            {
                drawW = previewArea.width;
                drawH = drawW / refAspect;
            }

            float screenX = previewArea.x + (previewArea.width - drawW) * 0.5f;
            float screenY = previewArea.y + (previewArea.height - drawH) * 0.5f;
            Rect screenRect = new Rect(screenX, screenY, drawW, drawH);
            EditorGUI.DrawRect(screenRect, new Color(0.18f, 0.18f, 0.22f));
            float scale = drawW / REF_WIDTH;

            if (showAllMode)
            {
                for (int i = 0; i < _nodeActions.Count; i++)
                {
                    CharacterActionData action = _nodeActions[i];

                    if (!IsPreviewableShowAction(action)) continue;

                    Sprite sprite = _charDB.GetSprite(action.CharacterName, action.EmotionName);
                    DrawActionSpriteOnScreen(screenRect, scale, action, sprite);
                }
            }
            else DrawActionSpriteOnScreen(screenRect, scale, _actionRef, _cachedSprite);

            DrawScreenFrame(screenRect);
        }
    }
}
#endif