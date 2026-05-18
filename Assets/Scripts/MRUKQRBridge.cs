using UnityEngine;
using Meta.XR.MRUtilityKit;
using TMPro;
using Fusion;
using System.Collections;

public class MRUKQRBridge : MonoBehaviour
{
    [Header("QR Settings")]
    [SerializeField] private string qrCodePayload =
        "GAME_ORIGIN_V1";

    [Header("Debug")]
    [SerializeField] private TMP_Text debugText;

    private bool          _hasTriggered = false;
    private NetworkRunner _runner;

    void Start()
    {
        UpdateDebug($"Scanning for:\n{qrCodePayload}");

        if (MRUK.Instance == null)
        {
            UpdateDebug("MRUK null!");
            return;
        }

        var config = MRUK.Instance.SceneSettings
            .TrackerConfiguration;
        config.QRCodeTrackingEnabled = true;
        MRUK.Instance.SceneSettings
            .TrackerConfiguration = config;

        MRUK.Instance.SceneSettings.TrackableAdded
            .AddListener(OnTrackableAdded);

        StartCoroutine(FindRunner());
    }

    IEnumerator FindRunner()
    {
        yield return new WaitUntil(() => {
            _runner =
                FindFirstObjectByType<NetworkRunner>();
            return _runner != null && _runner.IsRunning;
        });
        Debug.Log("Runner found in QRBridge");
    }

    public void OnColocationReady()
    {
        UpdateDebug("Colocation ready!");
        Debug.Log("Colocation ready!");
    }

    public void OnTrackableAdded(
        MRUKTrackable trackable)
    {
        UpdateDebug("Trackable detected!");

        if (_hasTriggered) return;
        if (trackable == null) return;

        if (trackable.TrackableType !=
            OVRAnchor.TrackableType.QRCode)
        {
            UpdateDebug("Not a QR code");
            return;
        }

        string payload =
            trackable.MarkerPayloadString;
        UpdateDebug($"QR found:\n{payload}");

        if (payload != qrCodePayload)
        {
            UpdateDebug($"Wrong QR:\n{payload}");
            return;
        }

        _hasTriggered = true;
        UpdateDebug("Correct QR!\nConnecting...");

        StartCoroutine(ApplyOrigin(
            trackable.transform.position,
            trackable.transform.rotation));
    }

    IEnumerator ApplyOrigin(Vector3 position,
                             Quaternion rotation)
    {
        // Wait for runner
        yield return new WaitUntil(() =>
            _runner != null && _runner.IsRunning);

        // Wait for role
        yield return new WaitUntil(() =>
            _runner.IsServer || _runner.IsClient);

        // Extra wait for session to stabilise
        yield return new WaitForSeconds(2f);

        bool isHost = _runner.IsServer ||
                      _runner.IsSharedModeMasterClient;

        UpdateDebug($"Role: " +
                    $"{(isHost ? "HOST" : "CLIENT")}\n" +
                    $"Session: " +
                    $"{_runner.SessionInfo.Name}\n" +
                    $"Players: " +
                    $"{_runner.SessionInfo.PlayerCount}");

        // Set local world origin
        if (WorldOrigin.Instance != null)
            WorldOrigin.Instance.SetOrigin(
                position, rotation);

        if (isHost)
        {
            UpdateDebug("HOST!\nSpawning weapons...");

            if (WeaponSpawner.Instance != null)
                WeaponSpawner.Instance.SpawnWeapons(
                    position, rotation);
            else
                UpdateDebug("WeaponSpawner missing!");

            if (BreakableSpawner.Instance != null)
                BreakableSpawner.Instance.Initialise();
            else
                UpdateDebug("BreakableSpawner missing!");

            // Wait for bats to spawn
            yield return new WaitForSeconds(2f);
            UpdateDebug("Ready!\nBats spawned ✓");
        }
        else
        {
            UpdateDebug("CLIENT!\nWaiting for bats...");

            // Wait for bats to appear
            // (Fusion replicates from host)
            yield return new WaitUntil(() =>
                FindFirstObjectByType<NetworkedBat>()
                    != null);

            int count = FindObjectsByType<NetworkedBat>(
                FindObjectsSortMode.None).Length;

            UpdateDebug($"Ready!\n{count} bats found ✓");
        }
    }

    void UpdateDebug(string msg)
    {
        Debug.Log(msg);
        if (debugText != null)
            debugText.text = msg;
    }

    void OnDestroy()
    {
        if (MRUK.Instance != null)
            MRUK.Instance.SceneSettings.TrackableAdded
                .RemoveListener(OnTrackableAdded);
    }
}