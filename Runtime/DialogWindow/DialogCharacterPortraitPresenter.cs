using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ALTUSHKA.EasyNodeDialogSystem
{
    /// <summary>Optional VN-style character sprites from LineReady. CharacterActions. Not required for text-only RPG dialogs.</summary>
    public sealed class DialogCharacterPortraitPresenter : MonoBehaviour
    {
        #region Developer API

            #region General - portrait layer

                /// <summary>Removes all character portraits from the container.</summary>
                public void ClearAll() => ClearAllImpl();

            #endregion General - portrait layer

        #endregion Developer API

        private const float CHARACTER_LOCAL_SCALE_Z = 1f;

        [Tooltip("Parent for instantiated character portraits. Required when this component is used.")][SerializeField] private RectTransform _charactersContainer;
        [Tooltip("Portrait slot prefab with DialogCharacterPortraitSlotUI. Required when this component is used.")][SerializeField] private DialogCharacterPortraitSlotUI _characterPortraitSlotPrefab;

        private readonly Dictionary<string, DialogCharacterPortraitSlotUI> _characterSlots = new Dictionary<string, DialogCharacterPortraitSlotUI>();
        private bool _started;
        private DialogRunner _runner;

        private void Start()
        {
            _runner = DialogRunner.Instance;
            if (_runner == null) throw new InvalidOperationException("DialogCharacterPortraitPresenter: DialogRunner.Instance is missing - place DialogRunner in the scene.");
            if (_charactersContainer == null) throw new InvalidOperationException("DialogCharacterPortraitPresenter: Characters Container is not assigned in the Inspector.");
            if (_characterPortraitSlotPrefab == null) throw new InvalidOperationException("DialogCharacterPortraitPresenter: Character Portrait Slot Prefab is not assigned in the Inspector.");
            if (_characterPortraitSlotPrefab.Image == null) throw new InvalidOperationException("DialogCharacterPortraitPresenter: portrait slot prefab has no Image assigned.");
            if (_characterPortraitSlotPrefab.RectTransform == null) throw new InvalidOperationException("DialogCharacterPortraitPresenter: portrait slot prefab has no RectTransform assigned.");

            UnsubscribeRunner();
            SubscribeRunner();
            _started = true;
        }

        private void OnEnable()
        {
            if (!_started) return;

            UnsubscribeRunner();
            SubscribeRunner();
        }

        private void OnDisable()
        {
            if (!_started) return;

            UnsubscribeRunner();
        }

        private void SubscribeRunner()
        {
            _runner.LineReady += HandleLineReady;
            _runner.DialogEnded += HandleSessionClosed;
            _runner.DialogCancelled += HandleSessionClosed;
        }

        private void UnsubscribeRunner()
        {
            _runner.LineReady -= HandleLineReady;
            _runner.DialogEnded -= HandleSessionClosed;
            _runner.DialogCancelled -= HandleSessionClosed;
        }

        private void HandleLineReady(DialogLinePresentation line)
        {
            IReadOnlyList<CharacterActionData> actions = line.CharacterActions;

            if (actions == null) return;

            for (int i = 0; i < actions.Count; i++)
            {
                CharacterActionData action = actions[i];

                if (action == null)
                {
                    Debug.LogWarning("DialogCharacterPortraitPresenter: CharacterActions contains a null entry - skipped.", this);
                    continue;
                }

                if (action.Action == CharacterAction.Show)
                {
                    if (DialogReferenceUtility.IsMissing(action.ReferenceState))
                    {
                        Debug.LogWarning($"DialogCharacterPortraitPresenter: Show skipped - removed sprite ({action.MissingReferenceLabel}).", this);
                        continue;
                    }

                    if (!TryShowCharacter(action.CharacterName, action.EmotionName, action.PosX, action.PosY, action.Zoom)) continue;
                }
                else if (action.Action == CharacterAction.Hide)
                {
                    if (DialogReferenceUtility.IsMissing(action.ReferenceState))
                    {
                        Debug.LogWarning($"DialogCharacterPortraitPresenter: Hide skipped - removed character ({action.MissingReferenceLabel}).", this);
                        continue;
                    }

                    HideCharacter(action.CharacterName);
                }
            }
        }

        private void HandleSessionClosed(DialogSessionInfo info)
        {
            ClearAllImpl();
        }

        private bool TryShowCharacter(string characterName, string emotionName, float xPos, float yPos, float zoom)
        {
            if (string.IsNullOrEmpty(characterName) || string.IsNullOrEmpty(emotionName))
            {
                Debug.LogWarning("DialogCharacterPortraitPresenter: Show skipped - characterName or emotionName is empty.", this);
                return false;
            }

            DialogDatabase database = _runner.DialogDatabase;

            if (database == null)
            {
                Debug.LogWarning("DialogCharacterPortraitPresenter: Show skipped - DialogRunner has no DialogDatabase.", this);
                return false;
            }

            Sprite newSprite = database.GetSprite(characterName, emotionName);

            if (newSprite == null)
            {
                Debug.LogWarning($"DialogCharacterPortraitPresenter: Show skipped - sprite not found for '{characterName}' / '{emotionName}'.", this);
                return false;
            }

            if (_characterSlots.TryGetValue(characterName, out DialogCharacterPortraitSlotUI existingSlot))
            {
                Image characterImage = existingSlot.Image;
                characterImage.sprite = newSprite;

                RectTransform rectTransform = existingSlot.RectTransform;
                rectTransform.anchoredPosition = new Vector2(xPos, yPos);
                rectTransform.localScale = new Vector3(zoom, zoom, CHARACTER_LOCAL_SCALE_Z);
            }
            else
            {
                DialogCharacterPortraitSlotUI newSlot = Instantiate(_characterPortraitSlotPrefab, _charactersContainer);
                Image characterImage = newSlot.Image;
                characterImage.sprite = newSprite;
                characterImage.type = Image.Type.Simple;
                characterImage.preserveAspect = true;

                RectTransform rectTransform = newSlot.RectTransform;
                rectTransform.anchoredPosition = new Vector2(xPos, yPos);
                rectTransform.localScale = new Vector3(zoom, zoom, CHARACTER_LOCAL_SCALE_Z);

                _characterSlots[characterName] = newSlot;
            }

            return true;
        }

        private void HideCharacter(string characterName)
        {
            if (!_characterSlots.TryGetValue(characterName, out DialogCharacterPortraitSlotUI slot)) return;

            Destroy(slot.gameObject);
            _characterSlots.Remove(characterName);
        }

        private void ClearAllImpl()
        {
            foreach (DialogCharacterPortraitSlotUI slot in _characterSlots.Values)
            {
                Destroy(slot.gameObject);
            }

            _characterSlots.Clear();
        }
    }
}
