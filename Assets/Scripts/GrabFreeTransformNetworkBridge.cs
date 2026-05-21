using UnityEngine;
using Fusion;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class GrabFreeTransformerNetworkBridge : NetworkBehaviour
{
    [Networked] private Vector3     SyncPos  { get; set; }
    [Networked] private Quaternion  SyncRot  { get; set; }
    [Networked] private NetworkBool IsGrabbed { get; set; }

    private HandGrabInteractable _handGrab;
    private GrabFreeTransformer  _transformer;
    private Rigidbody            _rb;
    private bool                 _isLocallyGrabbed;

    public override void Spawned()
    {
        _handGrab    = GetComponent<HandGrabInteractable>();
        _transformer = GetComponent<GrabFreeTransformer>();
        _rb          = GetComponent<Rigidbody>();

        if (_handGrab != null)
        {
            _handGrab.WhenSelectingInteractorViewAdded +=
                OnGrabStart;
            _handGrab.WhenSelectingInteractorViewRemoved +=
                OnGrabEnd;
        }

        if (Object.HasStateAuthority)
        {
            SyncPos   = transform.position;
            SyncRot   = transform.rotation;
            IsGrabbed = false;
        }
    }

    void OnGrabStart(IInteractorView view)
    {
        _isLocallyGrabbed = true;
        Debug.Log("GrabBridge: grab started");

        // Request authority so we can move it
        Object.RequestStateAuthority();

        if (Object.HasStateAuthority)
            IsGrabbed = true;

        // Enable transformer for free movement
        if (_transformer != null)
            _transformer.enabled = true;

        // Enable physics
        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity  = true;
        }
    }

    void OnGrabEnd(IInteractorView view)
    {
        _isLocallyGrabbed = false;
        Debug.Log("GrabBridge: grab ended");

        if (Object.HasStateAuthority)
            IsGrabbed = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            // Broadcast position every tick
            SyncPos = transform.position;
            SyncRot = transform.rotation;
        }
        else
        {
            if (!_isLocallyGrabbed)
            {
                // Follow authority position
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
                // Locally grabbed — broadcast our position
                SyncPos = transform.position;
                SyncRot = transform.rotation;
            }
        }
    }

    void OnDestroy()
    {
        if (_handGrab != null)
        {
            _handGrab.WhenSelectingInteractorViewAdded -=
                OnGrabStart;
            _handGrab.WhenSelectingInteractorViewRemoved -=
                OnGrabEnd;
        }
    }
}