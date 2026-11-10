using UnityEngine;
using UnityEngine.InputSystem;

namespace ALTUSHKA.EasyNodeDialogSystem.Samples
{
    // NOTE: These scripts are for demonstration purposes of "Easy Node Dialog System" plugin only.
    // They do not represent best coding practices.

    [DefaultExecutionOrder(-100)]
    public class PlayerController : MonoBehaviour
    {
        private PlayerControls _controls;

        [Header("Camera")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float mouseSensitivity = 0.3f;
        [SerializeField] private float verticalClamp = 60f;
        [SerializeField] private float verticalRotation = 0f;
        [SerializeField] private Vector2 cameraInput;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 2f;
        [SerializeField] private float _sprintSpeed = 3.5f;
        [SerializeField] bool _isSprinting;

        [SerializeField] Vector2 _rawInput;
        private Vector2 _smoothedInput;
        [SerializeField] private float acceleration = 8f;
        private Vector3 _movement;

        [Header("Jump")]
        [SerializeField] private float jumpForce = 5f;
        [SerializeField] private float groundCheckRadius = 0.28f;
        [SerializeField] private float groundCheckDistance = 0.18f;
        [SerializeField] private float groundNormalMinY = 0.5f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Misc")]
        [SerializeField] private Rigidbody rb;

        private const float INPUT_THRESHOLD = 0.1f;
        private const float GROUND_CAST_LIFT = 0.05f;

        private bool _isGrounded;
        private float _lastGroundedTime = -999f;
        private bool _jumpRequested;

        private void Awake()
        {
            _controls = new PlayerControls();
            _controls.Enable();
            PlayerState.InitializeControls(_controls);
        }

        private void Start()
        {
            PlayerState.PlayerCanMove = true;
            PlayerState.StopCamera = false;
            PlayerState.SetCursorLocked(true);

            LoadBindings();
        }

        private void OnEnable()
        {
            PlayerState.StopCamera = false;
            PlayerState.IsCursorLocked = true;
            PlayerState.SetCursorLocked(true);

            Subscribe(PlayerState.JUMP, OnJump);
            SubscribeMove(PlayerState.BASE_MOVE, OnBaseMovePerformed, OnBaseMoveCanceled);
            Subscribe(PlayerState.SPRINT, OnSprintPerformed, OnSprintCanceled);
            Subscribe(PlayerState.HEAD, OnHeadPerformed, OnHeadCanceled);
        }

        private void OnDisable()
        {
            Unsubscribe(PlayerState.JUMP, OnJump);
            UnsubscribeMove(PlayerState.BASE_MOVE, OnBaseMovePerformed, OnBaseMoveCanceled);
            Unsubscribe(PlayerState.SPRINT, OnSprintPerformed, OnSprintCanceled);
            Unsubscribe(PlayerState.HEAD, OnHeadPerformed, OnHeadCanceled);
        }

        private void OnDestroy()
        {
            if (_controls != null) _controls.Disable();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame) PlayerState.ToggleCursorLocked(); //Just my custom key to toggle cursor locked
#endif
        }

        private void LateUpdate()
        {
            if (PlayerState.StopCamera)
            {
                cameraInput = Vector2.zero;

                return;
            }

            Vector2 look = cameraInput;
            cameraInput = Vector2.zero;

            if (look.sqrMagnitude <= 0f) return;

            transform.Rotate(0f, look.x * mouseSensitivity, 0f);
            verticalRotation -= look.y * mouseSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, -verticalClamp, verticalClamp);
            cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }

        private void FixedUpdate()
        {
            UpdateGroundedState();

            if (PlayerState.PlayerCanMove)
            {
                _smoothedInput.x = Mathf.MoveTowards(_smoothedInput.x, _rawInput.x, acceleration * Time.fixedDeltaTime);
                _smoothedInput.y = Mathf.MoveTowards(_smoothedInput.y, _rawInput.y, acceleration * Time.fixedDeltaTime);

                float walkOrSprint;

                if (_isSprinting && _smoothedInput.y > INPUT_THRESHOLD) walkOrSprint = _sprintSpeed;
                else walkOrSprint = _moveSpeed;

                Vector3 direction = transform.forward * _smoothedInput.y + transform.right * _smoothedInput.x;
                _movement = direction.normalized * walkOrSprint;
            }

            if (_jumpRequested)
            {
                TryJump();
                _jumpRequested = false;
            }

            if (PlayerState.PlayerCanMove)
            {
                _movement.y = rb.linearVelocity.y;
                rb.linearVelocity = _movement;
            }
            else rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }

        private void UpdateGroundedState()
        {
            _isGrounded = false;

            Vector3 origin = rb.position + Vector3.up * (groundCheckRadius + GROUND_CAST_LIFT);
            float castDistance = groundCheckDistance + GROUND_CAST_LIFT;
            RaycastHit[] hits = Physics.SphereCastAll(origin, groundCheckRadius, Vector3.down, castDistance, groundLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null) continue;
                if (hit.collider.attachedRigidbody == rb) continue;
                if (hit.collider.transform == transform) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.normal.y < groundNormalMinY) continue;

                _isGrounded = true;
                break;
            }

            if (_isGrounded) _lastGroundedTime = Time.time;
        }

        private void TryJump()
        {
            if (!PlayerState.PlayerCanMove) return;

            bool withinCoyote = Time.time - _lastGroundedTime <= coyoteTime;
            if (!_isGrounded && !withinCoyote) return;

            Vector3 velocity = rb.linearVelocity;
            if (velocity.y < 0f) velocity.y = 0f;
            rb.linearVelocity = velocity;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            _isGrounded = false;
            _lastGroundedTime = -999f;
        }

        private void LoadBindings()
        {
            if (_controls == null) return;

            string bindings = PlayerPrefs.GetString("InputBindings", "");

            if (!string.IsNullOrEmpty(bindings))
            {
                _controls.LoadBindingOverridesFromJson(bindings);
                Debug.Log("Bindings loaded in PlayerController!", this);
            }
        }

        private static void Subscribe(string actionName, System.Action<InputAction.CallbackContext> handler)
        {
            var action = PlayerState.GetAction(actionName);

            if (action != null) action.started += handler;
        }

        private static void Unsubscribe(string actionName, System.Action<InputAction.CallbackContext> handler)
        {
            var action = PlayerState.GetAction(actionName);

            if (action != null) action.started -= handler;
        }

        private static void SubscribeMove(string actionName, System.Action<InputAction.CallbackContext> performed, System.Action<InputAction.CallbackContext> canceled)
        {
            var action = PlayerState.GetAction(actionName);

            if (action == null) return;

            action.performed += performed;
            action.canceled += canceled;
        }

        private static void UnsubscribeMove(string actionName, System.Action<InputAction.CallbackContext> performed, System.Action<InputAction.CallbackContext> canceled)
        {
            var action = PlayerState.GetAction(actionName);

            if (action == null) return;

            action.performed -= performed;
            action.canceled -= canceled;
        }

        private static void Subscribe(string actionName, System.Action<InputAction.CallbackContext> performed, System.Action<InputAction.CallbackContext> canceled)
        {
            var action = PlayerState.GetAction(actionName);

            if (action == null) return;

            action.performed += performed;
            action.canceled += canceled;
        }

        private static void Unsubscribe(string actionName, System.Action<InputAction.CallbackContext> performed, System.Action<InputAction.CallbackContext> canceled)
        {
            var action = PlayerState.GetAction(actionName);
            if (action == null) return;
            action.performed -= performed;
            action.canceled -= canceled;
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            if (!PlayerState.PlayerCanMove) return;
            _jumpRequested = true;
        }

        private void OnBaseMovePerformed(InputAction.CallbackContext ctx)
        {
            _rawInput = ctx.ReadValue<Vector2>();
        }

        private void OnBaseMoveCanceled(InputAction.CallbackContext ctx)
        {
            _rawInput = Vector2.zero;
        }

        private void OnSprintPerformed(InputAction.CallbackContext ctx)
        {
            _isSprinting = true;
        }

        private void OnSprintCanceled(InputAction.CallbackContext ctx)
        {
            _isSprinting = false;
        }

        private void OnHeadPerformed(InputAction.CallbackContext ctx)
        {
            cameraInput += ctx.ReadValue<Vector2>();
        }

        private void OnHeadCanceled(InputAction.CallbackContext ctx)
        {
            // Look is consumed in LateUpdate; do not clear mid-frame on canceled.
        }
    }
}
