using UnityEngine;
using System.Collections;
using Fusion;

[RequireComponent(typeof(Rigidbody))]
public class BatImpactHandler : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────

    [Header("OVR Controller")]
    public OVRInput.Controller controllerHand =
        OVRInput.Controller.LTouch;

    [Header("Swing Thresholds")]
    [SerializeField] private float minSwingSpeed = 1.0f;
    [SerializeField] private float forceMultiplier = 1.2f;
    [SerializeField] private float maxForce = 50f;
    [SerializeField] private float hitCooldown = 0.15f;

    [Header("Hand Offset")]
    [SerializeField] private Vector3 handPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 handRotationOffset = Vector3.zero;

    [Header("Weapon State")]
    [SerializeField] private bool isEquipped = false;

    // ── Read-only Properties ──────────────────────────────────────────────

    public float CurrentSwingSpeed { get; private set; }
    public bool IsSwinging =>
        CurrentSwingSpeed >= minSwingSpeed;

    // ── Runtime State ─────────────────────────────────────────────────────

    private Rigidbody rb;
    private OVRCameraRig cameraRig;
    private Vector3 prevPosition;
    private float lastHitTime;

    // 0 = host/P1, 1 = client/P2 — set once the Fusion session is running
    private int _playerIndex = 0;
    private NetworkRunner _runner;

    // ── Unity Lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        cameraRig = FindAnyObjectByType<OVRCameraRig>();

        if (cameraRig == null)
            Debug.LogWarning(
                "[BatImpactHandler] OVRCameraRig " +
                "not found!");
    }

    private void Start()
    {
        StartCoroutine(DetectPlayerIndex());
    }

    private IEnumerator DetectPlayerIndex()
    {
        // Wait for a live session
        yield return new WaitUntil(() =>
        {
            _runner = FindFirstObjectByType<NetworkRunner>();
            return _runner != null && _runner.IsRunning;
        });

        // Wait for NetworkedRageState scene object (should be near-instant)
        float waited = 0f;
        while (NetworkedRageState.Instance == null && waited < 10f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        // Brief pause so RPC_RegisterPlayer calls can propagate
        yield return new WaitForSeconds(1.5f);

        if (NetworkedRageState.Instance != null)
            _playerIndex = NetworkedRageState.Instance
                               .GetPlayerIndex(_runner.LocalPlayer);
        else
            _playerIndex = _runner.IsSharedModeMasterClient ? 0 : 1;

        Debug.Log($"[BatImpactHandler] PlayerIndex = {_playerIndex} " +
                  $"for {_runner.LocalPlayer}");

        // Auto-equip on the peer that owns this bat. PlayerSpawner only runs on
        // the host, so the client's bat would never get equipped without this.
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.HasInputAuthority)
            isEquipped = true;
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
            Vector3 localPos =
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
        prevPosition = transform.position;
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
                $"P{_playerIndex + 1} " +
                $"force:{clampedForce:F1} " +
                $"speed:{CurrentSwingSpeed:F1}");
            breakable.TakeHit(_playerIndex, clampedForce, CurrentSwingSpeed);
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
                hitDir,
                _playerIndex);

            Debug.Log(
                $"[BatImpactHandler] Hit destructible: " +
                $"{destructible.name} " +
                $"P{_playerIndex + 1} " +
                $"force:{clampedForce:F1} " +
                $"speed:{CurrentSwingSpeed:F1}");
            return;
        }

        // ── Check BreakableObject ─────────────────
        BreakableObject legacyBreakable =
            collision.gameObject
                .GetComponent<BreakableObject>();

        if (legacyBreakable != null)
        {
            lastHitTime = Time.time;
            bool wasBroken = legacyBreakable.IsBroken;
            legacyBreakable.TakeHit(clampedForce, hitPoint, hitDir);
            if (!wasBroken && legacyBreakable.IsBroken && NetworkedRageState.Instance != null)
                NetworkedRageState.Instance.AddRage(_playerIndex, 10f);
            return;
        }

        // ── Check YueDestructible ─────────────────
        var yue = collision.gameObject
            .GetComponent<YueDestructibles.YueDestructible>();

        if (yue != null)
        {
            lastHitTime = Time.time;
            if (collision.impulse.magnitude > yue.maximumImpulse
                && NetworkedRageState.Instance != null)
            {
                NetworkedRageState.Instance.AddRage(_playerIndex, 10f);
                Debug.Log($"[BatImpactHandler] YueDestructible broken: " +
                          $"{collision.gameObject.name} P{_playerIndex + 1}");
            }
        }
    }
}