using UnityEngine;
using Meta.XR.MRUtilityKit;
using TMPro;
using Fusion;
using System.Collections;
using Unity.Netcode;

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
            UpdateDebug("HOST!\nSpawning...");

            // Spawn test cube
            TestSpawner testSpawner =
                FindFirstObjectByType<TestSpawner>();
            if (testSpawner != null)
                testSpawner.SpawnCubes(
                    position, rotation);
            else
                Debug.Log("No TestSpawner — " +
                          "using weapon spawner");

            // Spawn weapons
            if (WeaponSpawner.Instance != null)
                WeaponSpawner.Instance.SpawnWeapons(
                    position, rotation);
            else
                UpdateDebug("WeaponSpawner missing!");

            // Spawn breakables
            if (BreakableSpawner.Instance != null)
                BreakableSpawner.Instance.Initialise();
            else
                UpdateDebug("BreakableSpawner missing!");

            yield return new WaitForSeconds(2f);
            UpdateDebug("Ready!\nSpawn complete ✓");
        }
        else
        {
            UpdateDebug("CLIENT!\nWaiting...");

            // Wait for networked objects to appear
            yield return new WaitForSeconds(5f);

            UpdateDebug("CLIENT ready!");
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