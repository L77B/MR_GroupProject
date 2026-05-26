using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using System.Collections;
using TMPro;

public class NetworkSessionManager : MonoBehaviour
{
    public static NetworkSessionManager Instance { get; private set; }

    [Header("Session")]
    [SerializeField] private string sessionName = "GameRoom";

    [Header("Debug")]
    [SerializeField] private TMP_Text debugText;

    public NetworkRunner Runner { get; private set; }
    public bool IsReady { get; private set; }

    public static event System.Action OnSessionReady;
    public static event System.Action OnSessionFailed;

    public GameObject theDebugText;

    void Awake() => Instance = this;

    void Start() => StartCoroutine(Connect());

    IEnumerator Connect()
    {
        Log("Connecting to\nPhoton session...");

        // Reuse any runner already in the scene (building blocks may have created one)
        Runner = FindFirstObjectByType<NetworkRunner>();
        if (Runner == null)
            Runner = new GameObject("NetworkRunner").AddComponent<NetworkRunner>();

        Runner.ProvideInput = true;

        var sm = Runner.GetComponent<NetworkSceneManagerDefault>()
              ?? Runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        sm.IsSceneTakeOverEnabled = false;

        // Tell Fusion which scene is already loaded so it discovers scene NetworkObjects
        // (e.g. NetworkedRageState) without attempting a scene reload.
        var task = Runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = sessionName,
            SceneManager = sm,
            Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
        });

        yield return new WaitUntil(() => task.IsCompleted);

        if (!task.Result.Ok)
        {
            yield return null; // one extra frame — Shared Mode sometimes reports non-OK but runner is live

            if (!Runner.IsRunning)
            {
                Log($"FAILED:\n{task.Result.ShutdownReason}\nCheck App ID / region");
                Debug.LogError($"[SessionManager] StartGame failed: {task.Result.ShutdownReason}");
                OnSessionFailed?.Invoke();
                yield break;
            }

            Debug.LogWarning($"[SessionManager] Non-OK but runner live: {task.Result.ShutdownReason}");
        }

        // Brief pause for SharedModeMasterClient assignment to settle
        yield return new WaitForSeconds(1f);

        IsReady = true;
        bool isHost = Runner.IsSharedModeMasterClient;
        Log($"Session ready!\nRole: {(isHost ? "HOST" : "CLIENT")}\nScan QR to colocate...");
        Debug.Log($"[SessionManager] Ready — IsHost:{isHost} Session:{Runner.SessionInfo.Name}");
        OnSessionReady?.Invoke();
        debugText.gameObject.SetActive(false);
    }

    void Log(string msg)
    {
        Debug.Log($"[SessionManager] {msg}");
        if (debugText != null) debugText.text = msg;
    }
}
