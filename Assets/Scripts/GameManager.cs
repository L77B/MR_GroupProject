using Meta.XR.MRUtilityKit;
using UnityEngine;

/// <summary>
/// Central coordinator for the Rage Room game.
///
/// RESPONSIBILITIES
/// ────────────────
/// - Holds references to all major subsystems so they can be reached from one place.
/// - Auto-finds subsystems at startup if they were not assigned in the Inspector.
/// - Subscribes to RageMeter events for any game-level reactions (logging, future
///   multiplayer announcements, etc.).
/// - Exposes a RestartGame() method that can be called from a UI button or from
///   the ESP32 button via WebSocketClientExample.
///
/// SINGLE PLAYER NOW / MULTIPLAYER LATER
/// ──────────────────────────────────────
/// Currently wired for one player. To support two players, duplicate the
/// RageMeter, WeaponRack, and ObjectWaveManager components (or GameObjects)
/// and assign them to a second GameManager instance (or extend this class
/// with Player 1 / Player 2 reference pairs).
///
/// SETUP
/// ─────
/// Attach this script to the GameManagers empty GameObject alongside
/// RageMeter, ObjectWaveManager, and any other persistent managers.
/// SpawnManager can be anywhere in the scene — it will be found automatically.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ── Inspector References ──────────────────────────────────────────────────

    [Header("Core Systems")]
    [Tooltip("The player's rage meter. Drives scoring, level-ups, and weapon unlocks. " +
             "Auto-found if not assigned.")]
    public RageMeter rageMeter;

    [Tooltip("The weapon rack in the scene. Manages weapon slots and unlock events. " +
             "Auto-found if not assigned.")]
    public WeaponRack weaponRack;

    [Tooltip("Manages object wave spawning and break counting. " +
             "Auto-found if not assigned.")]
    public ObjectWaveManager waveManager;

    [Tooltip("Handles prefab cycling via MRUK FindSpawnPositions. " +
             "Used by the ESP32 button (WebSocket) to spawn the next room layout. " +
             "Auto-found if not assigned.")]
    public SpawnManager spawnManager;

    // ── Read-only State ───────────────────────────────────────────────────────

    /// <summary>True once Start() has completed successfully.</summary>
    public bool GameStarted { get; private set; }

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-find any subsystems that were not manually wired in the Inspector.
        // This is a convenience fallback — explicit Inspector assignment is preferred
        // because it avoids the overhead of a scene-wide search.
        if (rageMeter == null) rageMeter = FindAnyObjectByType<RageMeter>();
        if (weaponRack == null) weaponRack = FindAnyObjectByType<WeaponRack>();
        if (waveManager == null) waveManager = FindAnyObjectByType<ObjectWaveManager>();
        if (spawnManager == null) spawnManager = FindAnyObjectByType<SpawnManager>();
    }

    private void Start()
    {
        // Validate that the essential rage meter is present before continuing
        if (rageMeter == null)
        {
            Debug.LogError("[GameManager] RageMeter not found in the scene! " +
                           "Make sure a RageMeter component exists.");
            return;
        }

        // Listen for rage level-up events — used here for logging and as a hook
        // for future multiplayer announcements or scene changes
        rageMeter.OnRageLevelUp += OnRageLevelUp;

        GameStarted = true;
        Debug.Log("[GameManager] Game started — single player mode.");
    }

    private void OnDestroy()
    {
        // Always clean up event subscriptions to prevent callbacks on destroyed objects
        if (rageMeter != null)
            rageMeter.OnRageLevelUp -= OnRageLevelUp;
    }

    // ── Bat Placement ─────────────────────────────────────────────────────────

    /// <summary>
    /// Places the baseball bat on the nearest floor or table surface
    /// so the player can walk to it and pick it up naturally.
    /// Called once after MRUK scene data is loaded.
    /// </summary>
    private void PlaceBatOnStart()
    {
        if (weaponRack == null)
        {
            Debug.LogWarning("[GameManager] WeaponRack not found — cannot place bat.");
            return;
        }

        // PlaceBatNearPlayer() tries table first, falls back to floor
        // 0.8m in front of the player if no table is available in the room
        weaponRack.PlaceBatNearPlayer();
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by RageMeter whenever the player reaches a new rage tier.
    /// Currently logs the event. Extend this to trigger multiplayer sync,
    /// environmental effects, or announcements.
    /// </summary>
    private void OnRageLevelUp(RageLevelConfig.RageLevel level, int levelIndex)
    {
        Debug.Log($"[GameManager] Player reached rage level {levelIndex}: '{level.levelName}'");

        // Future: broadcast to second player, trigger room effects, etc.
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reloads the current scene from scratch, resetting all game state.
    /// Can be called from:
    ///   - A UI button in the scene.
    ///   - WebSocketClientExample.IncomingMessageParser() for ESP32 button restart.
    ///   - Any other external trigger.
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("[GameManager] Restarting game...");
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}