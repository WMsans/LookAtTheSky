using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class ZeroGPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveForce = 10f;
    [SerializeField] private float _jumpForce = 5f;
    [SerializeField] private float _sprintMultiplier = 1.5f;
    [SerializeField] private float _sneakMultiplier = 0.5f;
    [SerializeField] private float _friction = 5f;
    [SerializeField] private float _maxWalkSpeed = 5f;
    [SerializeField] private float _airAcceleration = 5f;

    [Header("Zero-G Settings")]
    [SerializeField] private float _accelerationForce = 5f;
    [SerializeField] private float _verticalForce = 5f;
    [SerializeField] private float _matchVelocityForce = 5f;
    [SerializeField] private float _offsetSpeed = 2f;
    [SerializeField] private float _lockRange = 100f;
    [SerializeField] private float _lockRadius = 2f;
    [SerializeField] private float _releaseRange = 200f;
    [SerializeField] private LayerMask _targetLockLayers;

    [Header("Ground Check")]
    [SerializeField] private float _groundCheckDistance = 0.2f;
    [SerializeField] private float _groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask _groundLayer;

    [Header("References")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private PlayerCamera _playerCamera;

    private Rigidbody _rb;
    private ZeroGPlayerInput _input;
    private IGravityState _currentState;
    private GroundedState _groundedState;
    private ZeroGravityState _zeroGravityState;
    private TargetLock _targetLock;
    private bool _isGravityEnabled = true;
    private float _toggleCooldown;
    private const float TOGGLE_COOLDOWN_TIME = 0.2f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _input = new ZeroGPlayerInput(GetComponent<PlayerInput>());

        _targetLock = new TargetLock(_cameraTransform, _lockRange, _lockRadius, _releaseRange, _targetLockLayers);

        _groundedState = new GroundedState(
            _cameraTransform,
            _moveForce,
            _jumpForce,
            _sprintMultiplier,
            _sneakMultiplier,
            _groundCheckDistance,
            _groundCheckRadius,
            _groundLayer,
            _friction,
            _maxWalkSpeed,
            _airAcceleration);

        _zeroGravityState = new ZeroGravityState(
            _cameraTransform,
            _targetLock,
            _accelerationForce,
            _verticalForce,
            _matchVelocityForce,
            _offsetSpeed);

        _currentState = _groundedState;
        _currentState.Enter(_rb);

        if (_playerCamera != null)
        {
            _playerCamera.Initialize(transform);
        }
    }

    private void Update()
    {
        _input.Update();
        _toggleCooldown -= Time.deltaTime;

        HandleToggleGravity();
        HandleLockTarget();
        UpdateCamera();

        if (!_isGravityEnabled)
        {
            _targetLock.CheckRelease(transform);
            _targetLock.UpdateCandidate();
        }
    }

    private void FixedUpdate()
    {
        _currentState.FixedUpdate(_rb, _input);
    }

    private void HandleToggleGravity()
    {
        if (!_input.ToggleGravityPressed || _toggleCooldown > 0) return;

        _toggleCooldown = TOGGLE_COOLDOWN_TIME;
        _currentState.Exit(_rb);

        _isGravityEnabled = !_isGravityEnabled;
        _currentState = _isGravityEnabled ? (IGravityState)_groundedState : _zeroGravityState;

        _currentState.Enter(_rb);

        if (_playerCamera != null)
        {
            _playerCamera.SetZeroGravityMode(!_isGravityEnabled);
        }
    }

    private void HandleLockTarget()
    {
        if (_isGravityEnabled) return;

        if (_input.LockTargetPressed)
        {
            if (_targetLock.IsLocked)
            {
                _targetLock.Release();
            }
            else
            {
                _targetLock.TryLock();
            }
        }
    }

    private void UpdateCamera()
    {
        if (UI.MouseManager.Instance != null && UI.MouseManager.Instance.IsCursorFree) return;

        if (_playerCamera != null)
        {
            _playerCamera.UpdateLook(_input.Look, _input.RollHeld);
        }
    }
}
