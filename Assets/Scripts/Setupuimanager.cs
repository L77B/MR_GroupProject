using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Oculus.Interaction;
using Image = UnityEngine.UI.Image;
using Button = UnityEngine.UI.Button;

/// <summary>
/// Manages the setup UI shown during GameFlowManager Phase 2.
/// After the player confirms position, Hide() hides only the setup elements.
/// The Slider, RageScoreText and RageLevelText stay visible throughout gameplay.
/// </summary>
public class SetupUIManager : MonoBehaviour
{
    // ── Inspector References ──────────────────────────────────────────────────

    [Header("UI Elements")]
    [Tooltip("Main status text showing current phase message.")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Tooltip("Background panel image — colour changes per phase.")]
    [SerializeField] private Image backgroundPanel;

    [Header("Setup Panel")]
    [Tooltip("Parent GO containing all setup UI (buttons, status text, background). " +
             "This entire GO is hidden when game starts. " +
             "Slider, RageScoreText, RageLevelText must be OUTSIDE this GO " +
             "so they remain visible during gameplay.")]
    [SerializeField] private GameObject setupPanel;

    [Header("Confirm Button")]
    [Tooltip("The confirm button GameObject.")]
    [SerializeField] private GameObject confirmButtonGO;

    [Tooltip("Unity Button component on the confirm button.")]
    [SerializeField] private Button confirmButtonComponent;

    [Tooltip("Text label on the confirm button.")]
    [SerializeField] private TextMeshProUGUI confirmButtonText;

    [Tooltip("PokeInteractable on the confirm button (optional).")]
    [SerializeField] private PokeInteractable confirmPokeInteractable;

    [Header("Reset Button")]
    [Tooltip("The reset button GameObject.")]
    [SerializeField] private GameObject resetButtonGO;

    [Tooltip("Unity Button component on the reset button.")]
    [SerializeField] private Button resetButtonComponent;

    [Tooltip("Text label on the reset button.")]
    [SerializeField] private TextMeshProUGUI resetButtonText;

    [Tooltip("PokeInteractable on the reset button (optional).")]
    [SerializeField] private PokeInteractable resetPokeInteractable;

    [Header("Phase Colours")]
    [SerializeField] private Color waitingColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
    [SerializeField] private Color readyColor = new Color(0.05f, 0.35f, 0.05f, 0.95f);
    [SerializeField] private Color confirmedColor = new Color(0.05f, 0.05f, 0.45f, 0.95f);

    [Header("Positioning")]
    [Tooltip("Distance in RAY mode (1.5 to 2.0m).")]
    [SerializeField] private float rayModeDistance = 1.5f;

    [Tooltip("Distance in POKE mode (0.5 to 0.7m).")]
    [SerializeField] private float pokeModeDistance = 0.6f;

    [Tooltip("Height relative to eye level. 0 = eye level.")]
    [SerializeField] private float heightOffset = -0.1f;

    [Tooltip("Hand distance threshold to switch to poke mode.")]
    [SerializeField] private float pokeSwitchDistance = 1.0f;

    [Tooltip("Smoothing speed between ray and poke distances.")]
    [SerializeField] private float distanceSmoothSpeed = 3f;

    [Header("Poke Cooldown")]
    [Tooltip("Minimum seconds between poke detections to prevent double-firing.")]
    [SerializeField] private float pokeCooldown = 1.0f;

    // ── Events ────────────────────────────────────────────────────────────────

    public System.Action OnConfirmClicked;
    public System.Action OnResetClicked;

    // ── Runtime State ─────────────────────────────────────────────────────────

    private Camera playerCamera;
    private bool confirmEnabled = false;
    private float lastConfirmTime = -999f;
    private float lastResetTime = -999f;
    private float currentDistance;
    private bool inPokeMode = false;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        playerCamera = Camera.main;

        if (confirmButtonComponent != null)
            confirmButtonComponent.onClick.AddListener(OnConfirmPressed);

        if (resetButtonComponent != null)
            resetButtonComponent.onClick.AddListener(OnResetPressed);

        if (confirmPokeInteractable != null)
            confirmPokeInteractable.WhenStateChanged += OnConfirmPokeStateChanged;

        if (resetPokeInteractable != null)
            resetPokeInteractable.WhenStateChanged += OnResetPokeStateChanged;
    }

    private void Start()
    {
        currentDistance = rayModeDistance;
        SetWaitingState();
        SnapInFrontOfPlayer();
    }

    private void LateUpdate()
    {
        FollowPlayer();
    }

    private void OnDestroy()
    {
        if (confirmButtonComponent != null)
            confirmButtonComponent.onClick.RemoveListener(OnConfirmPressed);

        if (resetButtonComponent != null)
            resetButtonComponent.onClick.RemoveListener(OnResetPressed);

        if (confirmPokeInteractable != null)
            confirmPokeInteractable.WhenStateChanged -= OnConfirmPokeStateChanged;

        if (resetPokeInteractable != null)
            resetPokeInteractable.WhenStateChanged -= OnResetPokeStateChanged;
    }

    // ── Public State API ──────────────────────────────────────────────────────

    public void SetWaitingState()
    {
        confirmEnabled = false;
        SetStatus("Scanning your room...\nPlease wait.");
        SetPanelColor(waitingColor);
        SetConfirmInteractable(false);
        if (confirmButtonText != null)
            confirmButtonText.text = "Please Wait...";
    }

    public void SetReadyState()
    {
        confirmEnabled = true;
        SetStatus("Room scanned!\n\nWalk to your play area.\n\nPinch or press:\nCONFIRM POSITION");
        SetPanelColor(readyColor);
        SetConfirmInteractable(true);
        if (confirmButtonText != null)
            confirmButtonText.text = "CONFIRM POSITION";
    }

    public void SetConfirmedState()
    {
        confirmEnabled = false;
        SetStatus("Position confirmed!\nSpawning objects...");
        SetPanelColor(confirmedColor);
        SetConfirmInteractable(false);
        SetResetInteractable(false);
        if (confirmButtonText != null)
            confirmButtonText.text = "Starting...";
    }

    /// <summary>
    /// Hides the setup panel only.
    /// The Canvas stays active so Slider, RageScoreText and RageLevelText
    /// remain visible throughout gameplay.
    /// </summary>
    public void Hide()
    {
        // Hide the SetupPanel GO — this hides confirm button, reset button,
        // status text and background in one call.
        if (setupPanel != null)
        {
            setupPanel.SetActive(false);
            Debug.Log("[SetupUIManager] SetupPanel hidden. Rage meter remains visible.");
        }
        else
        {
            // Fallback if setupPanel not assigned — hide individual elements
            if (confirmButtonGO != null) confirmButtonGO.SetActive(false);
            if (resetButtonGO != null) resetButtonGO.SetActive(false);
            if (backgroundPanel != null) backgroundPanel.gameObject.SetActive(false);
            if (statusText != null) statusText.gameObject.SetActive(false);
            Debug.Log("[SetupUIManager] Setup elements hidden individually. Rage meter remains visible.");
        }

        // Stop following the player head during gameplay
        enabled = false;
    }

    public void Show()
    {
        if (setupPanel != null)
            setupPanel.SetActive(true);
        else
        {
            if (confirmButtonGO != null) confirmButtonGO.SetActive(true);
            if (resetButtonGO != null) resetButtonGO.SetActive(true);
            if (backgroundPanel != null) backgroundPanel.gameObject.SetActive(true);
            if (statusText != null) statusText.gameObject.SetActive(true);
        }

        enabled = true;
    }

    // ── Button Callbacks ──────────────────────────────────────────────────────

    public void OnConfirmPressed()
    {
        if (!confirmEnabled) return;
        if (Time.time - lastConfirmTime < pokeCooldown) return;

        lastConfirmTime = Time.time;
        Debug.Log("[SetupUIManager] Confirm pressed.");
        OnConfirmClicked?.Invoke();
    }

    public void OnResetPressed()
    {
        if (Time.time - lastResetTime < pokeCooldown) return;

        lastResetTime = Time.time;
        Debug.Log("[SetupUIManager] Reset pressed.");

        SetConfirmInteractable(true);
        SetStatus("Position reset.\n\nWalk to your play area.\nPinch or press to confirm.");
        SetPanelColor(readyColor);
        confirmEnabled = true;

        if (confirmButtonText != null)
            confirmButtonText.text = "CONFIRM POSITION";

        OnResetClicked?.Invoke();
    }

    private void OnConfirmPokeStateChanged(InteractableStateChangeArgs args)
    {
        if (args.NewState != InteractableState.Select) return;
        if (!confirmEnabled) return;
        if (Time.time - lastConfirmTime < pokeCooldown) return;

        lastConfirmTime = Time.time;
        Debug.Log("[SetupUIManager] Confirm button POKED.");
        OnConfirmClicked?.Invoke();
    }

    private void OnResetPokeStateChanged(InteractableStateChangeArgs args)
    {
        if (args.NewState != InteractableState.Select) return;
        if (Time.time - lastResetTime < pokeCooldown) return;

        lastResetTime = Time.time;
        Debug.Log("[SetupUIManager] Reset button POKED.");
        OnResetPressed();
    }

    // ── Internal Helpers ──────────────────────────────────────────────────────

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private void SetPanelColor(Color color)
    {
        if (backgroundPanel != null) backgroundPanel.color = color;
    }

    private void SetConfirmInteractable(bool interactable)
    {
        if (confirmButtonComponent != null)
            confirmButtonComponent.interactable = interactable;

        if (confirmPokeInteractable != null)
            confirmPokeInteractable.enabled = interactable;
    }

    private void SetResetInteractable(bool interactable)
    {
        if (resetButtonComponent != null)
            resetButtonComponent.interactable = interactable;

        if (resetPokeInteractable != null)
            resetPokeInteractable.enabled = interactable;
    }

    private void FollowPlayer()
    {
        if (playerCamera == null) return;

        Vector3 targetPos = CalculateCanvasPosition();
        transform.position = Vector3.Lerp(
            transform.position, targetPos, Time.deltaTime * 5f);

        Vector3 look = playerCamera.transform.position - transform.position;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(-look.normalized);
    }

    private void SnapInFrontOfPlayer()
    {
        if (playerCamera == null) return;

        currentDistance = rayModeDistance;
        transform.position = CalculateCanvasPosition();

        Vector3 look = playerCamera.transform.position - transform.position;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(-look.normalized);
    }

    private Vector3 CalculateCanvasPosition()
    {
        // Check hand proximity to switch between ray and poke mode
        float closestHandDist = float.MaxValue;

        var cameraRig = FindAnyObjectByType<OVRCameraRig>();
        if (cameraRig != null && cameraRig.trackingSpace != null)
        {
            Transform ts = cameraRig.trackingSpace;

            Vector3 rLocal = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
            Vector3 rWorld = ts.TransformPoint(rLocal);
            closestHandDist = Mathf.Min(closestHandDist,
                Vector3.Distance(rWorld, transform.position));

            Vector3 lLocal = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
            Vector3 lWorld = ts.TransformPoint(lLocal);
            closestHandDist = Mathf.Min(closestHandDist,
                Vector3.Distance(lWorld, transform.position));
        }

        bool shouldPoke = closestHandDist < pokeSwitchDistance;
        if (shouldPoke != inPokeMode)
        {
            inPokeMode = shouldPoke;
            Debug.Log($"[SetupUIManager] Switched to {(inPokeMode ? "POKE" : "RAY")} mode.");
        }

        float targetDist = inPokeMode ? pokeModeDistance : rayModeDistance;
        currentDistance = Mathf.Lerp(currentDistance, targetDist,
            Time.deltaTime * distanceSmoothSpeed);

        Vector3 forward = playerCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        return playerCamera.transform.position
             + forward * currentDistance
             + Vector3.up * heightOffset;
    }
}