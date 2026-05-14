using UnityEngine;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction;

public class ObjectHanger : MonoBehaviour
{
    private Vector3    hangPosition;
    private Quaternion hangRotation;
    private Rigidbody  rb;
    private bool       isHanging = true;
    private HandGrabInteractable grabInteractable;

    public void Initialise(Vector3 position, Quaternion rotation) {
        hangPosition = position;
        hangRotation = rotation;

        rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        grabInteractable = GetComponent<HandGrabInteractable>();
        if (grabInteractable != null)
            grabInteractable.WhenSelectingInteractorViewAdded +=
                OnGrabbed;
    }

    void OnGrabbed(IInteractorView interactor) {
        if (!isHanging) return;
        isHanging = false;

        if (rb != null) {
            rb.isKinematic = false;
            rb.useGravity  = true;
        }

        if (grabInteractable != null)
            grabInteractable.WhenSelectingInteractorViewAdded -=
                OnGrabbed;

        Debug.Log($"{gameObject.name} grabbed from wall!");
    }

    void Update() {
        if (isHanging) {
            transform.position = hangPosition;
            transform.rotation = hangRotation;
        }
    }
}