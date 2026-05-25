using UnityEngine;
using NativeWebSocket;

/// <summary>
/// Connects to the LED-strip ESP32 WebSocket server and streams live rage
/// values so the physical strip mirrors the DualRageBarUI canvas.
///
/// SETUP
/// ─────
/// 1. Add this component to any active GameObject in the Xage scene.
/// 2. Set Server IP to the LED ESP32's IP address (check Serial Monitor after boot).
/// 3. Server Port must match WS_PORT in RageLEDStrip.ino (default 8082).
/// </summary>
public class RageLEDClient : MonoBehaviour
{
    [Header("LED ESP32 WebSocket")]
    public string serverIP   = "10.204.0.207"; // LED ESP32 IP — check its Serial Monitor
    public int    serverPort = 8082;

    [Header("Update Rate")]
    [SerializeField] private float sendInterval = 0.1f; // seconds between updates (10 Hz)

    private WebSocket _websocket;
    private float     _sendTimer;

    async void Start()
    {
        _websocket = new WebSocket($"ws://{serverIP}:{serverPort}");

        _websocket.OnOpen  += ()     => Debug.Log("[RageLED] Connected to LED ESP32");
        _websocket.OnClose += (code) => Debug.Log($"[RageLED] Disconnected ({code})");
        _websocket.OnError += (err)  => Debug.LogError($"[RageLED] Error: {err}");

        await _websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        _websocket?.DispatchMessageQueue();
#endif
        if (_websocket == null || _websocket.State != WebSocketState.Open) return;
        if (NetworkedRageState.Instance == null || !NetworkedRageState.Instance.IsSpawned) return;

        _sendTimer += Time.deltaTime;
        if (_sendTimer >= sendInterval)
        {
            _sendTimer = 0f;
            SendRage();
        }
    }

    async void OnDestroy()
    {
        if (_websocket != null)
            await _websocket.Close();
    }

    private async void SendRage()
    {
        float p1  = NetworkedRageState.Instance.RageP1;
        float p2  = NetworkedRageState.Instance.RageP2;
        float max = NetworkedRageState.Instance.MaxRage;
        await _websocket.SendText($"rage:{p1:F1},{p2:F1},{max:F1}");
    }
}
