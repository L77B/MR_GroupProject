using UnityEngine;
using System.Collections;
using Fusion;
using Meta.XR.MRUtilityKit;
using TMPro;

/// <summary>
/// Waits for both the Fusion session (NetworkSessionManager) and QR colocation
/// (ColocationSetup) to be ready, then initialises the game: registers the local
/// player and, if host, spawns the score canvas on the best wall.
/// </summary>
public class GameOrchestrator : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private NetworkObject canvasPrefab;
    [SerializeField] private float         canvasWallHeight = 1.6f;
    [SerializeField] private float         canvasWallOffset = 0.05f;

    [Header("Debug")]
    [SerializeField] private TMP_Text debugText;

    private bool _sessionReady = false;
    private bool _colocated    = false;
    private bool _initialized  = false;

    // ── Event Wiring ──────────────────────────────────────────────────────────

    void OnEnable()
    {
        NetworkSessionManager.OnSessionReady  += OnSessionReady;
        NetworkSessionManager.OnSessionFailed += OnSessionFailed;
        ColocationSetup.OnColocated           += OnColocated;
    }

    void OnDisable()
    {
        NetworkSessionManager.OnSessionReady  -= OnSessionReady;
        NetworkSessionManager.OnSessionFailed -= OnSessionFailed;
        ColocationSetup.OnColocated           -= OnColocated;
    }

    // ── Readiness Callbacks ───────────────────────────────────────────────────

    void OnSessionReady()
    {
        _sessionReady = true;
        Log(_colocated ? "Session ready!" : "Session ready!\nNow scan QR code...");
        TryInitialize();
    }

    void OnSessionFailed()
    {
        Log("Session FAILED!\nCheck Photon App ID / region.");
    }

    void OnColocated()
    {
        _colocated = true;
        Log(_sessionReady ? "Colocated!" : "Colocated!\nWaiting for session...");
        TryInitialize();
    }

    void TryInitialize()
    {
        if (_initialized || !_sessionReady || !_colocated) return;
        _initialized = true;
        StartCoroutine(Initialize());
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    IEnumerator Initialize()
    {
        Log("Initialising game...");

        // NetworkedRageState is a scene object — Fusion networks it automatically
        // as soon as StartGame completes. This wait should resolve in < 1 s.
        float waited = 0f;
        while ((NetworkedRageState.Instance == null || !NetworkedRageState.Instance.IsSpawned)
               && waited < 20f)
        {
            if (Mathf.FloorToInt(waited) % 3 == 0 && waited > 0f)
                Debug.Log($"[GameOrchestrator] Waiting for RageState… " +
                          $"Instance={NetworkedRageState.Instance != null} " +
                          $"IsSpawned={NetworkedRageState.Instance?.IsSpawned} " +
                          $"t={waited:F1}s");
            waited += Time.deltaTime;
            yield return null;
        }

        if (NetworkedRageState.Instance == null || !NetworkedRageState.Instance.IsSpawned)
        {
            bool exists = NetworkedRageState.Instance != null;
            Log($"ERROR: NetworkedRageState\n{(exists ? "exists but not networked" : "not in scene")}!\nSee console.");
            Debug.LogError($"[GameOrchestrator] NetworkedRageState timed out. " +
                           $"Instance exists:{exists} IsSpawned:{NetworkedRageState.Instance?.IsSpawned}\n" +
                           "Checklist:\n" +
                           "  1. GameObject is ACTIVE in Xage hierarchy\n" +
                           "  2. NetworkObject component present with Is Root = true\n" +
                           "  3. NOT listed in NetworkPrefabTable\n" +
                           "  4. Scene build index is correct in NetworkSessionManager");
            yield break;
        }

        NetworkRunner runner = NetworkSessionManager.Instance.Runner;
        bool isHost = runner.IsSharedModeMasterClient;

        Log($"Role: {(isHost ? "HOST" : "CLIENT")}\nSession: {runner.SessionInfo.Name}");
        Debug.Log($"[GameOrchestrator] Init — IsHost:{isHost} Player:{runner.LocalPlayer} " +
                  $"waited {waited:F1}s for RageState");

        // Register local player so NetworkedRageState can assign P1 / P2 slots.
        NetworkedRageState.Instance.RPC_RegisterPlayer(runner.LocalPlayer);

        if (isHost)
        {
            StartCoroutine(SpawnCanvasOnWall(runner));
            yield return new WaitForSeconds(2f);
            Log("HOST ready!\nPress ESP32 buttons to spawn");
        }
        else
        {
            Log("CLIENT ready!\nWaiting for ESP32...");
        }
    }

    // ── Canvas Spawn ──────────────────────────────────────────────────────────

    IEnumerator SpawnCanvasOnWall(NetworkRunner runner)
    {
        if (canvasPrefab == null)
        {
            Debug.LogError("[GameOrchestrator] canvasPrefab not assigned in Inspector!");
            yield break;
        }

        // Wait up to 5 s for MRUK room + at least one wall anchor
        MRUKRoom room    = null;
        float    elapsed = 0f;
        while (elapsed < 5f)
        {
            room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;
            if (room != null && room.WallAnchors != null && room.WallAnchors.Count > 0) break;
            yield return null;
            elapsed += Time.deltaTime;
        }

        Vector3    pos;
        Quaternion rot;

        if (room != null && room.WallAnchors != null && room.WallAnchors.Count > 0)
        {
            // Pick the wall with the largest surface area
            MRUKAnchor best     = room.WallAnchors[0];
            float      bestArea = -1f;
            foreach (var wall in room.WallAnchors)
            {
                if (!wall.PlaneRect.HasValue) continue;
                float area = wall.PlaneRect.Value.width * wall.PlaneRect.Value.height;
                if (area > bestArea) { bestArea = area; best = wall; }
            }

            // wall.transform.forward points into the room;
            // LookRotation(-inward) makes canvas face inward (readable from inside).
            Vector3 inward = best.transform.forward;
            pos   = best.transform.position + inward * canvasWallOffset;
            pos.y = canvasWallHeight;
            rot   = Quaternion.LookRotation(-inward, Vector3.up);

            Debug.Log($"[GameOrchestrator] Wall '{best.name}' area={bestArea:F2} → canvas pos={pos}");
        }
        else
        {
            // Fallback: place 1.5 m in front of the camera
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            pos   = (cam != null ? cam.transform.position : Vector3.zero) + fwd * 1.5f;
            pos.y = canvasWallHeight;
            rot   = Quaternion.LookRotation(-fwd, Vector3.up);
            Debug.LogWarning($"[GameOrchestrator] No MRUK walls — canvas fallback at {pos}");
        }

        var spawned = runner.Spawn(canvasPrefab, pos, rot);
        if (spawned != null)
        {
            Debug.Log($"[GameOrchestrator] Canvas spawned at {pos}");

            // SetupUIManager.LateUpdate() moves the canvas to follow the camera.
            // Calling Hide() disables the component so the canvas stays on the wall.
            var setupUI = spawned.GetComponentInChildren<SetupUIManager>(includeInactive: true);
            if (setupUI != null)
                setupUI.Hide();
        }
        else
            Debug.LogError("[GameOrchestrator] runner.Spawn returned null — " +
                           "check canvasPrefab is registered in NetworkPrefabTable.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void Log(string msg)
    {
        Debug.Log($"[GameOrchestrator] {msg}");
        if (debugText != null) debugText.text = msg;
    }
}
