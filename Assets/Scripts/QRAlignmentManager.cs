using UnityEngine;
using ZXing;
using System.Collections;

public class QRAlignmentManager : MonoBehaviour
{
    public static QRAlignmentManager Instance;

    [Header("QR Settings")]
    [SerializeField] private string targetQRContent = "GAME_ORIGIN_V1";

    [Header("Calibration UI")]
    [SerializeField] private GameObject calibrationUI;
    [SerializeField] private GameObject successUI;

    [Header("Debug")]
    [SerializeField] private GameObject debugAnchorSphere;

    // Public state
    public bool IsCalibrated { get; private set; }
    public Vector3 AnchorPosition { get; private set; }
    public Quaternion AnchorRotation { get; private set; }

    // Private
    private WebCamTexture camTexture;
    private BarcodeReaderGeneric reader;
    private bool isScanning = false;
    private Color32[] pixels;

    void Awake() {
        // Singleton
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start() {
        reader = new BarcodeReaderGeneric();

        // Show calibration UI at start
        if (calibrationUI != null)
            calibrationUI.SetActive(true);
        if (successUI != null)
            successUI.SetActive(false);

        // Hide debug sphere until calibrated
        if (debugAnchorSphere != null)
            debugAnchorSphere.SetActive(false);

        StartCoroutine(InitialiseCamera());
    }

    IEnumerator InitialiseCamera() {
        // Request camera permission
        yield return Application.RequestUserAuthorization(
            UserAuthorization.WebCam);

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) {
            Debug.LogError("Camera permission denied!");
            yield break;
        }

        // Start webcam
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0) {
            Debug.LogError("No camera found!");
            yield break;
        }

        // On Quest, use the first available camera
        camTexture = new WebCamTexture(devices[0].name, 1280, 720, 30);
        camTexture.Play();

        // Wait for camera to initialise
        yield return new WaitUntil(() => camTexture.width > 100);

        Debug.Log("Camera initialised. Starting QR scan...");
        isScanning = true;
        StartCoroutine(ScanLoop());
    }

    IEnumerator ScanLoop() {
        while (!IsCalibrated) {
            // Wait for next frame
            yield return new WaitForSeconds(0.2f);

            if (camTexture == null || !camTexture.isPlaying)
                continue;

            // Grab pixels and scan
            pixels = camTexture.GetPixels32();

            try {
                var result = reader.Decode(
                    pixels,
                    camTexture.width,
                    camTexture.height);

                if (result != null) {
                    Debug.Log($"QR Detected: {result.Text}");

                    if (result.Text == targetQRContent) {
                        AlignToQR();
                    }
                }
            }
            catch (System.Exception e) {
                Debug.LogWarning($"QR scan error: {e.Message}");
            }
        }
    }

    void AlignToQR() {
        // Cast ray from camera centre to find world position
        Ray ray = new Ray(
            Camera.main.transform.position,
            Camera.main.transform.forward);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 5f)) {
            AnchorPosition = hit.point;
            AnchorRotation = Quaternion.LookRotation(-hit.normal);
        }
        else {
            // Fallback — place anchor 2m in front of camera
            AnchorPosition = Camera.main.transform.position +
                             Camera.main.transform.forward * 2f;
            AnchorRotation = Camera.main.transform.rotation;
        }

        IsCalibrated = true;
        isScanning   = false;

        // Stop camera to save battery
        if (camTexture != null)
            camTexture.Stop();

        // Update UI
        if (calibrationUI != null)
            calibrationUI.SetActive(false);
        if (successUI != null) {
            successUI.SetActive(true);
            StartCoroutine(HideSuccessUI());
        }

        // Show debug sphere at anchor point
        if (debugAnchorSphere != null) {
            debugAnchorSphere.SetActive(true);
            debugAnchorSphere.transform.position = AnchorPosition;
        }

        Debug.Log($"Calibrated! Anchor at: {AnchorPosition}");
    }

    IEnumerator HideSuccessUI() {
        yield return new WaitForSeconds(3f);
        if (successUI != null)
            successUI.SetActive(false);
    }

    // Convert anchor-relative position to world position
    public Vector3 ToWorldPosition(Vector3 anchorRelativePos) {
        return AnchorPosition + AnchorRotation * anchorRelativePos;
    }

    // Convert anchor-relative rotation to world rotation
    public Quaternion ToWorldRotation(Quaternion anchorRelativeRot) {
        return AnchorRotation * anchorRelativeRot;
    }

    void OnDestroy() {
        if (camTexture != null)
            camTexture.Stop();
    }
}