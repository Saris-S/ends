#if UNITY_EDITOR
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Editor
{
    public partial class DialogEditorWindowED
    {
        #region ANIMATION HOST

        private AnimationHost _animationHost;

        private sealed class AnimationHost : EndsAnimationDrawerED.IHost
        {
            private readonly DialogEditorWindowED _window;

            /// <summary> ENDS. Adapts this window's existing editor operations to the window-independent animation drawer contract. </summary>
            internal AnimationHost(DialogEditorWindowED window)
            {
                _window = window;
            }

            /// <summary> ENDS. Records a manual graph edit through the same intent path used before drawer extraction. </summary>
            public void RecordIntent(string description) => _window.RecordEditorIntent(description);

            /// <summary> ENDS. Keeps paste disabled for the originating node and returns the existing clipboard snapshot for other nodes. </summary>
            public CharacterActionData GetPasteSource(DialogNode node)
            {
                if (copiedActionData == null || copiedActionSourceNodeId == EndsNodeRefED.Address(_window.selectedDialog, node.nodeId)) return null;

                return copiedActionData;
            }

            /// <summary> ENDS. Preserves the current node address and copies all registered animation storage into the window clipboard. </summary>
            public void CopyAnimation(CharacterActionData animation, DialogNode node)
            {
                copiedActionSourceNodeId = EndsNodeRefED.Address(_window.selectedDialog, node.nodeId);
                copiedActionData = EndsDialogRulesED.Snapshot(animation);
            }

            /// <summary> ENDS. Preserves live preview resolution by database, dialog identity and node ID instead of retaining a stale animation list. </summary>
            public void PreviewAnimation(CharacterActionData animation, DialogNode node, int index)
            {
                if (_window.database == null || string.IsNullOrEmpty(animation.CharacterName)) return;

                DialogDatabase previewDatabase = _window.database;
                string dialogKey = _window.selectedDialog.NodeIdentityKey;
                string nodeId = node.nodeId;
                EndsSpritePreviewWindowED.ShowPreview(animation, previewDatabase, node.LineData?.CharacterActions, () => _window.ReadPreviewActions(previewDatabase, dialogKey, nodeId), index);
            }

            /// <summary> ENDS. Reuses the graph's existing warning renderer and scaling for missing animation references. </summary>
            public void DrawMissingReference(Rect bounds, string message, float scale, GUIStyle labelStyle) => _window.DrawMissingReferenceWarning(bounds, message, scale, labelStyle);
        }

        #endregion ANIMATION HOST
    }
}
#endif
