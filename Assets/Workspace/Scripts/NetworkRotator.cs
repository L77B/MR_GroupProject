using Fusion;
using UnityEngine;

public class NetworkRotator : NetworkBehaviour
{
    [Networked]
    public bool IsRotating { get; set; }

    public float rotateSpeed = 90f;

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (IsRotating)
        {
            transform.Rotate(Vector3.up, rotateSpeed * Runner.DeltaTime);
        }
    }

    private void RequestToggleRotation()
    {
        if (!Object.HasStateAuthority)
            return;

        IsRotating = !IsRotating;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ToggleRotation()
    {
        RequestToggleRotation();
    }
}