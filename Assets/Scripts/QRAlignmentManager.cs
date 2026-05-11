using UnityEngine;
using ZXing;
using System.Collections;

public class QRAlignmentManager : MonoBehaviour
{
    public static QRAlignmentManager Instance;

    [Header("QR Settings")]
    [SerializeField] private string targetQRContent = "GAME_ORIGIN_V1";

    [Header("Debug")]
    [SerializeField] private GameObject debugAnchorSphere;
    [SerializeField] private UnityEngine.UI.Text debugText;

    public bool IsCalibrated         { get; private set; }
    public Vector3 AnchorPosition    { get; private set; }
    public Quaternion AnchorRotation { get; private set; }
    public Vector3 WallNormal        { get; private set; }
    public Vector3 FloorPosition     { get; private set; }

    private WebCamTexture camTexture;
    private BarcodeReaderGeneric reader;

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start() {
        reader = new BarcodeReaderGeneric();
        UpdateDebugText("Initialising camera...");
        StartCoroutine(InitialiseCamera());
    }

    void UpdateDebugText(string msg) {
        Debug.Log(msg);
        if (debugText != null)
            debugText.text = msg;
    }

    IEnumerator InitialiseCamera() {
        UpdateDebugText("Requesting camera permission...");

        yield return Application.RequestUserAuthorization(
            UserAuthorization.WebCam);

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) {
            UpdateDebugText("Camera permission DENIED!");
            yield break;
        }

        UpdateDebugText("Camera permission granted!");

        // List all available cameras
        WebCamDevice[] devices = WebCamTexture.devices;
        UpdateDebugText($"Found {devices.Length} camera(s)");

        if (devices.Length == 0) {
            UpdateDebugText("No cameras found on device!");
            yield break;
        }

        // Log all cameras found
        for (int i = 0; i < devices.Length; i++)
            Debug.Log($"Camera {i}: {devices[i].name}");

        // Try each camera until one works
        WebCamTexture workingCam = null;
        for (int i = 0; i < devices.Length; i++) {
            UpdateDebugText($"Trying camera {i}: {devices[i].name}");

            WebCamTexture testCam = new WebCamTexture(
                devices[i].name, 1280, 720, 30);
            testCam.Play();

            float waited = 0f;
            while (testCam.width < 100 && waited < 2f) {
                waited += Time.deltaTime;
                yield return null;
            }

            if (testCam.width > 100) {
                workingCam = testCam;
                UpdateDebugText($"Camera {i} working! " +
                               $"{testCam.width}x{testCam.height}");
                break;
            }
            else {
                testCam.Stop();
                UpdateDebugText($"Camera {i} failed, trying next...");
            }
        }

        if (workingCam == null) {
            UpdateDebugText("No working camera found!");
            yield break;
        }

        camTexture = workingCam;
        UpdateDebugText("Starting QR scan...");
        StartCoroutine(ScanLoop());
    }

    IEnumerator ScanLoop() {
        int attempts = 0;

        while (!IsCalibrated) {
            yield return new WaitForSeconds(0.2f);

            if (camTexture == null || !camTexture.isPlaying) {
                UpdateDebugText("Camera stopped!");
                continue;
            }

            attempts++;
            if (attempts % 15 == 0)
                UpdateDebugText($"Scanning... {attempts} attempts\n" +
                               $"Cam: {camTexture.width}x" +
                               $"{camTexture.height}");

            try {
                Color32[] pixels = camTexture.GetPixels32();

                if (pixels == null || pixels.Length == 0) {
                    UpdateDebugText("Camera returning empty pixels!");
                    continue;
                }

                byte[] rawRGB = Color32ToByteArray(pixels);

                var result = reader.Decode(
                    rawRGB,
                    camTexture.width,
                    camTexture.height,
                    RGBLuminanceSource.BitmapFormat.RGB24);

                if (result != null) {
                    UpdateDebugText($"QR found: {result.Text}");

                    if (result.Text == targetQRContent) {
                        AlignToQR();
                    }
                    else {
                        UpdateDebugText($"Wrong QR!\n" +
                                       $"Got: {result.Text}\n" +
                                       $"Need: {targetQRContent}");
                    }
                }
            }
            catch (System.Exception e) {
                Debug.LogWarning($"Scan error: {e.Message}");
            }
        }
    }

    void AlignToQR() {
        UpdateDebugText("QR Detected! Calibrating...");

        Ray ray = new Ray(
            Camera.main.transform.position,
            Camera.main.transform.forward);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 10f)) {
            AnchorPosition = hit.point;
            WallNormal     = hit.normal;
            AnchorRotation = Quaternion.LookRotation(-hit.normal);
            FloorPosition  = new Vector3(
                AnchorPosition.x, 0f, AnchorPosition.z);
        }
        else {
            AnchorPosition = Camera.main.transform.position +
                             Camera.main.transform.forward * 2f;
            WallNormal     = -Camera.main.transform.forward;
            AnchorRotation = Camera.main.transform.rotation;
            FloorPosition  = new Vector3(
                AnchorPosition.x, 0f, AnchorPosition.z);
            UpdateDebugText("No wall hit — using fallback position");
        }

        IsCalibrated = true;

        if (camTexture != null)
            camTexture.Stop();

        if (debugAnchorSphere != null) {
            debugAnchorSphere.SetActive(true);
            debugAnchorSphere.transform.position = AnchorPosition;
        }

        UpdateDebugText($"Calibrated!\nAnchor: {AnchorPosition}");

        if (GameObjectSpawner.Instance != null)
            GameObjectSpawner.Instance.SpawnAll();
        else
            UpdateDebugText("GameObjectSpawner not found!");
    }

    byte[] Color32ToByteArray(Color32[] colors) {
        byte[] bytes = new byte[colors.Length * 3];
        int index = 0;
        foreach (Color32 c in colors) {
            bytes[index++] = c.r;
            bytes[index++] = c.g;
            bytes[index++] = c.b;
        }
        return bytes;
    }

    void OnDestroy() {
        if (camTexture != null)
            camTexture.Stop();
    }
}