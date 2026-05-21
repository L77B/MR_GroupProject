using UnityEngine;
using Fusion;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class BatGrabAuthority : NetworkBehaviour
{
    [Networked] private Vector3     SyncPos   { get; set; }
    [Networked] private Quaternion  SyncRot   { get; set; }
    [Networked] private NetworkBool IsHeld    { get; set; }

    private HandGrabInteractable _grab;
    private NetworkTransform     _netTransform;
    private bool                 _isHeldLocally;

    public override void Spawned()
    {
        _grab         = GetComponent<HandGrabInteractable>();
        _netTransform = GetComponent<NetworkTransform>();

        if (_grab != null)
        {
            _grab.WhenSelectingInteractorViewAdded +=
                OnGrabbed;
            _grab.WhenSelectingInteractorViewRemoved +=
                OnReleased;
        }

        if (Object.HasStateAuthority)
        {
            SyncPos = transform.position;
            SyncRot = transform.rotation;
            IsHeld  = false;
        }
    }

    void OnGrabbed(IInteractorView view)
    {
        _isHeldLocally = true;
        Debug.Log("Bat grabbed locally!");

        Object.RequestStateAuthority();

        if (Object.HasStateAuthority)
            IsHeld = true;

        // Disable NetworkTransform and this script's
        // position override while held locally
        if (_netTransform != null)
            _netTransform.enabled = false;
    }

    void OnReleased(IInteractorView view)
    {
        _isHeldLocally = false;
        Debug.Log("Bat released!");

        if (Object.HasStateAuthority)
            IsHeld = false;

        if (_netTransform != null)
            _netTransform.enabled = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            // Always broadcast position
            SyncPos = transform.position;
            SyncRot = transform.rotation;
        }
        else
        {
            if (_isHeldLocally)
            {
                // This player is holding it locally
                // Let HandGrabInteractable move it freely
                // Just broadcast our position
                SyncPos = transform.position;
                SyncRot = transform.rotation;
            }
            else if (IsHeld)
            {
                // Someone else is holding it
                // Follow their position smoothly
                transform.position = Vector3.Lerp(
                    transform.position,
                    SyncPos,
                    Runner.DeltaTime * 30f);

                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    SyncRot,
                    Runner.DeltaTime * 30f);
            }
            else
            {
                // Nobody holding it
                // Follow physics/networked position
                transform.position = Vector3.Lerp(
                    transform.position,
                    SyncPos,
                    Runner.DeltaTime * 15f);

                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    SyncRot,
                    Runner.DeltaTime * 15f);
            }
        }
    }

    void OnDestroy()
    {
        if (_grab != null)
        {
            _grab.WhenSelectingInteractorViewAdded -=
                OnGrabbed;
            _grab.WhenSelectingInteractorViewRemoved -=
                OnReleased;
        }
    }
}