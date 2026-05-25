using System.Collections;
using UnityEngine;

/// <summary>
/// Drives both RageMeters at configurable rates for simulation/demo.
/// Safe to deploy in APK — no Editor-only APIs.
///
/// KEY FIX — RE-FETCH METERS EVERY TICK
/// ──────────────────────────────────────
/// The original version cached rageMeterP1/P2 in Awake() and called AddRage()
/// on those cached references forever. When Fusion duplicates GameManager every
/// 6 seconds, new RageMeter components are created with new instanceIDs. The
/// cached references point at orphaned components that nobody reads — so P2
/// rage was always 0.0 in DualRageBarUI even though AddRage() was being called.
///
/// Fix: every tick, find the current active meters by playerIndex before calling
/// AddRage(). This costs one FindObjectsByType per tick (every 50ms) which is
/// acceptable for a simulator script.
///
/// The permanent fix is removing NetworkObject from GameManager so Fusion stops
/// duplicating it. But this simulator code is now resilient either way.
/// </summary>
public class RageSimulator : MonoBehaviour
{
    [Header("Fill Rates")]
    [Tooltip("Rage points added to P1 per second. 10 = 10%/s on a 0-100 scale.")]
    [SerializeField] private float rageP1PerSecond = 10f;

    [Tooltip("Rage points added to P2 per second. 20 = 20%/s on a 0-100 scale.")]
    [SerializeField] private float rageP2PerSecond = 20f;

    [Header("Tick Settings")]
    [Tooltip("How many times per second rage is injected. 20 = smooth bar animation.")]
    [SerializeField] private float ticksPerSecond = 20f;

    [Header("Simulation Control")]
    [Tooltip("Starts automatically on scene load.")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("After victory, pause this many seconds then reset and loop.")]
    [SerializeField] private bool loopAfterVictory = true;

    [Tooltip("Seconds to hold victory state before resetting (only when loopAfterVictory=true).")]
    [SerializeField] private float victoryHoldSeconds = 10f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private bool isRunning = false;
    private bool victoryTriggered = false;
    private Coroutine loopCoroutine;

    private void Start()
    {
        if (autoStart) StartSimulation();
    }

    private void OnDestroy() => StopSimulation();

    public void StartSimulation()
    {
        if (isRunning) return;
        isRunning = true;
        victoryTriggered = false;
        if (loopCoroutine != null) StopCoroutine(loopCoroutine);
        loopCoroutine = StartCoroutine(SimulationLoop());
        if (debugLogging)
            Debug.Log($"[RageSimulator] Started — P1:{rageP1PerSecond}/s  P2:{rageP2PerSecond}/s");
    }

    public void StopSimulation()
    {
        isRunning = false;
        if (loopCoroutine != null) { StopCoroutine(loopCoroutine); loopCoroutine = null; }
        if (debugLogging) Debug.Log("[RageSimulator] Stopped.");
    }

    /// <summary>
    /// Finds the current active P1 and P2 meters by playerIndex each tick.
    /// This handles Fusion duplications — new meters are picked up automatically.
    /// </summary>
    private (RageMeter p1, RageMeter p2) FindCurrentMeters()
    {
        RageMeter p1 = null, p2 = null;
        var all = FindObjectsByType<RageMeter>(FindObjectsSortMode.None);
        foreach (var m in all)
        {
            // Pick the meter with the highest instanceID for each index —
            // Fusion always creates new components with higher IDs so the
            // newest (active) one wins over the stale orphaned copies.
            if (m.playerIndex == 0 && (p1 == null || m.GetInstanceID() > p1.GetInstanceID()))
                p1 = m;
            if (m.playerIndex == 1 && (p2 == null || m.GetInstanceID() > p2.GetInstanceID()))
                p2 = m;
        }
        return (p1, p2);
    }

    private IEnumerator SimulationLoop()
    {
        float tickInterval = 1f / Mathf.Max(1f, ticksPerSecond);
        float p1PerTick = rageP1PerSecond / ticksPerSecond;
        float p2PerTick = rageP2PerSecond / ticksPerSecond;
        var tickWait = new WaitForSeconds(tickInterval);

        if (debugLogging)
            Debug.Log($"[RageSimulator] Tick interval:{tickInterval:F3}s  " +
                      $"P1/tick:{p1PerTick:F2}  P2/tick:{p2PerTick:F2}");

        while (isRunning)
        {
            yield return tickWait;
            if (!isRunning) yield break;

            // Re-fetch meters every tick — handles Fusion duplications
            var (p1, p2) = FindCurrentMeters();

            float p1Rage = p1 != null ? p1.CurrentRage : 0f;
            float p1Max = p1 != null ? p1.MaxRage : 100f;
            float p2Rage = p2 != null ? p2.CurrentRage : 0f;
            float p2Max = p2 != null ? p2.MaxRage : 100f;

            bool p1Full = p1Rage >= p1Max - 0.1f;
            bool p2Full = p2Rage >= p2Max - 0.1f;

            // Victory: both meters full
            if (p1Full && p2Full && !victoryTriggered)
            {
                victoryTriggered = true;
                if (debugLogging)
                    Debug.Log("[RageSimulator] VICTORY — both meters full!");

                if (loopAfterVictory)
                {
                    yield return StartCoroutine(VictoryAndReset());
                    yield break;
                }
                else
                {
                    isRunning = false;
                    yield break;
                }
            }

            // Inject rage into current active meters
            if (!p1Full && p1 != null) p1.AddRage(p1PerTick);
            if (!p2Full && p2 != null) p2.AddRage(p2PerTick);
        }
    }

    private IEnumerator VictoryAndReset()
    {
        if (debugLogging)
            Debug.Log($"[RageSimulator] Holding victory for {victoryHoldSeconds}s then resetting.");

        yield return new WaitForSeconds(victoryHoldSeconds);

        // Reset the current active meters
        var (p1, p2) = FindCurrentMeters();
        p1?.ResetRage();
        p2?.ResetRage();

        yield return new WaitForSeconds(0.5f);

        isRunning = true;
        victoryTriggered = false;
        loopCoroutine = StartCoroutine(SimulationLoop());

        if (debugLogging) Debug.Log("[RageSimulator] Loop restarted.");
    }
}