using UnityEngine;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction;
using Fusion;

public class ObjectHanger : NetworkBehaviour
{
    [Networked] private Vector3     HangPosition { get; set; }
    [Networked] private Quaternion  HangRotation { get; set; }
    [Networked] private NetworkBool IsHanging    { get; set; }

    private Rigidbody            rb;
    private HandGrabInteractable grabInteractable;
    private bool                 _initialised = false;

    public void Initialise(Vector3 position,
                           Quaternion rotation)
    {
        _initialised = true;

        if (Object != null && Object.HasStateAuthority)
        {
            HangPosition = position;
            HangRotation = rotation;
            IsHanging    = true;
        }

        SetupPhysics();
        SetupGrab();
    }

    public override void Spawned()
    {
        SetupPhysics();

        if (IsHanging)
        {
            transform.position = HangPosition;
            transform.rotation = HangRotation;
        }
    }

    void SetupPhysics()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null && IsHanging)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
    }

    void SetupGrab()
    {
        grabInteractable =
            GetComponent<HandGrabInteractable>();
        if (grabInteractable != null)
            grabInteractable
                .WhenSelectingInteractorViewAdded +=
                OnGrabbed;
    }

    void OnGrabbed(IInteractorView interactor)
    {
        if (!IsHanging) return;

        if (Object != null && Object.HasStateAuthority)
            IsHanging = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity  = true;
        }

        if (grabInteractable != null)
            grabInteractable
                .WhenSelectingInteractorViewAdded -=
                OnGrabbed;

        Debug.Log($"{gameObject.name} grabbed!");
    }

    public override void FixedUpdateNetwork()
    {
        if (IsHanging)
        {
            transform.position = HangPosition;
            transform.rotation = HangRotation;
        }
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
            grabInteractable
                .WhenSelectingInteractorViewAdded -=
                OnGrabbed;
    }
}