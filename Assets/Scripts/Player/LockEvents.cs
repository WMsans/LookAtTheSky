using System;
using UnityEngine;

public static class LockEvents
{
    public static event Action<Collider> OnTargetLocked;
    public static event Action OnTargetReleased;
    public static event Action<Collider> OnCandidateChanged;

    public static void TargetLocked(Collider target) => OnTargetLocked?.Invoke(target);
    public static void TargetReleased() => OnTargetReleased?.Invoke();
    public static void CandidateChanged(Collider candidate) => OnCandidateChanged?.Invoke(candidate);
}
