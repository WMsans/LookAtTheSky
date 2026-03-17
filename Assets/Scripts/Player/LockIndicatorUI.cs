using UnityEngine;

public class LockIndicatorUI : MonoBehaviour
{
    [Header("Indicator Prefabs")]
    [SerializeField] private LockIndicator _candidateIndicator;
    [SerializeField] private LockIndicator _lockedIndicator;

    [Header("References")]
    [SerializeField] private Camera _camera;

    private void OnEnable()
    {
        LockEvents.OnTargetLocked += HandleTargetLocked;
        LockEvents.OnTargetReleased += HandleTargetReleased;
        LockEvents.OnCandidateChanged += HandleCandidateChanged;
    }

    private void OnDisable()
    {
        LockEvents.OnTargetLocked -= HandleTargetLocked;
        LockEvents.OnTargetReleased -= HandleTargetReleased;
        LockEvents.OnCandidateChanged -= HandleCandidateChanged;
    }

    private void Start()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_candidateIndicator != null)
            _candidateIndicator.Initialize(_camera);

        if (_lockedIndicator != null)
            _lockedIndicator.Initialize(_camera);

        HideAll();
    }

    private void HandleTargetLocked(Collider target)
    {
        HideCandidate();
        
        if (_lockedIndicator != null && target != null)
            _lockedIndicator.Show(target, true);
    }

    private void HandleTargetReleased()
    {
        HideLocked();
    }

    private void HandleCandidateChanged(Collider candidate)
    {
        if (candidate != null)
        {
            if (_candidateIndicator != null)
                _candidateIndicator.Show(candidate, false);
        }
        else
        {
            HideCandidate();
        }
    }

    private void HideCandidate()
    {
        if (_candidateIndicator != null)
            _candidateIndicator.Hide();
    }

    private void HideLocked()
    {
        if (_lockedIndicator != null)
            _lockedIndicator.Hide();
    }

    private void HideAll()
    {
        HideCandidate();
        HideLocked();
    }
}
