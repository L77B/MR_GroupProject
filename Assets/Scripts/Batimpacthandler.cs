using UnityEngine;

/// <summary>
/// Attach to the Baseball Bat (or any swingable weapon) in the scene.
///
/// RESPONSIBILITIES
/// ────────────────
/// 1. Mirror the physical Quest 3 controller pose every frame so the virtual
///    bat matches where the player's hand actually is.
/// 2. Track the bat tip's velocity each FixedUpdate to measure swing speed.
/// 3. On collision with a DestructibleObject, compute the impact force from the
///    physics impulse, clamp it to a safe range, and call TakeHit().
///
/// SETUP
/// ─────
/// - Parent this GameObject to OVRCameraRig / TrackingSpace / RightHandAnchor
///   (or LeftHandAnchor for a left-handed bat).
/// - Add a Rigidbody → set Is Kinematic = true (the bat follows the hand, not physics).
/// - Add a CapsuleCollider aligned along the barrel length.
/// - Set controllerHand to match the hand anchor (RTouch for right, LTouch for left).
/// - Assign forceMultiplier and minSwingSpeed in the Inspector and tune in Play Mode.
///
/// NOTE
/// ────
/// The bat does NOT use OVRInput.GetLocalControllerVelocity() because that API
/// can return unstable spikes on very fast swings. Instead, we compute velocity
/// from the positional delta between FixedUpdate frames which is more consistent.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BatImpactHandler : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("OVR Controller")]
    [Tooltip("Which Quest 3 controller this bat tracks. " +
             "RTouch = right hand (default), LTouch = left hand.")]
    public OVRInput.Controller controllerHand = OVRInput.Controller.RTouch;

    [Header("Swing Thresholds")]
    [Tooltip("Minimum tip speed in m/s for a swing to register as a real hit. " +
             "Prevents slow accidental bumps from triggering damage.")]
    [SerializeField] private float minSwingSpeed = 1.0f;

    [Tooltip("Multiplier applied to the raw physics impulse magnitude before it is " +
             "passed to TakeHit() as 'force'. " +
             "Tune this in Play Mode so hits feel correctly weighted.")]
    [SerializeField] private float forceMultiplier = 1.2f;

    [Tooltip("Hard cap on the force value sent to TakeHit(). " +
             "Prevents a single extremely fast swing from one-shotting everything.")]
    [SerializeField] private float maxForce = 50f;

    [Tooltip("Minimum seconds that must pass before the same target can be hit again. " +
             "Prevents multiple OnCollisionEnter calls in one physics step from " +
             "registering as multiple hits.")]
    [SerializeField] private float hitCooldown = 0.15f;

    // ── Read-only Properties ──────────────────────────────────────────────────

    /// <summary>Current tip speed in m/s computed from positional delta (FixedUpdate).</summary>
    public float CurrentSwingSpeed { get; private set; }

    /// <summary>True when the bat is moving fast enough to deal damage.</summary>
    public bool IsSwinging => CurrentSwingSpeed >= minSwingSpeed;

    // ── Runtime State ─────────────────────────────────────────────────────────

    private Rigidbody rb;
    private Vector3 prevPosition;  // Position last FixedUpdate — used for velocity calc
    private float lastHitTime;   // Timestamp of the most recent hit — used for cooldown

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Kinematic: the bat position is driven by controller input, not physics forces.
        // The Rigidbody still participates in collision detection.
        rb.isKinematic = true;

        // Interpolation smooths the visual position between physics steps,
        // reducing the chance of the bat "tunnelling" through thin objects.
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    /// <summary>
    /// Update runs every rendered frame.
    /// We mirror the controller pose here (not in FixedUpdate) so the bat
    /// visually follows the hand without any lag.
    /// </summary>
    private void Update()
    {
        // Read the controller's local position and rotation from the OVR SDK
        // and apply them as LOCAL transforms (relative to the hand anchor parent)
        transform.localPosition = OVRInput.GetLocalControllerPosition(controllerHand);
        transform.localRotation = OVRInput.GetLocalControllerRotation(controllerHand);
    }

    /// <summary>
    /// FixedUpdate runs in sync with the physics engine.
    /// We compute swing speed here so it aligns with the collision callbacks
    /// which are also physics-step based.
    /// </summary>
    private void FixedUpdate()
    {
        // Velocity = displacement / time — gives us tip speed in world space (m/s)
        Vector3 velocity = (transform.position - prevPosition) / Time.fixedDeltaTime;
        CurrentSwingSpeed = velocity.magnitude;

        // Store current position as the baseline for the next FixedUpdate
        prevPosition = transform.position;
    }

    // ── Collision ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by Unity's physics engine when the bat collider contacts another collider.
    /// Validates that the hit is intentional (fast enough, not too soon after the last hit),
    /// then computes and forwards the hit data to the target DestructibleObject.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // Reject idle bumps and slow accidental contacts
        if (!IsSwinging) return;

        // Enforce per-hit cooldown to avoid duplicate calls in one physics step
        if (Time.time - lastHitTime < hitCooldown) return;

        // We only care about hitting destructible objects
        var target = collision.gameObject.GetComponent<DestructibleObject>();
        if (target == null) return;

        // Record hit time before doing anything else
        lastHitTime = Time.time;

        // ── Force calculation ─────────────────────────────────────────────────
        // collision.impulse is the physics engine's resolved impulse vector for this frame.
        // Its magnitude is a good proxy for "how hard did the bat hit this object."
        float rawForce = collision.impulse.magnitude * forceMultiplier;
        float clampedForce = Mathf.Clamp(rawForce, 0f, maxForce);

        // ── Hit geometry ──────────────────────────────────────────────────────
        // First contact point gives us where on the surface the bat made contact
        Vector3 hitPoint = collision.GetContact(0).point;

        // Approximate hit direction from the bat's movement this physics step
        Vector3 hitDir = (transform.position - prevPosition).normalized;

        // Forward all data to the destructible object
        target.TakeHit(clampedForce, CurrentSwingSpeed, hitPoint, hitDir);

        Debug.Log($"[BatImpactHandler] Hit {target.name} — " +
                  $"rawForce:{rawForce:F1}  clamped:{clampedForce:F1}  speed:{CurrentSwingSpeed:F1}");
    }
}