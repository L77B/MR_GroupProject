using UnityEngine;

public class BatSwingTracker : MonoBehaviour
{
    [Header("Tracking")]
    private Rigidbody rb;
    private Vector3 previousVelocity;

    [Header("Debug")]
    public float currentSwingSpeed;
    public float peakSwingSpeed;

    public float SwingSpeed => currentSwingSpeed;
    public Vector3 SwingVelocity => rb.linearVelocity;

    void Awake() {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate() {
        currentSwingSpeed = rb.linearVelocity.magnitude;

        // Track peak speed for this swing
        if (currentSwingSpeed > peakSwingSpeed)
            peakSwingSpeed = currentSwingSpeed;

        previousVelocity = rb.linearVelocity;
    }

    // Call this to reset peak after a swing
    public void ResetPeak() {
        peakSwingSpeed = 0f;
    }

    // Calculate impact force based on bat mass and velocity
    public float GetImpactForce() {
        return rb.mass * currentSwingSpeed;
    }
}