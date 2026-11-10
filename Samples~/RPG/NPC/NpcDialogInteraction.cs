using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Samples
{
    // NOTE: These scripts are for demonstration purposes of "Easy Node Dialog System" plugin only.
    // They do not represent best coding practices.

    public class NpcDialogInteraction : MonoBehaviour
    {
        public static NpcDialogInteraction Instance { get; private set; }
        [SerializeField] private KeyCode _interactionKey = KeyCode.E;
        private NPC _focusedNpc;
        public KeyCode InteractionKey => _interactionKey;
        
        private void OnEnable()
        {
            NPC.InteractionRangeChanged += HandleNpcInteractionRangeChanged;
        }

        private void OnDisable()
        {
            NPC.InteractionRangeChanged -= HandleNpcInteractionRangeChanged;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(_interactionKey)) return;
            if (_focusedNpc == null) return;
            if (DialogRunner.Instance == null) return;
            if (DialogRunner.Instance.IsTalking) return;

            _focusedNpc.InitiateDialog();
        }

        private void HandleNpcInteractionRangeChanged(NPC npc, bool inRange)
        {
            if (npc == null) return;

            if (inRange) _focusedNpc = npc;
            else if (_focusedNpc == npc) _focusedNpc = null;
        }
    }
}
