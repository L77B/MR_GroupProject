using System.Collections;
using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Controls the exact sequence of events from scene load to gameplay.
///
/// CORRECT GAME FLOW
/// ──────────────────
///
/// Phase 1 — SETUP (automatic, no spawning yet)
///   Scene loads → MRUK scans the physical room
///   SetupUIManager shows "Scanning room..." panel
///
/// Phase 2 — ORIGIN CONFIRMATION (player action required)
///   MRUK scan complete → SetupUIManager shows "Confirm Position" button
///   Player walks to their play area
///   Player points controller ray at button and pulls trigger
///   SetupUIManager.OnConfirmClicked fires
///   RoomOriginManager.SetOriginToPlayerPosition() is called
///
/// Phase 3 — SPAWN (happens only after origin is confirmed)
///   WeaponRack places bat near player
///   SpawnManager spawns default weapon
///   ObjectWaveManager spawns first wave of breakable objects
///   SetupUIManager hides → Game begins
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    // ── Inspector References ──────────────────────────────────────────────────

    [Header("Core Systems")]
    [SerializeField] private RoomOriginManager roomOriginManager;
    [SerializeField] private SpawnManager spawnManager;
    [SerializeField] private ObjectWaveManager waveManager;
    [SerializeField] private WeaponRack weaponRack;

    [Header("Setup UI")]
    [Tooltip("The SetupUIManager on the setup Canvas. " +
             "Handles all UI display and button events.")]
    [SerializeField] private SetupUIManager setupUI;

    [Header("Timing")]
    [Tooltip("Seconds to wait after origin is set before spawning begins.")]
    [SerializeField] private float spawnDelay = 1.0f;

    [Header("MRUK Timeout")]
    [Tooltip("If MRUK does not fire SceneLoaded within this many seconds, " +
             "proceed to Phase 2 anyway. Prevents the game getting stuck " +
             "when running via Meta Quest Link or in Editor without full Scene API. " +
             "Set to 0 to disable the timeout.")]
    [SerializeField] private float mrukTimeoutSeconds = 10f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    // ── Game Phase ────────────────────────────────────────────────────────────

    public enum GamePhase
    {
        WaitingForMRUK,     // Phase 1: MRUK scanning
        WaitingForOrigin,   // Phase 2: Player must confirm position via UI button
        Spawning,           // Transition: spawning objects
        Playing             // Phase 3: Game running
    }

    public GamePhase CurrentPhase { get; private set; } = GamePhase.WaitingForMRUK;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-find all references
        if (roomOriginManager == null)
            roomOriginManager = FindAnyObjectByType<RoomOriginManager>();
        if (spawnManager == null)
            spawnManager = FindAnyObjectByType<SpawnManager>();
        if (waveManager == null)
            waveManager = FindAnyObjectByType<ObjectWaveManager>();
        if (weaponRack == null)
            weaponRack = FindAnyObjectByType<WeaponRack>();
        if (setupUI == null)
            setupUI = FindAnyObjectByType<SetupUIManager>();
    }

    private void Start()
    {
        // ── Subscribe to UI button events ─────────────────────────────────────
        // SetupUIManager fires these when the player clicks the buttons
        if (setupUI != null)
        {
            setupUI.OnConfirmClicked += OnPlayerConfirmedPosition;
            setupUI.OnResetClicked += OnPlayerResetPosition;
            setupUI.SetWaitingState();
        }
        else
        {
            Debug.LogWarning("[GameFlowManager] SetupUIManager not found. " +
                             "UI buttons will not work. " +
                             "Add SetupUIManager to the setup Canvas.");
        }

        // Phase 1: Wait for MRUK to finish scanning
        // Nothing spawns until the player confirms via the UI button
        if (MRUK.Instance != null)
        {
            MRUK.Instance.RegisterSceneLoadedCallback(OnMRUKReady);

            // Safety timeout — if MRUK does not fire the callback within
            // mrukTimeoutSeconds (e.g. running via Link with limited Scene API),
            // proceed to Phase 2 anyway so the game is not stuck forever.
            StartCoroutine(MRUKLoadTimeout());
        }
        else
        {
            Debug.LogWarning("[GameFlowManager] MRUK not found. " +
                             "Skipping to origin confirmation.");
            OnMRUKReady();
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from UI events to prevent memory leaks
        if (setupUI != null)
        {
            setupUI.OnConfirmClicked -= OnPlayerConfirmedPosition;
            setupUI.OnResetClicked -= OnPlayerResetPosition;
        }
    }

    /// <summary>
    /// Safety timeout for MRUK scene loading.
    /// If MRUK does not fire SceneLoadedCallback within mrukTimeoutSeconds,
    /// proceed to Phase 2 anyway so the game does not get stuck.
    /// This handles Meta Quest Link mode where Scene API may be limited.
    /// </summary>
    private System.Collections.IEnumerator MRUKLoadTimeout()
    {
        if (mrukTimeoutSeconds <= 0f) yield break;

        yield return new WaitForSeconds(mrukTimeoutSeconds);

        // Only fire timeout if still waiting for MRUK
        if (CurrentPhase == GamePhase.WaitingForMRUK)
        {
            Debug.LogWarning($"[GameFlowManager] MRUK did not load within " +
                             $"{mrukTimeoutSeconds}s. Proceeding anyway. " +
                             "This is normal when running via Meta Quest Link " +
                             "or in Editor without full Scene API support. " +
                             "Build and Run to the headset for full MRUK functionality.");
            OnMRUKReady();
        }
    }

    // ── Phase Transitions ─────────────────────────────────────────────────────

    /// <summary>
    /// Phase 1 complete — MRUK has finished scanning the room.
    /// Enable the confirm button so the player can set their position.
    /// Nothing is spawned yet.
    /// </summary>
    private void OnMRUKReady()
    {
        CurrentPhase = GamePhase.WaitingForOrigin;

        if (debugLogging)
            Debug.Log("[GameFlowManager] MRUK ready. " +
                      "Waiting for player to confirm position via UI button.");

        // Tell the UI to show the ready state with the confirm button enabled
        if (setupUI != null)
            setupUI.SetReadyState();
    }

    /// <summary>
    /// Called when the player clicks the Confirm Position button in the UI.
    /// Sets the spawn origin to the player's current position then
    /// spawns all objects in the correct sequence.
    /// </summary>
    private void OnPlayerConfirmedPosition()
    {
        // Ignore if not in the correct phase
        if (CurrentPhase != GamePhase.WaitingForOrigin) return;

        if (debugLogging)
            Debug.Log("[GameFlowManager] Player confirmed position via UI button.");

        StartCoroutine(ConfirmOriginAndSpawn());
    }

    /// <summary>
    /// Called when the player clicks the Reset button in the UI.
    /// Resets the origin so they can reposition before confirming.
    /// </summary>
    private void OnPlayerResetPosition()
    {
        if (debugLogging)
            Debug.Log("[GameFlowManager] Player reset position via UI button.");

        if (roomOriginManager != null)
            roomOriginManager.ResetOrigin();
    }

    /// <summary>
    /// Phase 2 complete — spawns everything in the correct order after
    /// the player has confirmed their position via the UI button.
    /// </summary>
    private IEnumerator ConfirmOriginAndSpawn()
    {
        CurrentPhase = GamePhase.Spawning;

        // Update UI to confirmed state
        if (setupUI != null)
            setupUI.SetConfirmedState();

        // ── Step 1: Origin management ─────────────────────────────────────────
        // RoomOriginManager.autocentreOnLoad handles origin on scene load.
        // If autocentreOnLoad is OFF (fixed installation), no offset is applied
        // and objects spawn at the tracking origin which matches the guardian.
        // If autocentreOnLoad is ON, it already ran when MRUK loaded.
        // We call SetOriginToPlayerPosition() only if explicitly needed.
        if (roomOriginManager != null && roomOriginManager.AutocentreOnLoad)
        {
            // Autocentre is on — re-centre on player's confirmed position
            roomOriginManager.SetOriginToPlayerPosition();
            Debug.Log("[GameFlowManager] Origin set to player confirmed position.");
        }
        else
        {
            Debug.Log("[GameFlowManager] Using fixed origin — no offset applied. " +
                      "Guardian boundary and spawns should be aligned.");
        }

        // Wait for MRUK TrackingSpaceOffset to propagate
        yield return new WaitForSeconds(0.3f);

        // ── Step 2: Place bat near player ─────────────────────────────────────
        if (weaponRack != null)
            weaponRack.PlaceBatNearPlayer();

        yield return new WaitForSeconds(spawnDelay * 0.5f);

        // ── Step 3: Spawn bat via SpawnManager at index 0 ────────────────────────
        // SpawnManager.SpawnByIndex(0) places the bat via FindSpawnPositions
        // at a valid surface near the player so it is always reachable.
        if (spawnManager != null)
        {
            spawnManager.SpawnByIndex(0);
            Debug.Log("[GameFlowManager] SpawnManager spawned bat at index 0.");
        }
        else
        {
            Debug.LogWarning("[GameFlowManager] SpawnManager null — bat not spawned via SpawnByIndex.");
        }

        yield return new WaitForSeconds(spawnDelay);

        // ── Step 4: Start first wave of breakable objects ─────────────────────
        if (waveManager != null)
            waveManager.StartFirstWaveManual();

        // ── Step 5: Hide setup UI and start playing ───────────────────────────
        if (setupUI != null)
            setupUI.Hide();

        CurrentPhase = GamePhase.Playing;

        if (debugLogging)
            Debug.Log("[GameFlowManager] Game is now playing.");
    }
}