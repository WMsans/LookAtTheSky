using UnityEngine;
using UnityEngine.InputSystem;

public class ZeroGPlayerInput
{
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _sprintAction;
    private InputAction _crouchAction;
    private InputAction _jumpAction;
    private InputAction _toggleGravityAction;
    private InputAction _lockTargetAction;
    private InputAction _rollAction;

    private bool _jumpBuffer;
    private bool _toggleGravityBuffer;
    private bool _lockTargetBuffer;

    public Vector2 Move => _moveAction.ReadValue<Vector2>();
    public Vector2 Look => _lookAction.ReadValue<Vector2>();
    public bool SprintHeld => _sprintAction.IsPressed();
    public bool SneakHeld => _crouchAction.IsPressed();
    public bool JumpHeld => _jumpAction.IsPressed();
    public bool RollHeld => _rollAction.IsPressed();

    public bool JumpPressed
    {
        get
        {
            if (_jumpBuffer)
            {
                _jumpBuffer = false;
                return true;
            }
            return false;
        }
    }

    public bool ToggleGravityPressed
    {
        get
        {
            if (_toggleGravityBuffer)
            {
                _toggleGravityBuffer = false;
                return true;
            }
            return false;
        }
    }

    public bool LockTargetPressed
    {
        get
        {
            if (_lockTargetBuffer)
            {
                _lockTargetBuffer = false;
                return true;
            }
            return false;
        }
    }

    public ZeroGPlayerInput(PlayerInput playerInput)
    {
        _moveAction = playerInput.actions["Move"];
        _lookAction = playerInput.actions["Look"];
        _sprintAction = playerInput.actions["Sprint"];
        _crouchAction = playerInput.actions["Crouch"];
        _jumpAction = playerInput.actions["Jump"];
        _toggleGravityAction = playerInput.actions["ToggleGravity"];
        _lockTargetAction = playerInput.actions["LockTarget"];
        _rollAction = playerInput.actions["Roll"];
    }

    public void Update()
    {
        if (_jumpAction.WasPressedThisFrame()) _jumpBuffer = true;
        if (_toggleGravityAction.WasPressedThisFrame()) _toggleGravityBuffer = true;
        if (_lockTargetAction.WasPressedThisFrame()) _lockTargetBuffer = true;
    }
}
