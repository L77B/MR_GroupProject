using UnityEngine;
using Meta.XR.MRUtilityKit;
using TMPro;

public class QRManager : MonoBehaviour
{
    public static QRManager Instance;

    [Header("QR")]
    [SerializeField] private string targetQR = "HELLO";

    [Header("Shared Origin")]
    [SerializeField] private Transform sharedOrigin;

    [Header("Debug")]
    [SerializeField] private TMP_Text debugText;

    public bool IsCalibrated { get; private set; }

    public Transform SharedOrigin => sharedOrigin;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UpdateDebug("Waiting for QR...");
    }

    // Connect this in MRUKManager inspector
    public void OnTrackableAdded(MRUKTrackable trackable)
    {
        UpdateDebug("TRACKABLE DETECTED");

        if (IsCalibrated)
            return;

        if (trackable == null)
            return;

        string qrContent = trackable.name;

        Debug.Log($"QR Content: {qrContent}");

        UpdateDebug($"Detected:\n{qrContent}");

        // Simple comparison
        if (qrContent.Trim() == targetQR.Trim())
        {
            Calibrate(trackable);
        }
    }

    void Calibrate(MRUKTrackable trackable)
    {
        IsCalibrated = true;

        // Move shared origin to QR pose
        sharedOrigin.position =
            trackable.transform.position;

        sharedOrigin.rotation =
            trackable.transform.rotation;

        UpdateDebug("CALIBRATION SUCCESS");

        Debug.Log("CO-LOCATION CALIBRATED");

        // Spawn objects
        if (ObjectSpawner.Instance != null)
        {
            ObjectSpawner.Instance.SpawnObjects();
        }
    }

    void UpdateDebug(string msg)
    {
        Debug.Log(msg);

        if (debugText != null)
        {
            debugText.text = msg;
        }
    }
}