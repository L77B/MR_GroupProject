using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BatImpactHandler : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────

    [Header("OVR Controller")]
    public OVRInput.Controller controllerHand =
        OVRInput.Controller.LTouch;

    [Header("Swing Thresholds")]
    [SerializeField] private float minSwingSpeed  = 1.0f;
    [SerializeField] private float forceMultiplier = 1.2f;
    [SerializeField] private float maxForce        = 50f;
    [SerializeField] private float hitCooldown     = 0.15f;

    [Header("Hand Offset")]
    [SerializeField] private Vector3 handPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 handRotationOffset = Vector3.zero;

    [Header("Weapon State")]
    [SerializeField] private bool isEquipped = false;

    // ── Read-only Properties ──────────────────────────────────────────────

    public float CurrentSwingSpeed { get; private set; }
    public bool  IsSwinging =>
        CurrentSwingSpeed >= minSwingSpeed;

    // ── Runtime State ─────────────────────────────────────────────────────

    private Rigidbody    rb;
    private OVRCameraRig cameraRig;
    private Vector3      prevPosition;
    private float        lastHitTime;

    // ── Unity Lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic  = true;
        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        cameraRig = FindAnyObjectByType<OVRCameraRig>();

        if (cameraRig == null)
            Debug.LogWarning(
                "[BatImpactHandler] OVRCameraRig " +
                "not found!");
    }

    // Called by WeaponSpawner when X is pressed
    public void SetEquipped(bool equipped)
    {
        isEquipped = equipped;
    }

    private void Update()
    {
        // Only follow controller when equipped
        if (!isEquipped) return;

        Transform trackingSpace = cameraRig != null
            ? cameraRig.trackingSpace
            : null;

        if (trackingSpace != null)
        {
            Vector3    localPos =
                OVRInput.GetLocalControllerPosition(
                    controllerHand);
            Quaternion localRot =
                OVRInput.GetLocalControllerRotation(
                    controllerHand);

            Vector3 offsetPos = localPos +
                localRot * handPositionOffset;
            Quaternion offsetRot = localRot *
                Quaternion.Euler(handRotationOffset);

            transform.position =
                trackingSpace.TransformPoint(offsetPos);
            transform.rotation =
                trackingSpace.rotation * offsetRot;
        }
        else
        {
            transform.localPosition =
                OVRInput.GetLocalControllerPosition(
                    controllerHand);
            transform.localRotation =
                OVRInput.GetLocalControllerRotation(
                    controllerHand);
        }
    }

    private void FixedUpdate()
    {
        if (!isEquipped)
        {
            CurrentSwingSpeed = 0f;
            prevPosition = transform.position;
            return;
        }

        Vector3 velocity =
            (transform.position - prevPosition) /
            Time.fixedDeltaTime;
        CurrentSwingSpeed = velocity.magnitude;
        prevPosition      = transform.position;
    }

    // ── Collision ─────────────────────────────────────────────────────────

    private void OnCollisionEnter(Collision collision)
    {
        // Only process hits when equipped and swinging
        if (!isEquipped) return;
        if (!IsSwinging) return;
        if (Time.time - lastHitTime < hitCooldown)
            return;

        float rawForce =
            collision.impulse.magnitude * forceMultiplier;
        float clampedForce =
            Mathf.Clamp(rawForce, 0f, maxForce);

        Vector3 hitPoint =
            collision.GetContact(0).point;
        Vector3 hitDir =
            (transform.position - prevPosition)
            .normalized;

        // ── Check MeshExploderBreakable first ────
        MeshExploderBreakable breakable =
            collision.gameObject
                .GetComponent<MeshExploderBreakable>();

        if (breakable != null)
        {
            lastHitTime = Time.time;
            Debug.Log(
                $"[BatImpactHandler] Hit breakable: " +
                $"{collision.gameObject.name} " +
                $"force:{clampedForce:F1} " +
                $"speed:{CurrentSwingSpeed:F1}");
            breakable.TakeHit();
            return;
        }

        // ── Check DestructibleObject ─────────────
        DestructibleObject destructible =
            collision.gameObject
                .GetComponent<DestructibleObject>();

        if (destructible != null)
        {
            lastHitTime = Time.time;
            destructible.TakeHit(
                clampedForce,
                CurrentSwingSpeed,
                hitPoint,
                hitDir);

            Debug.Log(
                $"[BatImpactHandler] Hit destructible: " +
                $"{destructible.name} " +
                $"force:{clampedForce:F1} " +
                $"speed:{CurrentSwingSpeed:F1}");
        }
    }
}