using UnityEngine;

/// <summary>
/// Attach to the Baseball Bat (or any swingable weapon) in the scene.
///
/// RESPONSIBILITIES
/// ────────────────
/// 1. When the player is HOLDING the bat (WeaponPickup.IsHeld = true):
///      - Mirror the Quest 3 controller pose every frame so the virtual
///        bat matches exactly where the player's hand is.
///      - Track tip velocity to measure swing speed.
///      - Detect collisions and call DestructibleObject.TakeHit().
///
/// 2. When the bat is NOT held (sitting on the weapon rack):
///      - Do nothing. The bat stays wherever WeaponRack placed it.
///      - Controller tracking is disabled so walking around does not
///        drag the bat across the room.
///
/// HIERARCHY
/// ─────────
/// The bat must NOT be parented under RightHandAnchor or any part of
/// OVRCameraRig. Place it at the scene root or as a child of WeaponRack.
/// When held, this script overrides the transform directly in world space
/// using OVRInput world-space position — no parent anchor needed.
///
/// SETUP
/// ─────
/// 1. Place the bat prefab at scene root (not under OVRCameraRig).
/// 2. Add Rigidbody → IsKinematic = true, Interpolation = Interpolate.
/// 3. Add CapsuleCollider along the barrel.
/// 4. Add BatImpactHandler — set controllerHand to RTouch.
/// 5. Add WeaponPickup — BatImpactHandler reads IsHeld from it.
/// 6. Add HandGrabInteractable + Grabbable (OVR Interaction SDK).
/// 7. Register the bat with WeaponRack slot 0 via RegisterWeaponInSlot().
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BatImpactHandler : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("OVR Controller")]
    [Tooltip("Which Quest 3 controller drives this bat when held. " +
             "RTouch = right hand (default), LTouch = left hand.")]
    public OVRInput.Controller controllerHand = OVRInput.Controller.RTouch;

    [Header("Swing Thresholds")]
    [Tooltip("Minimum tip speed in m/s for a swing to register as a real hit. " +
             "Prevents slow accidental bumps from triggering damage.")]
    [SerializeField] private float minSwingSpeed = 1.0f;

    [Tooltip("Multiplier applied to the raw physics impulse magnitude before it is " +
             "passed to TakeHit() as 'force'. Tune in Play Mode.")]
    [SerializeField] private float forceMultiplier = 1.2f;

    [Tooltip("Hard cap on the force value sent to TakeHit(). " +
             "Prevents a single fast swing from one-shotting everything.")]
    [SerializeField] private float maxForce = 50f;

    [Tooltip("Minimum seconds between hits on the same target. " +
             "Prevents multiple OnCollisionEnter calls in one physics step.")]
    [SerializeField] private float hitCooldown = 0.15f;

    [Header("Hand Offset (fine-tune to match 3D printed handle)")]
    [Tooltip("Position offset from the controller origin to the bat pivot. " +
             "Use this to align the virtual bat with the physical printed handle. " +
             "Adjust in Play Mode while watching the headset view.")]
    [SerializeField] private Vector3 handPositionOffset = Vector3.zero;

    [Tooltip("Rotation offset from the controller orientation to the bat orientation. " +
             "Use this to correct any angular misalignment with the printed handle.")]
    [SerializeField] private Vector3 handRotationOffset = Vector3.zero;

    // ── Read-only Properties ──────────────────────────────────────────────────

    /// <summary>Current tip speed in m/s computed from positional delta.</summary>
    public float CurrentSwingSpeed { get; private set; }

    /// <summary>True when the bat is moving fast enough to deal damage.</summary>
    public bool IsSwinging => CurrentSwingSpeed >= minSwingSpeed;

    // ── Runtime State ─────────────────────────────────────────────────────────

    private Rigidbody rb;
    private WeaponPickup weaponPickup;   // Tells us whether the player is holding the bat
    private OVRCameraRig cameraRig;      // Cached reference — Instance is internal in SDK
    private Vector3 prevPosition;  // For velocity calculation in FixedUpdate
    private float lastHitTime;   // For hit cooldown

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Kinematic: bat position is driven by controller input when held,
        // or by WeaponRack when resting. Physics forces never move it directly.
        rb.isKinematic = true;

        // Interpolation reduces visual jitter between physics steps
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Cache WeaponPickup — used every Update to check IsHeld
        // WeaponPickup must be on the same GameObject as BatImpactHandler
        weaponPickup = GetComponent<WeaponPickup>();

        if (weaponPickup == null)
            Debug.LogWarning("[BatImpactHandler] No WeaponPickup found on this GameObject. " +
                             "The bat will always follow the controller. " +
                             "Add WeaponPickup to the same GameObject.");

        // Cache OVRCameraRig via FindAnyObjectByType because OVRCameraRig.Instance
        // is marked internal in the Meta SDK and not accessible from user scripts.
        // Caching in Awake() avoids calling FindAnyObjectByType every frame.
        cameraRig = FindAnyObjectByType<OVRCameraRig>();

        if (cameraRig == null)
            Debug.LogWarning("[BatImpactHandler] OVRCameraRig not found in scene. " +
                             "Controller tracking will use fallback local space. " +
                             "Make sure OVRCameraRig prefab is in the scene.");
    }

    /// <summary>
    /// Update runs every rendered frame.
    ///
    /// KEY BEHAVIOUR CHANGE FROM ORIGINAL:
    /// Only mirrors the controller pose when WeaponPickup.IsHeld is true.
    /// When the bat is sitting on the rack (IsHeld = false), this method
    /// does nothing and the bat stays wherever WeaponRack placed it.
    ///
    /// Uses WORLD SPACE position from OVRInput rather than local space,
    /// so the bat does not need to be parented under any hand anchor.
    /// </summary>
    private void Update()
    {
        // Do nothing if the player is not holding the bat.
        // The bat stays on the rack in whatever position WeaponRack placed it.
        if (weaponPickup != null && !weaponPickup.IsHeld)
            return;

        // ── Mirror controller pose in WORLD SPACE ─────────────────────────────
        // We use GetLocalControllerPosition relative to OVRCameraRig's
        // tracking space rather than local transform parenting.
        // This means the bat does NOT need to be a child of RightHandAnchor.

        // Get the tracking space transform from OVRCameraRig.
        // OVRCameraRig.Instance and GetTrackingSpace() are both marked
        // internal in the Meta SDK so they are not accessible from user scripts.
        // FindAnyObjectByType is the correct public way to get the reference.
        // We cache it in Awake() for performance — see cameraRig field below.
        Transform trackingSpace = cameraRig != null
            ? cameraRig.trackingSpace
            : null;

        if (trackingSpace != null)
        {
            // Controller position in local tracking space → convert to world space
            Vector3 localPos = OVRInput.GetLocalControllerPosition(controllerHand);
            Quaternion localRot = OVRInput.GetLocalControllerRotation(controllerHand);

            // Apply the hand offset so the virtual bat aligns with the
            // physical 3D printed handle (adjust handPositionOffset in Inspector)
            Vector3 offsetPos = localPos + localRot * handPositionOffset;
            Quaternion offsetRot = localRot * Quaternion.Euler(handRotationOffset);

            // Convert from tracking space to world space
            transform.position = trackingSpace.TransformPoint(offsetPos);
            transform.rotation = trackingSpace.rotation * offsetRot;
        }
        else
        {
            // Fallback if OVRCameraRig is not found —
            // use local space (requires bat to be under a hand anchor)
            transform.localPosition = OVRInput.GetLocalControllerPosition(controllerHand);
            transform.localRotation = OVRInput.GetLocalControllerRotation(controllerHand);
        }
    }

    /// <summary>
    /// FixedUpdate runs in sync with the physics engine.
    /// Velocity is computed here so it aligns with collision callbacks.
    /// Only computes velocity when held to avoid false swing readings
    /// while the bat is resting on the rack.
    /// </summary>
    private void FixedUpdate()
    {
        if (weaponPickup != null && !weaponPickup.IsHeld)
        {
            // Bat is on the rack — zero out swing speed so no accidental hits
            CurrentSwingSpeed = 0f;
            prevPosition = transform.position;
            return;
        }

        // Velocity = displacement / time — tip speed in world space (m/s)
        Vector3 velocity = (transform.position - prevPosition) / Time.fixedDeltaTime;
        CurrentSwingSpeed = velocity.magnitude;
        prevPosition = transform.position;
    }

    // ── Collision ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by Unity's physics engine when the bat collider contacts another collider.
    /// Only processes hits when the bat is being held and swung fast enough.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // Must be held and swinging to deal damage —
        // prevents rack collisions from triggering hits
        if (weaponPickup != null && !weaponPickup.IsHeld) return;
        if (!IsSwinging) return;
        if (Time.time - lastHitTime < hitCooldown) return;

        var target = collision.gameObject.GetComponent<DestructibleObject>();
        if (target == null) return;

        lastHitTime = Time.time;

        // Scale impulse by forceMultiplier and cap at maxForce
        float rawForce = collision.impulse.magnitude * forceMultiplier;
        float clampedForce = Mathf.Clamp(rawForce, 0f, maxForce);

        Vector3 hitPoint = collision.GetContact(0).point;
        Vector3 hitDir = (transform.position - prevPosition).normalized;

        target.TakeHit(clampedForce, CurrentSwingSpeed, hitPoint, hitDir);

        Debug.Log($"[BatImpactHandler] Hit {target.name} — " +
                  $"force:{clampedForce:F1}  speed:{CurrentSwingSpeed:F1}");
    }
}