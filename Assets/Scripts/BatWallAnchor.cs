using UnityEngine;
using Fusion;

public class BatWallAnchor : NetworkBehaviour
{
    [Networked] private Vector3     AnchorPosition
        { get; set; }
    [Networked] private Quaternion  AnchorRotation
        { get; set; }
    [Networked] private NetworkBool IsAnchored
        { get; set; }

    private Rigidbody _rb;

    public override void Spawned()
    {
        _rb = GetComponent<Rigidbody>();

        if (Object.HasStateAuthority)
        {
            AnchorPosition = transform.position;
            AnchorRotation = transform.rotation;
            IsAnchored     = true;
        }

        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity  = false;
        }
    }

    public void ReleaseFromWall()
    {
        if (!Object.HasStateAuthority) return;

        IsAnchored = false;

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity  = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (IsAnchored)
        {
            transform.position = AnchorPosition;
            transform.rotation = AnchorRotation;
        }
    }
}