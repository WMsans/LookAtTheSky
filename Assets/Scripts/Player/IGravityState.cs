using UnityEngine;

public interface IGravityState
{
    void Enter(Rigidbody rb);
    void Exit(Rigidbody rb);
    void FixedUpdate(Rigidbody rb, ZeroGPlayerInput input);
}
