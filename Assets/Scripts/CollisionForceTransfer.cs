using UnityEngine;
using System.Collections;
using Fusion;

public class CollisionForceTransfer : MonoBehaviour
{
    [Header("Force Settings")]
    [SerializeField] private float forceMultiplier = 0.5f;
    [SerializeField] private float minimumForceThreshold = 2f;

    [Header("Haptics")]
    [SerializeField] private bool enableHaptics = true;

    private BatSwingTracker swingTracker;
    private NetworkObject _netObj;
    private int _playerIndex = 0;
    private float _lastHitTime = -10f;
    private const float HitCooldown = 0.15f;
    private Coroutine hapticRoutine;


    public AudioSource audioSource;
    public AudioClip audioClip;

    void Awake()
    {
        swingTracker = GetComponent<BatSwingTracker>();
    }

    void Start()
    {
        StartCoroutine(ResolvePlayerIndex());
    }

    IEnumerator ResolvePlayerIndex()
    {
        NetworkRunner runner = null;
        yield return new WaitUntil(() =>
        {
            runner = FindFirstObjectByType<NetworkRunner>();
            return runner != null && runner.IsRunning;
        });

        float waited = 0f;
        while ((NetworkedRageState.Instance == null ||
                !NetworkedRageState.Instance.IsSpawned) && waited < 15f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        // Give RPC_RegisterPlayer time to propagate
        yield return new WaitForSeconds(1.5f);

        _playerIndex = NetworkedRageState.Instance != null
            ? NetworkedRageState.Instance.GetPlayerIndex(runner.LocalPlayer)
            : (runner.IsSharedModeMasterClient ? 0 : 1);

        Debug.Log($"[CollisionForceTransfer] {gameObject.name} → PlayerIndex={_playerIndex}");
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_netObj != null && !_netObj.HasInputAuthority) return;

        if (Time.time - _lastHitTime < HitCooldown) return;

        float impactForce = collision.impulse.magnitude * forceMultiplier;
        if (impactForce < minimumForceThreshold) return;

        Vector3 hitPoint = collision.GetContact(0).point;
        bool hasSwing = swingTracker != null && swingTracker.SwingVelocity.sqrMagnitude > 0.01f;
        Vector3 hitDir = hasSwing
            ? swingTracker.SwingVelocity.normalized
            : collision.relativeVelocity.normalized;
        float swingSpeed = hasSwing
            ? swingTracker.SwingSpeed
            : collision.relativeVelocity.magnitude;

        _lastHitTime = Time.time;

        // MeshExploderBreakable (networked mesh exploder)
        var meshBreakable = collision.gameObject.GetComponent<MeshExploderBreakable>();
        if (meshBreakable != null)
        {
            meshBreakable.TakeHit(_playerIndex, impactForce, swingSpeed);
            if (enableHaptics) TriggerHaptics(impactForce);
            return;
        }

        // DestructibleObject (networked destructible — already calls RPC_AddRage internally)
        var destructible = collision.gameObject.GetComponent<DestructibleObject>();
        if (destructible != null)
        {
            destructible.TakeHit(impactForce, swingSpeed, hitPoint, hitDir, _playerIndex);
            if (enableHaptics) TriggerHaptics(impactForce);
            return;
        }

        // BreakableObject (legacy local-only — fire RPC_AddRage manually)
        var breakable = collision.gameObject.GetComponent<BreakableObject>();
        if (breakable != null)
        {
            breakable.TakeHit(impactForce, hitPoint, hitDir);
            float gain = impactForce * 0.25f + swingSpeed * 0.8f + 5f;
            NetworkedRageState.Instance?.RPC_AddRage(_playerIndex, gain);
            if (enableHaptics) TriggerHaptics(impactForce);
            return;
        }

        // Generic fallback: any dynamic (Rigidbody) object contributes rage.
        // Skips static geometry — floors and walls have no Rigidbody.
        if (collision.gameObject.GetComponent<Rigidbody>() != null)
        {
            float gain = impactForce * 0.25f + swingSpeed * 0.8f + 5f;
            NetworkedRageState.Instance?.RPC_AddRage(_playerIndex, gain);
            if (enableHaptics) TriggerHaptics(impactForce);
            Debug.Log($"[CollisionForceTransfer] Generic hit: {collision.gameObject.name} " +
                      $"gain:{gain:F1} P{_playerIndex + 1}");
        }
    }

    void TriggerHaptics(float force)
    {
        audioSource.PlayOneShot(audioClip);

        float intensity = Mathf.Clamp01(force / 20f);

        StopHaptics();
        hapticRoutine = StartCoroutine(TriggerHapticsForSeconds(intensity, 0.3f));
    }

    System.Collections.IEnumerator TriggerHapticsForSeconds(float intensity, float duration)
    {
        OVRInput.SetControllerVibration(intensity, intensity, OVRInput.Controller.LTouch);
        yield return new WaitForSeconds(duration);
        StopHaptics();
    }

    void StopHaptics()
    {
        if (hapticRoutine != null)
        {
            StopCoroutine(hapticRoutine);
            hapticRoutine = null;
        }

        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
    }

}