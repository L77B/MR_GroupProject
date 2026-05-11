using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class ObjectHanger : MonoBehaviour
{
    private Vector3 hangPosition;
    private Quaternion hangRotation;
    private Rigidbody rb;
    private bool isHanging = true;
    private HandGrabInteractable grabInteractable;

    public void Initialise(Vector3 position, Quaternion rotation) {
        hangPosition = position;
        hangRotation = rotation;

        rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.isKinematic = true; // frozen on wall until grabbed
            rb.useGravity  = false;
        }

        // Listen for grab events
        grabInteractable = GetComponent<HandGrabInteractable>();
        if (grabInteractable != null) {
            grabInteractable.WhenSelectingInteractorViewAdded +=
                OnGrabbed;
        }
    }

    void OnGrabbed(IInteractorView interactor) {
        if (!isHanging) return;
        isHanging = false;

        // Release from wall — enable full physics
        if (rb != null) {
            rb.isKinematic = false;
            rb.useGravity  = true;
        }

        // Unsubscribe from grab event
        if (grabInteractable != null) {
            grabInteractable.WhenSelectingInteractorViewAdded -=
                OnGrabbed;
        }

        Debug.Log($"{gameObject.name} grabbed from wall!");
    }

    void Update() {
        // Keep weapon pinned to wall while hanging
        if (isHanging && rb != null) {
            transform.position = hangPosition;
            transform.rotation = hangRotation;
        }
    }
}