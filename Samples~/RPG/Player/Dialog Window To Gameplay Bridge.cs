using UnityEngine;

namespace ALTUSHKA.EasyNodeDialogSystem.Samples
{
    // NOTE: These scripts are for demonstration purposes of "Easy Node Dialog System" plugin only.
    // They do not represent best coding practices.

    public class DialogRunnerToGameplayBridge : MonoBehaviour
    {
        private void OnEnable()
        {
            // Subscribe on DialogRunner (source of freeze). Do not use QueryGameplayBlocked = !PlayerCanMove:
            // that skips freeze when the player is already immobile for any reason.
            DialogRunner.GameplayFreezeRequested += HandleGameplayFreezeRequested;
        }

        private void OnDisable()
        {
            DialogRunner.GameplayFreezeRequested -= HandleGameplayFreezeRequested;
        }

        private static void HandleGameplayFreezeRequested(bool freeze, bool showCursor)
        {
            PlayerState.FreezePlayer(freeze, showCursor);
        }
    }
}
