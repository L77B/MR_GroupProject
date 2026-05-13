using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Oculus.Interaction;

/// <summary>
/// Manages the setup UI shown during GameFlowManager Phase 2.
///
/// INTERACTION METHODS SUPPORTED
/// ───────────────────────────────
/// 1. POKE  — Player physically pushes the button with their finger
///            or controller tip. Uses OVR PokeInteractable.
///            Most natural for MR — feels like pressing a real button.
///
/// 2. RAY   — Player points controller laser at button and pulls trigger.
///            Uses OVR RayInteractable + Unity UI Button + OVR Raycaster.
///            Good fallback when hands are not tracked.
///
/// 3. HAND  — Player uses bare hand tracking to poke the button.
///            Works automatically when Poke is set up correctly —
///            Meta's hand tracking drives the same PokeInteractor.
///
/// HOW POKE INTERACTION WORKS
/// ───────────────────────────
/// Each button needs:
///   - A PokeInteractable component (defines the pokeable surface)
///   - A RoundedBoxPokeButtonVisual (optional — animates button press)
///   - A PointableUnityEventWrapper to fire Unity events on poke select
///
/// The PokeInteractor lives on the controller/hand inside OVRCameraRig.
/// When the finger/controller tip enters the PokeInteractable's surface
/// plane, it fires WhenStateChanged → Select, which we treat as a button press.
///
/// SETUP IN UNITY
/// ───────────────
/// See full setup guide at the bottom of this file.
/// </summary>
public class SetupUIManager : MonoBehaviour
{
    // ── Inspector References ──────────────────────────────────────────────────

    [Header("UI Elements")]
    [Tooltip("Main status text showing current phase message.")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Tooltip("Background panel image — colour changes per phase.")]
    [SerializeField] private Image backgroundPanel;

    [Header("Confirm Button")]
    [Tooltip("The confirm button GameObject. " +
             "Must have PokeInteractable for poke/hand interaction " +
             "AND Button component for ray interaction.")]
    [SerializeField] private GameObject confirmButtonGO;

    [Tooltip("Unity Button component on the confirm button — for ray interaction.")]
    [SerializeField] private Button confirmButtonComponent;

    [Tooltip("Text label on the confirm button.")]
    [SerializeField] private TextMeshProUGUI confirmButtonText;

    [Tooltip("PokeInteractable on the confirm button — for poke/hand interaction. " +
             "Add via Add Component → Oculus → Interaction → PokeInteractable.")]
    [SerializeField] private PokeInteractable confirmPokeInteractable;

    [Header("Reset Button")]
    [Tooltip("The reset button GameObject.")]
    [SerializeField] private GameObject resetButtonGO;

    [Tooltip("Unity Button component on the reset button — for ray interaction.")]
    [SerializeField] private Button resetButtonComponent;

    [Tooltip("Text label on the reset button.")]
    [SerializeField] private TextMeshProUGUI resetButtonText;

    [Tooltip("PokeInteractable on the reset button — for poke/hand interaction.")]
    [SerializeField] private PokeInteractable resetPokeInteractable;

    [Header("Phase Colours")]
    [SerializeField] private Color waitingColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);
    [SerializeField] private Color readyColor = new Color(0.05f, 0.35f, 0.05f, 0.95f);
    [SerializeField] private Color confirmedColor = new Color(0.05f, 0.05f, 0.45f, 0.95f);

    [Header("Positioning")]
    [Tooltip("Distance in front of the player (metres).")]
    [SerializeField] private float forwardDistance = 1.2f;

    [Tooltip("Height relative to eye level (metres). Negative = below eye level.")]
    [SerializeField] private float heightOffset = -0.1f;

    [Header("Poke Cooldown")]
    [Tooltip("Minimum seconds between poke detections to prevent double-firing.")]
    [SerializeField] private float pokeCooldown = 1.0f;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the player confirms their position via any interaction.</summary>
    public System.Action OnConfirmClicked;

    /// <summary>Fired when the player resets their position via any interaction.</summary>
    public System.Action OnResetClicked;

    // ── Runtime State ─────────────────────────────────────────────────────────

    private Camera playerCamera;
    private bool confirmEnabled = false; // Guards against presses during wrong phase
    private float lastConfirmTime = -999f;
    private float lastResetTime = -999f;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        playerCamera = Camera.main;

        // ── Wire Unity Button onClick (Ray interaction) ───────────────────────
        if (confirmButtonComponent != null)
            confirmButtonComponent.onClick.AddListener(OnConfirmPressed);

        if (resetButtonComponent != null)
            resetButtonComponent.onClick.AddListener(OnResetPressed);

        // ── Wire PokeInteractable WhenStateChanged (Poke / Hand interaction) ──
        // WhenStateChanged fires when a PokeInteractor enters/exits/selects.
        // We listen for the Select state which means the finger pressed the button.
        if (confirmPokeInteractable != null)
            confirmPokeInteractable.WhenStateChanged += OnConfirmPokeStateChanged;

        if (resetPokeInteractable != null)
            resetPokeInteractable.WhenStateChanged += OnResetPokeStateChanged;
    }

    private void Start()
    {
        SetWaitingState();
        SnapInFrontOfPlayer();
    }

    private void LateUpdate()
    {
        FollowPlayer();
    }

    private void OnDestroy()
    {
        // Unsubscribe all listeners
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

    /// <summary>Phase 1: Room scanning — disable confirm button.</summary>
    public void SetWaitingState()
    {
        confirmEnabled = false;
        SetStatus("Scanning your room...\nPlease wait.");
        SetPanelColor(waitingColor);
        SetConfirmInteractable(false);

        if (confirmButtonText != null)
            confirmButtonText.text = "Please Wait...";
    }

    /// <summary>Phase 2: Room ready — enable confirm button.</summary>
    public void SetReadyState()
    {
        confirmEnabled = true;
        SetStatus("Room scanned! ✓\n\n" +
                  "Walk to your play area.\n\n" +
                  "Poke, point or press:\n" +
                  "CONFIRM POSITION");
        SetPanelColor(readyColor);
        SetConfirmInteractable(true);

        if (confirmButtonText != null)
            confirmButtonText.text = "✓  CONFIRM POSITION";
    }

    /// <summary>Phase 3: Confirmed — disable all buttons.</summary>
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

    /// <summary>Hide the panel when game starts.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>Show the panel.</summary>
    public void Show()
    {
        gameObject.SetActive(true);
    }

    // ── Button / Poke Callbacks ───────────────────────────────────────────────

    /// <summary>
    /// Called by Unity Button onClick (ray interaction).
    /// </summary>
    public void OnConfirmPressed()
    {
        if (!confirmEnabled) return;
        if (Time.time - lastConfirmTime < pokeCooldown) return;

        lastConfirmTime = Time.time;
        Debug.Log("[SetupUIManager] Confirm pressed (ray/click).");
        OnConfirmClicked?.Invoke();
    }

    /// <summary>
    /// Called by Unity Button onClick (ray interaction).
    /// </summary>
    public void OnResetPressed()
    {
        if (Time.time - lastResetTime < pokeCooldown) return;

        lastResetTime = Time.time;
        Debug.Log("[SetupUIManager] Reset pressed (ray/click).");

        SetConfirmInteractable(true);
        SetStatus("Position reset.\n\nWalk to your play area.\nPoke or point to confirm.");
        SetPanelColor(readyColor);
        confirmEnabled = true;

        if (confirmButtonText != null)
            confirmButtonText.text = "✓  CONFIRM POSITION";

        OnResetClicked?.Invoke();
    }

    /// <summary>
    /// Called by PokeInteractable WhenStateChanged on the CONFIRM button.
    /// Fires when a finger or controller tip physically presses the button surface.
    ///
    /// InteractableState.Select = the poke interactor has fully pressed the surface.
    /// This is equivalent to a button click in poke interaction.
    /// </summary>
    private void OnConfirmPokeStateChanged(InteractableStateChangeArgs args)
    {
        // Only act on the Select transition — finger pressing in, not releasing
        if (args.NewState != InteractableState.Select) return;
        if (!confirmEnabled) return;
        if (Time.time - lastConfirmTime < pokeCooldown) return;

        lastConfirmTime = Time.time;
        Debug.Log("[SetupUIManager] Confirm button POKED.");
        OnConfirmClicked?.Invoke();
    }

    /// <summary>
    /// Called by PokeInteractable WhenStateChanged on the RESET button.
    /// </summary>
    private void OnResetPokeStateChanged(InteractableStateChangeArgs args)
    {
        if (args.NewState != InteractableState.Select) return;
        if (Time.time - lastResetTime < pokeCooldown) return;

        lastResetTime = Time.time;
        Debug.Log("[SetupUIManager] Reset button POKED.");
        OnResetPressed(); // Reuse the same logic as ray reset
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
        // Unity Button (ray)
        if (confirmButtonComponent != null)
            confirmButtonComponent.interactable = interactable;

        // PokeInteractable (poke/hand) — enable/disable the whole GO
        // Disabling prevents phantom pokes during wrong phase
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

        Vector3 forward = playerCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 target =
            playerCamera.transform.position
            + forward * forwardDistance
            + Vector3.up * heightOffset;

        transform.position = Vector3.Lerp(
            transform.position, target, Time.deltaTime * 5f);

        Vector3 look = playerCamera.transform.position - transform.position;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(-look.normalized);
    }

    private void SnapInFrontOfPlayer()
    {
        if (playerCamera == null) return;

        Vector3 forward = playerCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        transform.position =
            playerCamera.transform.position
            + forward * forwardDistance
            + Vector3.up * heightOffset;

        Vector3 look = playerCamera.transform.position - transform.position;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(-look.normalized);
    }
}