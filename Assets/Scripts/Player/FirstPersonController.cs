using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float jumpHeight = 1.5f;

        [Header("Mouse Look")]
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private Transform cameraTransform;

        private CharacterController _controller;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;

        private float _verticalVelocity;
        private float _cameraPitch;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            var playerInput = GetComponent<PlayerInput>();
            _moveAction = playerInput.actions["Move"];
            _lookAction = playerInput.actions["Look"];
            _jumpAction = playerInput.actions["Jump"];
        }

        private void Start()
        {
            // Cursor state is now managed by MouseManager
        }

        private void Update()
        {
            HandleLook();
            HandleMovement();
        }

        private void HandleLook()
        {
            // Don't process look when cursor is free (UI is open)
            if (UI.MouseManager.Instance != null && UI.MouseManager.Instance.IsCursorFree) return;

            Vector2 lookDelta = _lookAction.ReadValue<Vector2>();

            _cameraPitch -= lookDelta.y * mouseSensitivity;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -90f, 90f);

            cameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
            transform.Rotate(Vector3.up * lookDelta.x * mouseSensitivity);
        }

        private void HandleMovement()
        {
            Vector2 moveInput = _moveAction.ReadValue<Vector2>();

            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            move *= moveSpeed;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -2f;
                if (_jumpAction.WasPressedThisFrame())
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            move.y = _verticalVelocity;
            _controller.Move(move * Time.deltaTime);
        }
    }
}
