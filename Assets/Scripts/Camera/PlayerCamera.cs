using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private float _sensitivity = 2f;
    [SerializeField] private float _groundedVerticalClamp = 80f;

    private Transform _playerBody;
    private Rigidbody _playerRb;
    private float _xRotation;
    private float _yRotation;
    private float _zRotation;
    private bool _isZeroGravity;

    public void Initialize(Transform playerBody)
    {
        _playerBody = playerBody;
        _playerRb = playerBody.GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetZeroGravityMode(bool isZeroGravity)
    {
        _isZeroGravity = isZeroGravity;
    }

    public void UpdateLook(Vector2 lookInput, bool rollHeld = false)
    {
        if (lookInput.sqrMagnitude < 0.01f) return;

        float mouseX = lookInput.x * _sensitivity;
        float mouseY = lookInput.y * _sensitivity;

        if (_isZeroGravity)
        {
            if (rollHeld)
            {
                _zRotation -= mouseX;
                _zRotation = Mathf.Repeat(_zRotation + 180f, 360f) - 180f;
            }
            else
            {
                _yRotation += mouseX;
                _yRotation = Mathf.Repeat(_yRotation + 180f, 360f) - 180f;
            }

            _xRotation -= mouseY;
            _xRotation = Mathf.Repeat(_xRotation + 180f, 360f) - 180f;

            Quaternion targetRotation = Quaternion.Euler(_xRotation, _yRotation, _zRotation);
            _playerRb.MoveRotation(targetRotation);
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            _xRotation -= mouseY;
            _xRotation = Mathf.Clamp(_xRotation, -_groundedVerticalClamp, _groundedVerticalClamp);

            transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
            _playerBody.Rotate(Vector3.up * mouseX);
        }
    }
}
