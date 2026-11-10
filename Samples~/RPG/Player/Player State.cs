using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ALTUSHKA.EasyNodeDialogSystem.Samples
{
    // NOTE: These scripts are for demonstration purposes of "Easy Node Dialog System" plugin only.
    // They do not represent best coding practices.

    public static class PlayerState
    {
        public const string JUMP = "JUMP_Rebinded";
        public const string SPRINT = "SPRINT_Rebinded";
        public const string MOVE_UP = "MOVE_UP_Rebinded";
        public const string MOVE_LEFT = "MOVE_LEFT_Rebinded";
        public const string MOVE_DOWN = "MOVE_DOWN_Rebinded";
        public const string MOVE_RIGHT = "MOVE_RIGHT_Rebinded";
        public const string TAKE_ITEM = "TAKE_ITEM_Rebinded";
        public const string OPEN_INVENTORY = "OPEN_INVENTORY_Rebinded";
        public const string HEAD = "HEAD_NotRebinded";
        public const string BASE_MOVE = "MOVE_NotRebinded";

        private static bool _playerCanMove = true;
        private static bool _stopCamera = false;

        public static bool PlayerCanMove
        {
            get => _playerCanMove;
            set => _playerCanMove = value;
        }

        public static bool StopCamera
        {
            get => _stopCamera;
            set => _stopCamera = value;
        }

        public static bool IsCursorLocked = false;

        public static Dictionary<string, (InputAction action, int bindingIndex)> Actions { get; private set; }

        public static void InitializeControls(PlayerControls controls)
        {
            if (controls == null) return;

            Actions = new Dictionary<string, (InputAction action, int bindingIndex)>
            {
                { JUMP, (controls.Player.Jump, -1) },
                { SPRINT, (controls.Player.Sprint, -1) },
                { BASE_MOVE, (controls.Player.Move, 0) },
                { MOVE_UP, (controls.Player.Move, 1) },
                { MOVE_LEFT, (controls.Player.Move, 2) },
                { MOVE_DOWN, (controls.Player.Move, 3) },
                { MOVE_RIGHT, (controls.Player.Move, 4) },
                { HEAD, (controls.Player.Head, -1) },
                { TAKE_ITEM, (controls.Player.TakeItem, -1) },
                { OPEN_INVENTORY, (controls.Player.OpenInventory, -1) }
            };
        }

        public static InputAction GetAction(string actionName)
        {
            if (Actions != null && Actions.TryGetValue(actionName, out var info)) return info.action;
            return null;
        }

        public static void FreezePlayer(bool freeze, bool showCursor = true)
        {
            _playerCanMove = !freeze;
            _stopCamera = freeze;

            if (freeze && showCursor) EnsureUiCursorVisible();
            else if (!freeze) SetCursorLocked(true);
        }

        public static void ToggleCursorLocked()
        {
            StopCamera = !_stopCamera;
            IsCursorLocked = !IsCursorLocked;
            SetCursorLocked(IsCursorLocked);
        }

        public static void SetCursorLocked(bool locked)
        {
            IsCursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public static void EnsureUiCursorVisible()
        {
            IsCursorLocked = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
