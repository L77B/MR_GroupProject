using UnityEngine;
using NativeWebSocket;
using UnityEngine.SceneManagement;
using Meta.XR.MRUtilityKit;
using System.Collections;
using Fusion;
using TMPro;

public class WebSocketSceneClient : MonoBehaviour
{
    [Header("WebSocket")]
    public string serverIP   = "10.204.0.206";
    public int    serverPort = 8081;

    [Header("Explosion")]
    public DestructibleObject dynamite;
    public GameObject         explosionObject;
    private bool              hasExploded = false;

    [Header("Spawn Prefabs")]
    public GameObject[] spawnPrefabsA;
    public GameObject[] spawnPrefabsB;

    [Header("Spawn Settings")]
    public FindSpawnPositions spawnFinder;

    [Header("References")]
    public SceneLoaderTwo sceneLoader;

    [Header("Debug")]
    public TMP_Text debugText;

    private WebSocket _websocket;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    async void Start()
    {
        UpdateDebug("Starting...");

        UpdateDebug($"Connecting to ESP32:\n{serverIP}:{serverPort}");

        _websocket = new WebSocket($"ws://{serverIP}:{serverPort}");

        _websocket.OnOpen += async () =>
        {
            Debug.Log("WebSocket connected!");
            UpdateDebug("ESP32 Connected!\nScan QR to start...");
            await _websocket.SendText("Unity connected");
        };

        _websocket.OnMessage += (bytes) =>
        {
            string msg = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log($"[WS] Received: {msg}");
            HandleMessage(msg);
        };

        _websocket.OnError += (error) =>
        {
            Debug.LogError($"WebSocket error: {error}");
            UpdateDebug($"ESP32 Error:\n{error}");
        };

        _websocket.OnClose += (code) =>
        {
            Debug.Log($"WebSocket closed: {code}");
            UpdateDebug("ESP32 Disconnected!");
        };

        await _websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        _websocket?.DispatchMessageQueue();
#endif

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Alpha1))
            StartCoroutine(SpawnViaRpc(0));
        if (Input.GetKeyDown(KeyCode.Alpha2))
            StartCoroutine(SpawnViaRpc(1));
        if (Input.GetKeyDown(KeyCode.E))
            StartCoroutine(ExplosionViaRpc());
        if (Input.GetKeyDown(KeyCode.R))
            StartCoroutine(RestartViaRpc());
#endif
    }

    async void OnDestroy()
    {
        if (_websocket != null)
            await _websocket.Close();
    }

    // ── Event Subscriptions ───────────────────────────────────────────────────

    private void OnEnable()
    {
        // OnSpawnRequested only fires on StateAuthority (master client) —
        // no guard needed here, the RPC source already ensures it.
        NetworkedRageState.OnSpawnRequested    += OnSpawnRequested;
        NetworkedRageState.OnExplosionTriggered += OnNetworkedExplosion;
        NetworkedRageState.OnRestartRequested  += OnNetworkedRestart;
    }

    private void OnDisable()
    {
        NetworkedRageState.OnSpawnRequested    -= OnSpawnRequested;
        NetworkedRageState.OnExplosionTriggered -= OnNetworkedExplosion;
        NetworkedRageState.OnRestartRequested  -= OnNetworkedRestart;
    }

    // Fires ONLY on StateAuthority peer (master client) via RPC_RequestSpawn.
    private void OnSpawnRequested(int buttonIndex)
    {
        GameObject[] prefabs = buttonIndex == 0 ? spawnPrefabsA : spawnPrefabsB;
        UpdateDebug($"Spawning set {buttonIndex + 1}...");
        SpawnNetworked(prefabs);
    }

    // Fires on ALL peers via RPC_BroadcastExplosion.
    private void OnNetworkedExplosion()
    {
        if (hasExploded) return;
        hasExploded = true;
        UpdateDebug("EXPLOSION!");
        if (explosionObject != null) explosionObject.SetActive(true);
        if (dynamite != null)        dynamite.gameObject.SetActive(false);
        SendToESP32("led:explosion");
    }

    // Fires on ALL peers via RPC_BroadcastRestart.
    private void OnNetworkedRestart()
    {
        UpdateDebug("Restarting...");
        RestartGame();
    }

    // ── Message Handling ──────────────────────────────────────────────────────

    private void HandleMessage(string msg)
    {
        if (!msg.Contains(":")) return;

        int colon = msg.IndexOf(":");
        string value = msg[(colon + 1)..];

        if (msg.Contains("button") && value == "1")
        {
            UpdateDebug("Restarting game...");
            StartCoroutine(RestartViaRpc());
        }

        if (msg.Contains("spawnA") && value == "1")
        {
            UpdateDebug("Btn 1 pressed...");
            StartCoroutine(SpawnViaRpc(0));
        }

        if (msg.Contains("spawnB") && value == "1")
        {
            UpdateDebug("Btn 2 pressed...");
            StartCoroutine(SpawnViaRpc(1));
        }

        if (msg.Contains("main") && value == "1" && !hasExploded)
        {
            StartCoroutine(ExplosionViaRpc());
        }
    }

    // ── RPC Relay Coroutines ──────────────────────────────────────────────────

    // Waits for NetworkedRageState scene object, then asks StateAuthority to spawn.
    // Because it's StateAuthority-targeted, only the master client spawns —
    // no double-spawn risk regardless of which headset has the ESP32.
    IEnumerator SpawnViaRpc(int buttonIndex)
    {
        float waited = 0f;
        while ((NetworkedRageState.Instance == null || !NetworkedRageState.Instance.IsSpawned)
               && waited < 15f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (NetworkedRageState.Instance == null || !NetworkedRageState.Instance.IsSpawned)
        {
            UpdateDebug("ERROR: Session not ready.\nScan QR code first.");
            yield break;
        }

        UpdateDebug($"Btn {buttonIndex + 1} → requesting spawn\n(waited {waited:F1}s)");
        NetworkedRageState.Instance.RPC_RequestSpawn(buttonIndex);
    }

    IEnumerator ExplosionViaRpc()
    {
        float waited = 0f;
        while ((NetworkedRageState.Instance == null || !NetworkedRageState.Instance.IsSpawned)
               && waited < 15f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (NetworkedRageState.Instance != null && NetworkedRageState.Instance.IsSpawned)
            NetworkedRageState.Instance.RPC_BroadcastExplosion();
        else
            OnNetworkedExplosion(); // last-resort local fallback
    }

    IEnumerator RestartViaRpc()
    {
        float waited = 0f;
        while ((NetworkedRageState.Instance == null || !NetworkedRageState.Instance.IsSpawned)
               && waited < 15f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (NetworkedRageState.Instance != null && NetworkedRageState.Instance.IsSpawned)
            NetworkedRageState.Instance.RPC_BroadcastRestart();
        else
            RestartGame(); // last-resort local fallback
    }

    // ── Networked Spawning (runs only on StateAuthority / master client) ──────

    private void SpawnNetworked(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            UpdateDebug("ERROR: No prefabs assigned!\nCheck Inspector.");
            return;
        }

        if (NetworkedRageState.Instance == null) { UpdateDebug("ERROR: No session!"); return; }
        NetworkRunner runner = NetworkedRageState.Instance.Runner;
        if (runner == null || !runner.IsRunning)
        {
            UpdateDebug("ERROR: No running session!");
            return;
        }

        Vector3 pos = GetRandomMRPosition();
        if (pos == Vector3.zero)
        {
            UpdateDebug("ERROR: No floor position!\nRoom not scanned.");
            return;
        }

        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        NetworkObject netObj = prefab.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            UpdateDebug($"ERROR: {prefab.name}\nmissing NetworkObject!");
            return;
        }

        UpdateDebug($"Spawning:\n{prefab.name}");

        NetworkObject spawned = runner.Spawn(
            netObj, pos,
            Quaternion.Euler(0, Random.Range(0f, 360f), 0));

        if (spawned != null)
        {
            UpdateDebug($"Spawned!\n{prefab.name}\npos={pos:F2}");
            NetworkedBreakable breakable = spawned.GetComponent<NetworkedBreakable>();
            if (breakable != null)
                breakable.OnBroken += (go) => UpdateDebug($"Broken:\n{go.name}");
        }
        else
        {
            UpdateDebug($"Spawn Failed!\n{prefab.name}\nCheck prefab table.");
        }
    }

    // ── MRUK Position ─────────────────────────────────────────────────────────

    private Vector3 GetRandomMRPosition()
    {
        if (MRUK.Instance == null)
        {
            UpdateDebug("ERROR: MRUK not found!");
            return Vector3.zero;
        }

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            UpdateDebug("ERROR: No MR room!\nScan room first.");
            return Vector3.zero;
        }

        float clearance = spawnFinder != null
            ? spawnFinder.SurfaceClearanceDistance : 0.1f;

        bool success = room.GenerateRandomPositionOnSurface(
            MRUK.SurfaceType.FACING_UP, clearance,
            new LabelFilter(
                MRUKAnchor.SceneLabels.FLOOR |
                MRUKAnchor.SceneLabels.TABLE),
            out Vector3 pos, out _);

        if (!success)
        {
            UpdateDebug("ERROR: No surface!\nFloor not detected.");
            return Vector3.zero;
        }

        return pos;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void RestartGame()
    {
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex);
    }

    // ── ESP32 ─────────────────────────────────────────────────────────────────

    private async void SendToESP32(string message)
    {
        if (_websocket == null) return;
        if (_websocket.State != WebSocketState.Open) return;
        await _websocket.SendText(message);
        Debug.Log($"Sent to ESP32: {message}");
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

    private void UpdateDebug(string msg)
    {
        Debug.Log($"[WS] {msg}");
        if (debugText != null) debugText.text = msg;
    }
}
