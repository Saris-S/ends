using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Samples
{
    // NOTE: These scripts are for demonstration purposes of "Easy Node Dialog System" plugin only.
    // They do not represent best coding practices.

    public class NpcInteractionPrompt : MonoBehaviour
    {
        [Tooltip("NpcInteractionTriggerRelay on the dialog zone. Empty - searched on parent or among siblings.")][SerializeField] private NpcInteractionTriggerRelay _triggerRelay;
        [Tooltip("Camera to rotate the prompt toward. Empty - MainCamera by tag.")][SerializeField] private Transform _cameraTarget;

        private bool _isPlayerInRange;

        private void Awake()
        {
            ResolveTriggerRelay();
        }

        private void OnEnable()
        {
            SubscribeToTriggerRelay();
        }

        private void OnDisable()
        {
            UnsubscribeFromTriggerRelay();
        }

        private void LateUpdate()
        {
            if (!_isPlayerInRange || _cameraTarget == null) return;

            transform.LookAt(_cameraTarget);
        }

        private void ResolveTriggerRelay()
        {
            if (_triggerRelay != null) return;

            _triggerRelay = GetComponentInParent<NpcInteractionTriggerRelay>();

            if (_triggerRelay == null && transform.parent != null) _triggerRelay = transform.parent.GetComponentInChildren<NpcInteractionTriggerRelay>();

            if (_triggerRelay == null) Debug.LogWarning($"[{name}] NpcInteractionPrompt: NpcInteractionTriggerRelay is not assigned and was not found.", this);
        }

        private void SubscribeToTriggerRelay()
        {
            if (_triggerRelay == null) return;

            _triggerRelay.PlayerInRangeChanged += OnPlayerInRangeChanged;
        }

        private void UnsubscribeFromTriggerRelay()
        {
            if (_triggerRelay == null) return;

            _triggerRelay.PlayerInRangeChanged -= OnPlayerInRangeChanged;
        }

        private void OnPlayerInRangeChanged(bool isInRange)
        {
            _isPlayerInRange = isInRange;
        }
    }
}