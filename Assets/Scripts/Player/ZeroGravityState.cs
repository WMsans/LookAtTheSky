using UnityEngine;

public class ZeroGravityState : IGravityState
{
    private readonly Transform _cameraTransform;
    private readonly TargetLock _targetLock;
    private readonly float _accelerationForce;
    private readonly float _verticalForce;
    private readonly float _matchVelocityForce;
    private readonly float _offsetSpeed;

    public ZeroGravityState(
        Transform cameraTransform,
        TargetLock targetLock,
        float accelerationForce,
        float verticalForce,
        float matchVelocityForce,
        float offsetSpeed)
    {
        _cameraTransform = cameraTransform;
        _targetLock = targetLock;
        _accelerationForce = accelerationForce;
        _verticalForce = verticalForce;
        _matchVelocityForce = matchVelocityForce;
        _offsetSpeed = offsetSpeed;
    }

    public void Enter(Rigidbody rb)
    {
        rb.useGravity = false;
        rb.linearDamping = 0.1f;
    }

    public void Exit(Rigidbody rb)
    {
        rb.useGravity = true;
        rb.linearDamping = 1f;
        _targetLock.Release();
    }

    public void FixedUpdate(Rigidbody rb, ZeroGPlayerInput input)
    {
        RotateBodyToCamera(rb);

        if (_targetLock.IsLocked)
        {
            ApplyLockedMovement(rb, input);
        }
        else
        {
            ApplyFreeMovement(rb, input);
        }
    }

    private void RotateBodyToCamera(Rigidbody rb)
    {
        Quaternion targetRotation = Quaternion.LookRotation(_cameraTransform.forward, _cameraTransform.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, 10f * Time.fixedDeltaTime));
    }

    private void ApplyFreeMovement(Rigidbody rb, ZeroGPlayerInput input)
    {
        Vector3 forward = _cameraTransform.forward;
        Vector3 right = _cameraTransform.right;
        Vector3 up = _cameraTransform.up;

        Vector2 moveInput = input.Move;
        Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;

        if (moveInput.sqrMagnitude > 0.01f)
        {
            rb.AddForce(moveDir * _accelerationForce, ForceMode.Force);
        }

        if (input.JumpHeld)
        {
            rb.AddForce(up * _verticalForce, ForceMode.Force);
        }

        if (input.SneakHeld)
        {
            rb.AddForce(-up * _verticalForce, ForceMode.Force);
        }
    }

    private void ApplyLockedMovement(Rigidbody rb, ZeroGPlayerInput input)
    {
        Vector3 targetVelocity = _targetLock.TargetVelocity;
        Vector3 relativeVelocity = rb.linearVelocity - targetVelocity;

        Vector3 desiredRelativeVelocity = Vector3.zero;

        Vector2 moveInput = input.Move;
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 forward = _cameraTransform.forward;
            Vector3 right = _cameraTransform.right;
            desiredRelativeVelocity = (forward * moveInput.y + right * moveInput.x).normalized * _offsetSpeed;
        }

        if (input.JumpHeld)
        {
            desiredRelativeVelocity += _cameraTransform.up * _offsetSpeed;
        }

        if (input.SneakHeld)
        {
            desiredRelativeVelocity -= _cameraTransform.up * _offsetSpeed;
        }

        Vector3 velocityDiff = desiredRelativeVelocity - relativeVelocity;
        rb.AddForce(velocityDiff.normalized * _matchVelocityForce, ForceMode.Force);
    }
}
