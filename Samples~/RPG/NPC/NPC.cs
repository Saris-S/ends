using System;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Samples
{
    // NOTE: These scripts are for demonstration purposes of "Easy Node Dialog System" plugin only.
    // They do not represent best coding practices.

    [RequireComponent(typeof(DialogActivator))]
    public class NPC : MonoBehaviour
    {
        private const string PLAYER_TAG = "Player";

        public static event Action<NPC, bool> InteractionRangeChanged;
        public event Action<bool> PlayerInRangeChange;

        [Tooltip("Dialog zone on a child object (NpcInteractionTriggerRelay + trigger collider). Empty - OnTriggerEnter on this object.")][SerializeField] private NpcInteractionTriggerRelay _triggerZone;
        [SerializeField] private bool _playerInRange = false;

        private DialogActivator _dialogActivator;

        public bool PlayerInRange => _playerInRange;

        private void Awake()
        {
            _dialogActivator = GetComponent<DialogActivator>();
            if (_triggerZone != null) _triggerZone.Bind(this);
        }

        private void OnDestroy()
        {
            if (_triggerZone != null) _triggerZone.Unbind(this);
            if (_playerInRange) NotifyInteractionRangeChanged(false);
        }

        internal void SetPlayerInRange(bool inRange)
        {
            if (_playerInRange == inRange) return;

            _playerInRange = inRange;
            PlayerInRangeChange?.Invoke(inRange);
            NotifyInteractionRangeChanged(inRange);
        }

        private void NotifyInteractionRangeChanged(bool inRange)
        {
            InteractionRangeChanged?.Invoke(this, inRange);
        }

        public void InitiateDialog()
        {
            _dialogActivator.TryStartDialog();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggerZone != null) return;
            if (other.CompareTag(PLAYER_TAG)) SetPlayerInRange(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_triggerZone != null) return;
            if (other.CompareTag(PLAYER_TAG)) SetPlayerInRange(false);
        }
    }
}
