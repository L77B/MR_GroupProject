using UnityEngine;
using Fusion;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class NetworkedBat : NetworkBehaviour
{
    [Networked] private Vector3     NetPos { get; set; }
    [Networked] private Quaternion  NetRot { get; set; }
    [Networked] private NetworkBool Grabbed { get; set; }

    private HandGrabInteractable _grab;
    private Rigidbody            _rb;
    private bool                 _heldLocally;

    public override void Spawned()
    {
        _rb   = GetComponent<Rigidbody>();
        _grab = GetComponent<HandGrabInteractable>();

        // Keep kinematic until grabbed
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity  = false;
        }

        if (_grab != null)
        {
            _grab.WhenSelectingInteractorViewAdded   +=
                OnGrab;
            _grab.WhenSelectingInteractorViewRemoved +=
                OnRelease;
        }

        if (Object.HasStateAuthority)
        {
            NetPos  = transform.position;
            NetRot  = transform.rotation;
            Grabbed = false;
        }
    }

    void OnGrab(IInteractorView view)
    {
        _heldLocally = true;
        Debug.Log("Bat grabbed locally!");

        Object.RequestStateAuthority();

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.useGravity  = true;
        }
    }

    void OnRelease(IInteractorView view)
    {
        _heldLocally = false;
        Debug.Log("Bat released!");
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            NetPos  = transform.position;
            NetRot  = transform.rotation;
        }
        else if (!_heldLocally)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                NetPos,
                Runner.DeltaTime * 25f);

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                NetRot,
                Runner.DeltaTime * 25f);
        }
    }

    void OnDestroy()
    {
        if (_grab != null)
        {
            _grab.WhenSelectingInteractorViewAdded   -=
                OnGrab;
            _grab.WhenSelectingInteractorViewRemoved -=
                OnRelease;
        }
    }
}