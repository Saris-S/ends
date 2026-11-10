using System;
using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Samples
{
    // NOTE: These scripts are for demonstration purposes of "Easy Node Dialog System" plugin only.
    // They do not represent best coding practices.

    [RequireComponent(typeof(Collider))]
    public class NpcInteractionTriggerRelay : MonoBehaviour
    {
        private const string PLAYER_TAG = "Player";

        private NPC _npc;

        public event Action<bool> PlayerInRangeChanged;

        public void Bind(NPC npc)
        {
            _npc = npc;
        }

        public void Unbind(NPC npc)
        {
            if (_npc == npc) _npc = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_npc == null) return;
            if (!other.CompareTag(PLAYER_TAG)) return;

            _npc.SetPlayerInRange(true);
            PlayerInRangeChanged?.Invoke(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_npc == null) return;
            if (!other.CompareTag(PLAYER_TAG)) return;

            _npc.SetPlayerInRange(false);
            PlayerInRangeChanged?.Invoke(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Collider col = GetComponent<Collider>();
            if (col != null && !col.isTrigger) Debug.LogWarning($"[{name}] NpcInteractionTriggerRelay: collider must be Is Trigger.", this);
        }
#endif
    }
}