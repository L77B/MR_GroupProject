using UnityEngine;

public class CollisionForceTransfer : MonoBehaviour
{
    [Header("Force Settings")]
    [SerializeField] private float forceMultiplier = 1.5f;
    [SerializeField] private float minimumForceThreshold = 2f;

    [Header("Haptics")]
    [SerializeField] private bool enableHaptics = true;
    //[SerializeField] private float hapticDuration = 0.1f;

    private BatSwingTracker swingTracker;

    void Awake() {
        swingTracker = GetComponent<BatSwingTracker>();
    }

    void OnCollisionEnter(Collision collision) {
        // Calculate impact force from collision impulse
        float impactForce = collision.impulse.magnitude * forceMultiplier;

        // Ignore very light touches
        if (impactForce < minimumForceThreshold) return;

        // Get the hit point and direction
        ContactPoint contact    = collision.GetContact(0);
        Vector3 hitPoint        = contact.point;
        Vector3 hitDirection    = swingTracker.SwingVelocity.normalized;

        // Check if the hit object is breakable
        BreakableObject breakable = collision.gameObject
            .GetComponent<BreakableObject>();

        if (breakable != null) {
            breakable.TakeHit(impactForce, hitPoint, hitDirection);
        }

        // Trigger haptics on impact
        if (enableHaptics) {
            TriggerHaptics(impactForce);
        }

        Debug.Log($"Hit {collision.gameObject.name} " +
                  $"with force: {impactForce:F2}");
    }

    void TriggerHaptics(float force) {
        // Scale haptic intensity with force
        float intensity = Mathf.Clamp01(force / 20f);
        OVRInput.SetControllerVibration(intensity, intensity,
            OVRInput.Controller.RTouch);
    }
}