using UnityEngine;
using Meta.XR.MRUtilityKit;
using TMPro;

/// <summary>
/// Scans for the QR code ONCE, sets WorldOrigin, then fires OnColocated.
/// Completely independent of the Fusion session — can fire before or after session is ready.
/// The QR code does NOT need to stay in view after scanning.
/// </summary>
public class ColocationSetup : MonoBehaviour
{
    public static ColocationSetup Instance { get; private set; }

    [Header("QR")]
    [SerializeField] private string qrPayload = "GAME_ORIGIN_V1";

    [Header("Debug")]
    [SerializeField] private TMP_Text debugText;

    public bool       IsColocated    { get; private set; }
    public Vector3    OriginPosition { get; private set; }
    public Quaternion OriginRotation { get; private set; }

    public static event System.Action OnColocated;

    void Awake() => Instance = this;

    void Start()
    {
        Log($"Scanning for QR:\n{qrPayload}");

        if (MRUK.Instance == null)
        {
            Log("ERROR: MRUK not found!");
            return;
        }

        if (!MRUK.Instance.EnableWorldLock)
            Debug.LogWarning("[ColocationSetup] MRUK EnableWorldLock is OFF — " +
                             "enable it on the MRUK component for colocation to work.");

        // Load the saved room scan from the headset (e.g. "Studio").
        // MRUK's LoadSceneOnStartup flag does the same thing automatically —
        // this call is a belt-and-suspenders fallback so the floor/walls are
        // always available for spawning and canvas placement.
        if (MRUK.Instance.GetCurrentRoom() == null)
        {
            MRUK.Instance.RegisterSceneLoadedCallback(() =>
                Debug.Log("[ColocationSetup] MRUK room loaded from device."));
            MRUK.Instance.LoadSceneFromDevice();
        }

        var config = MRUK.Instance.SceneSettings.TrackerConfiguration;
        config.QRCodeTrackingEnabled = true;
        MRUK.Instance.SceneSettings.TrackerConfiguration = config;

        MRUK.Instance.SceneSettings.TrackableAdded.AddListener(OnTrackableAdded);
    }

    void OnTrackableAdded(MRUKTrackable trackable)
    {
        if (IsColocated) return;
        if (trackable == null) return;
        if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode) return;

        string payload = trackable.MarkerPayloadString;

        if (payload != qrPayload)
        {
            Log($"Wrong QR:\n{payload}");
            return;
        }

        IsColocated    = true;
        OriginPosition = trackable.transform.position;
        OriginRotation = trackable.transform.rotation;

        // Shift the shared world origin to the QR code location.
        // All scene children of WorldOrigin (dynamite, etc.) will appear
        // at their correct physical positions on both headsets.
        if (WorldOrigin.Instance != null)
            WorldOrigin.Instance.SetOrigin(OriginPosition, OriginRotation);
        else
            Debug.LogWarning("[ColocationSetup] WorldOrigin.Instance is null — " +
                             "add a WorldOrigin component to the scene.");

        Log($"Colocated!\nOrigin: {OriginPosition:F2}");
        Debug.Log($"[ColocationSetup] Origin set pos={OriginPosition} " +
                  $"rot={OriginRotation.eulerAngles}");

        // QR code can now leave the camera view — we're done with it.
        OnColocated?.Invoke();
    }

    void OnDestroy()
    {
        if (MRUK.Instance != null)
            MRUK.Instance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
    }

    void Log(string msg)
    {
        Debug.Log($"[Colocation] {msg}");
        if (debugText != null) debugText.text = msg;
    }
}
