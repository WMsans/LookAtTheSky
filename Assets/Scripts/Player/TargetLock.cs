using UnityEngine;

public class TargetLock
{
    private readonly Transform _cameraTransform;
    private readonly float _lockRange;
    private readonly float _lockRadius;
    private readonly float _releaseRange;

    private Collider _lockedTarget;
    private Collider _currentCandidate;

    public bool IsLocked => _lockedTarget != null;
    public Collider CurrentCandidate => _currentCandidate;
    public Collider Target => _lockedTarget;
    public Vector3 TargetVelocity => GetTargetVelocity();

    public TargetLock(Transform cameraTransform, float lockRange, float lockRadius, float releaseRange)
    {
        _cameraTransform = cameraTransform;
        _lockRange = lockRange;
        _lockRadius = lockRadius;
        _releaseRange = releaseRange;
    }

    public void UpdateCandidate()
    {
        if (_lockedTarget != null)
        {
            _currentCandidate = null;
            return;
        }

        if (Physics.SphereCast(
            _cameraTransform.position,
            _lockRadius,
            _cameraTransform.forward,
            out RaycastHit hit,
            _lockRange))
        {
            if (_currentCandidate != hit.collider)
            {
                _currentCandidate = hit.collider;
                LockEvents.CandidateChanged(_currentCandidate);
            }
        }
        else if (_currentCandidate != null)
        {
            _currentCandidate = null;
            LockEvents.CandidateChanged(null);
        }
    }

    public void TryLock()
    {
        if (Physics.SphereCast(
            _cameraTransform.position,
            _lockRadius,
            _cameraTransform.forward,
            out RaycastHit hit,
            _lockRange))
        {
            _lockedTarget = hit.collider;
            _currentCandidate = null;
            LockEvents.TargetLocked(_lockedTarget);
            LockEvents.CandidateChanged(null);
        }
    }

    public void Release()
    {
        _lockedTarget = null;
        LockEvents.TargetReleased();
    }

    public void CheckRelease(Transform playerTransform)
    {
        if (_lockedTarget == null) return;

        if (_lockedTarget == null || !_lockedTarget.gameObject.activeInHierarchy)
        {
            Release();
            return;
        }

        float distance = Vector3.Distance(playerTransform.position, _lockedTarget.transform.position);
        if (distance > _releaseRange)
        {
            Release();
        }
    }

    private Vector3 GetTargetVelocity()
    {
        if (_lockedTarget == null) return Vector3.zero;

        Rigidbody targetRb = _lockedTarget.attachedRigidbody;
        if (targetRb != null)
        {
            return targetRb.linearVelocity;
        }

        return Vector3.zero;
    }
}
