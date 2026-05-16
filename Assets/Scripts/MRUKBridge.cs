using UnityEngine;
using Meta.XR.MRUtilityKit;

public class MRUKQRBridge : MonoBehaviour
{
    [Header("QR Settings")]
    [SerializeField] private string qrCodePayload = "GAME_ORIGIN_V1";

    private bool _colocationReady = false;
    private bool _qrDetected      = false;
    private Vector3    _pendingPosition;
    private Quaternion _pendingRotation;

    void Start()
    {
        if (MRUK.Instance == null)
        {
            Debug.LogError("MRUK Instance is null!");
            return;
        }

        // Enable QR tracking via code
        var config = MRUK.Instance.SceneSettings
            .TrackerConfiguration;
        config.QRCodeTrackingEnabled = true;
        MRUK.Instance.SceneSettings.TrackerConfiguration
            = config;

        // Subscribe to trackable event via code
        MRUK.Instance.SceneSettings.TrackableAdded
            .AddListener(OnTrackableAdded);

        Debug.Log($"QR Bridge ready. " +
                  $"Looking for: {qrCodePayload}");
    }

    // Wire to ColocationController → ColocationReadyCallbacks
    public void OnColocationReady()
    {
        Debug.Log("Colocation ready!");
        _colocationReady = true;

        if (_qrDetected)
        {
            Debug.Log("Applying pending QR placement...");
            ApplyOrigin(_pendingPosition, _pendingRotation);
        }
    }

    void OnTrackableAdded(MRUKTrackable trackable)
    {
        // Log everything for debugging
        Debug.Log($"Trackable added! " +
                  $"Type: {trackable.TrackableType}");
        Debug.Log($"Payload: " +
                  $"{trackable.MarkerPayloadString}");

        // Only handle QR codes
        if (trackable.TrackableType !=
            OVRAnchor.TrackableType.QRCode)
        {
            Debug.Log("Not a QR code — ignoring");
            return;
        }

        // Check content matches our target
        if (trackable.MarkerPayloadString != qrCodePayload)
        {
            Debug.Log($"Wrong QR: " +
                      $"{trackable.MarkerPayloadString}\n" +
                      $"Expected: {qrCodePayload}");
            return;
        }

        Debug.Log($"Correct QR detected: {qrCodePayload}");

        if (_colocationReady)
        {
            // Colocation ready — apply immediately
            ApplyOrigin(
                trackable.transform.position,
                trackable.transform.rotation);
        }
        else
        {
            // Store and wait for colocation
            Debug.Log("Colocation not ready — " +
                      "holding QR placement");
            _pendingPosition = trackable.transform.position;
            _pendingRotation = trackable.transform.rotation;
            _qrDetected      = true;
        }
    }

    void ApplyOrigin(Vector3 position, Quaternion rotation)
    {
        Debug.Log($"Applying origin at: {position}");

        // Set world origin
        if (WorldOrigin.Instance != null)
            WorldOrigin.Instance.SetOrigin(
                position, rotation);
        else
            Debug.LogError("WorldOrigin not found!");

        // Spawn directly at QR position
        if (ObjectSpawner.Instance != null)
            ObjectSpawner.Instance.SpawnAtPosition(
                position, rotation);
        else
            Debug.LogError("ObjectSpawner not found!");
    }

    void OnDestroy()
    {
        if (MRUK.Instance != null)
            MRUK.Instance.SceneSettings.TrackableAdded
                .RemoveListener(OnTrackableAdded);
    }
}