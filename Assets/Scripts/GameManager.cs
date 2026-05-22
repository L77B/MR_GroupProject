using Meta.XR.MRUtilityKit;
using UnityEngine;

/// <summary>
/// Central coordinator — extended for two-player mode.
///
/// TWO-PLAYER CHANGES FROM SINGLE-PLAYER VERSION
/// ───────────────────────────────────────────────
/// 1. rageMeterP1 and rageMeterP2 replace the single rageMeter field.
///    The old public rageMeter field is kept as a compatibility alias
///    pointing to rageMeterP1 for any scripts that still reference it.
///
/// 2. Weapon unlocks are now driven by the HIGHER of the two players' rage
///    levels. Either player reaching level 2 unlocks the paintball gun for
///    both — the weapon rack is shared. You can change this to require BOTH
///    players to reach the level by changing the GetLeadLevelIndex() logic.
///
/// 3. ObjectWaveManager.rageMeter is injected as rageMeterP1 by default.
///    When a DestructibleObject is hit by P2's bat, it calls SetRageMeter(p2)
///    via BatImpactHandler — DestructibleObject must call the correct meter
///    based on which player's bat struck it (see DestructibleObject notes).
///
/// SETUP STEPS
/// ────────────
/// 1. On the GameManager GameObject:
///    - Add a second RageMeter component
///    - Set its playerIndex to 1
///    - Assign it to the rageMeterP2 field below
/// 2. Add DualRageBarUI to the HUD Canvas and assign both meters.
/// 3. Assign weaponRack — it subscribes to OnRageLevelUp from the lead player.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Player Rage Meters")]
    [Tooltip("RageMeter for Player 1 (playerIndex = 0). " +
             "Also exposed as 'rageMeter' for legacy script compatibility.")]
    public RageMeter rageMeterP1;

    [Tooltip("RageMeter for Player 2 (playerIndex = 1). " +
             "Add a second RageMeter component to this GameObject and assign it here.")]
    public RageMeter rageMeterP2;

    [Header("Core Systems")]
    public WeaponRack weaponRack;
    public ObjectWaveManager waveManager;
    public SpawnManager spawnManager;

    [Header("Dual Bar UI")]
    [Tooltip("The DualRageBarUI component on the HUD Canvas. " +
             "Auto-found if not assigned.")]
    [SerializeField] private DualRageBarUI dualBar;

    [Header("Weapon Unlock Mode")]
    [Tooltip("If true, weapon unlocks trigger when EITHER player reaches the tier. " +
             "If false, BOTH players must reach it (more challenging).")]
    [SerializeField] private bool unlockOnEitherPlayer = true;

    // ── Compatibility alias ───────────────────────────────────────────────────

    /// <summary>
    /// Legacy alias — returns rageMeterP1.
    /// Scripts that reference GameManager.rageMeter still compile without change.
    /// </summary>
    public RageMeter rageMeter => rageMeterP1;

    // ── Read-only State ───────────────────────────────────────────────────────

    public bool GameStarted { get; private set; }

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-find components if not wired in Inspector
        if (rageMeterP1 == null)
        {
            // Find the meter with playerIndex = 0
            var meters = FindObjectsByType<RageMeter>(FindObjectsSortMode.None);
            foreach (var m in meters)
            {
                if (m.playerIndex == 0 && rageMeterP1 == null) rageMeterP1 = m;
                if (m.playerIndex == 1 && rageMeterP2 == null) rageMeterP2 = m;
            }
        }

        if (weaponRack == null) weaponRack = FindAnyObjectByType<WeaponRack>();
        if (waveManager == null) waveManager = FindAnyObjectByType<ObjectWaveManager>();
        if (spawnManager == null) spawnManager = FindAnyObjectByType<SpawnManager>();
        if (dualBar == null) dualBar = FindAnyObjectByType<DualRageBarUI>();
    }

    private void Start()
    {
        if (rageMeterP1 == null)
        {
            Debug.LogError("[GameManager] RageMeter P1 not found!");
            return;
        }

        // Subscribe to level-up events from both meters
        rageMeterP1.OnRageLevelUp += (level, index) => OnAnyPlayerLevelUp(0, level, index);

        if (rageMeterP2 != null)
            rageMeterP2.OnRageLevelUp += (level, index) => OnAnyPlayerLevelUp(1, level, index);
        else
            Debug.LogWarning("[GameManager] RageMeter P2 not assigned — " +
                             "add a second RageMeter component with playerIndex=1.");

        GameStarted = true;
        Debug.Log("[GameManager] Game started — two-player mode.");
    }

    private void OnDestroy()
    {
        if (rageMeterP1 != null)
            rageMeterP1.OnRageLevelUp -= (level, index) => OnAnyPlayerLevelUp(0, level, index);
        if (rageMeterP2 != null)
            rageMeterP2.OnRageLevelUp -= (level, index) => OnAnyPlayerLevelUp(1, level, index);
    }

    // ── Shared unlock event ───────────────────────────────────────────────────

    /// <summary>
    /// Subscribe WeaponRack to this event in WeaponRack.Start():
    ///   GameManager.OnSharedRageLevelUp += HandleRageLevelUp;
    /// GameManager fires it after applying the unlock gate.
    /// WeaponRack.HandleRageLevelUp() guards against duplicate unlocks
    /// via slotUnlocked[], so firing from two meters is safe.
    /// </summary>
    public static event System.Action<RageLevelConfig.RageLevel, int> OnSharedRageLevelUp;

    // ── Event Handlers ────────────────────────────────────────────────────────

    private void OnAnyPlayerLevelUp(int playerIdx,
                                    RageLevelConfig.RageLevel level,
                                    int levelIndex)
    {
        Debug.Log($"[GameManager] P{playerIdx + 1} reached rage level " +
                  $"{levelIndex}: '{level.levelName}'");

        if (unlockOnEitherPlayer)
        {
            // Fire immediately — WeaponRack handles duplicate guard internally
            OnSharedRageLevelUp?.Invoke(level, levelIndex);
        }
        else
        {
            // Require BOTH players to have reached at least this level
            int p1Level = rageMeterP1?.CurrentLevelIndex ?? 0;
            int p2Level = rageMeterP2?.CurrentLevelIndex ?? 0;
            int minLevel = Mathf.Min(p1Level, p2Level);

            if (minLevel >= levelIndex)
                OnSharedRageLevelUp?.Invoke(level, levelIndex);
            else
                Debug.Log($"[GameManager] Unlock gated — " +
                          $"P{(p1Level < p2Level ? 1 : 2)} at level {minLevel}, " +
                          $"need {levelIndex}.");
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the RageMeter for a given player index (0 or 1).
    /// Used by BatImpactHandler and DestructibleObject to pick the right meter.
    /// </summary>
    public RageMeter GetMeterForPlayer(int playerIdx)
    {
        return playerIdx == 0 ? rageMeterP1 : rageMeterP2;
    }

    /// <summary>
    /// Returns the index of the player with the higher current rage.
    /// Used for UI highlights or announcements.
    /// </summary>
    public int GetLeadPlayerIndex()
    {
        float r1 = rageMeterP1?.CurrentRage ?? 0f;
        float r2 = rageMeterP2?.CurrentRage ?? 0f;
        return r1 >= r2 ? 0 : 1;
    }

    public void RestartGame()
    {
        Debug.Log("[GameManager] Restarting...");
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}