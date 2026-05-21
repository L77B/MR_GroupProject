using UnityEngine;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction;
using Fusion;

public class ObjectHanger : NetworkBehaviour
{
    [Networked] public Vector3     HangPosition
        { get; set; }
    [Networked] public Quaternion  HangRotation
        { get; set; }
    [Networked] public NetworkBool IsHanging
        { get; set; }

    private Rigidbody            rb;
    private HandGrabInteractable grabInteractable;

    public void Initialise(Vector3 position,
                           Quaternion rotation)
    {
        if (Object != null &&
            Object.HasStateAuthority)
        {
            HangPosition = position;
            HangRotation = rotation;
            IsHanging    = true;
        }

        Setup();
    }

    public override void Spawned()
    {
        Setup();

        if (IsHanging)
        {
            transform.position = HangPosition;
            transform.rotation = HangRotation;
        }
    }

    void Setup()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null && IsHanging)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

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

        Debug.Log($"{gameObject.name} grabbed!");

        if (Object != null && Object.HasStateAuthority)
        {
            IsHanging = false;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity  = true;
            }
        }
        else
        {
            // Request state authority so this
            // player can control the bat
            Object.RequestStateAuthority();
            IsHanging = false;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity  = true;
            }
        }

        if (grabInteractable != null)
            grabInteractable
                .WhenSelectingInteractorViewAdded -=
                OnGrabbed;
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