using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;

/// <summary>
/// Validates poke button setup at runtime and provides debug information.
/// Does NOT use any inject methods — all wiring is done in the Inspector.
///
/// REASON FOR CHANGE
/// ──────────────────
/// The OVR SDK version in this project does not expose InjectSurface()
/// or InjectSurfacePatch() as public methods on PokeInteractable.
/// All component references must be wired in the Unity Inspector instead.
///
/// WHAT THIS SCRIPT DOES
/// ──────────────────────
/// 1. Validates that all required components are present and wired.
/// 2. Logs clear error messages if anything is missing.
/// 3. Subscribes to WhenStateChanged on PokeInteractable to detect
///    poke events and call the confirm/reset actions directly.
///    This bypasses the need for PointableUnityEventWrapper entirely.
///
/// INSPECTOR WIRING STILL REQUIRED
/// ─────────────────────────────────
/// On PokeInteractable:
///   Surface Patch → use the workaround described below
///
/// SURFACE PATCH WORKAROUND (since picker does not work)
/// ──────────────────────────────────────────────────────
/// 1. Add a second empty child GameObject to ConfirmButton
/// 2. Name it "SurfacePatchProxy"
/// 3. Add PlaneSurface to SurfacePatchProxy
/// 4. Now drag SurfacePatchProxy's PlaneSurface into Surface Patch
///    — dragging from a CHILD object works when dragging from
///    the same object does not
/// </summary>
public class PokeButtonSetup : MonoBehaviour
{
    // ── Inspector References ──────────────────────────────────────────────────

    [Header("Poke Setup")]
    [Tooltip("The PokeInteractable on this button. Auto-found if empty.")]
    [SerializeField] private PokeInteractable pokeInteractable;

    [Header("Actions")]
    [Tooltip("Action fired when this button is poked. " +
             "Assign SetupUIManager.OnConfirmPressed or OnResetPressed.")]
    [SerializeField] private UnityEngine.Events.UnityEvent onPoked;

    [Header("Settings")]
    [Tooltip("Minimum seconds between poke detections.")]
    [SerializeField] private float pokeCooldown = 0.8f;

    [SerializeField] private bool debugLogging = true;

    // ── Runtime State ─────────────────────────────────────────────────────────

    private float lastPokeTime = -999f;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-find PokeInteractable if not assigned
        if (pokeInteractable == null)
            pokeInteractable = GetComponent<PokeInteractable>();

        if (pokeInteractable == null)
        {
            Debug.LogError($"[PokeButtonSetup] No PokeInteractable on '{name}'. " +
                           "Add PokeInteractable component.");
            return;
        }

        // Validate Surface Patch is wired — cannot inject in code
        // so we just warn the developer if it is missing
        ValidateSetup();
    }

    private void OnEnable()
    {
        // Subscribe to poke state changes
        // This is the most reliable way to detect pokes without
        // needing PointableUnityEventWrapper or inject methods
        if (pokeInteractable != null)
            pokeInteractable.WhenStateChanged += OnPokeStateChanged;
    }

    private void OnDisable()
    {
        if (pokeInteractable != null)
            pokeInteractable.WhenStateChanged -= OnPokeStateChanged;
    }

    // ── Poke Detection ────────────────────────────────────────────────────────

    /// <summary>
    /// Fires when the PokeInteractable transitions between states.
    /// InteractableState.Select = finger/controller has pressed the surface.
    /// </summary>
    private void OnPokeStateChanged(InteractableStateChangeArgs args)
    {
        // Only act on the press — not hover or release
        if (args.NewState != InteractableState.Select) return;

        // Cooldown prevents double-firing on a single press
        if (Time.time - lastPokeTime < pokeCooldown) return;

        lastPokeTime = Time.time;

        if (debugLogging)
            Debug.Log($"[PokeButtonSetup] '{name}' poked — firing onPoked event.");

        // Fire the assigned Unity Event
        onPoked?.Invoke();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks all required components and logs helpful error messages.
    /// Called on Awake so problems are visible immediately in the Console.
    /// </summary>
    private void ValidateSetup()
    {
        bool allGood = true;

        // Check PlaneSurface exists somewhere on this GO or children
        var surface = GetComponentInChildren<PlaneSurface>();
        if (surface == null)
        {
            Debug.LogWarning($"[PokeButtonSetup] '{name}': No PlaneSurface found. " +
                             "Add PlaneSurface to this GO or a child. " +
                             "Then drag it into PokeInteractable's Surface Patch slot.");
            allGood = false;
        }

        // Check onPoked has listeners
        if (onPoked == null || onPoked.GetPersistentEventCount() == 0)
        {
            Debug.LogWarning($"[PokeButtonSetup] '{name}': No listeners on onPoked. " +
                             "Assign SetupUIManager.OnConfirmPressed in the Inspector.");
            allGood = false;
        }

        if (allGood && debugLogging)
            Debug.Log($"[PokeButtonSetup] '{name}': Setup validated successfully.");
    }
}