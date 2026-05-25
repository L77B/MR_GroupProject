using Meta.XR.MRUtilityKit;
using UnityEngine;

/// <summary>
/// Central coordinator for the Rage Room — two-player mode.
///
/// THE INSPECTOR ASSIGNMENT PROBLEM — AND WHY THIS CODE FIXES IT
/// ──────────────────────────────────────────────────────────────
/// Unity's object picker cannot visually distinguish two components of the
/// same type on the same GameObject. Both slots show "GameManager (Rage Meter)"
/// and dragging from the picker often assigns the same component to both slots.
///
/// This version of Awake() ALWAYS re-validates by playerIndex at runtime,
/// even when Inspector slots are already assigned. It does a scene-wide search
/// for all RageMeter components, checks playerIndex on each, and overwrites
/// whatever the Inspector set if the indices do not match.
///
/// This means the Inspector assignment is a visual hint only — the code self-
/// corrects at startup. You will see a log confirming which component ended
/// up in each slot so you can verify it worked.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Player Rage Meters")]
    [Tooltip("RageMeter with playerIndex = 0. " +
             "Even if set wrong in the Inspector, Awake() corrects it by playerIndex.")]
    public RageMeter rageMeterP1;

    [Tooltip("RageMeter with playerIndex = 1. " +
             "Even if set wrong in the Inspector, Awake() corrects it by playerIndex.")]
    public RageMeter rageMeterP2;

    [Header("Core Systems")]
    public WeaponRack weaponRack;
    public ObjectWaveManager waveManager;
    public SpawnManager spawnManager;

    [Header("Dual Bar UI")]
    [Tooltip("DualRageBarUI component on the HUD Canvas. Auto-found if not assigned.")]
    [SerializeField] private DualRageBarUI dualBar;

    [Header("Weapon Unlock Mode")]
    [Tooltip("If true, weapon unlocks trigger when EITHER player reaches the tier. " +
             "If false, BOTH players must reach it.")]
    [SerializeField] private bool unlockOnEitherPlayer = true;

    // ── Compatibility alias ───────────────────────────────────────────────────

    /// <summary>Legacy alias so any script using GameManager.rageMeter still compiles.</summary>
    public RageMeter rageMeter => rageMeterP1;

    // ── Shared unlock event ───────────────────────────────────────────────────

    /// <summary>
    /// WeaponRack subscribes here instead of to a single RageMeter.
    /// GameManager fires this after applying the two-player unlock gate.
    /// </summary>
    public static event System.Action<RageLevelConfig.RageLevel, int> OnSharedRageLevelUp;

    // ── Read-only State ───────────────────────────────────────────────────────

    public bool GameStarted { get; private set; }

    // ── Cached event lambdas (needed to unsubscribe correctly) ────────────────

    // Lambda references must be stored so OnDestroy can unsubscribe the same instance.
    // Anonymous lambdas in Start() cannot be unsubscribed — they create new instances each time.
    private System.Action<RageLevelConfig.RageLevel, int> _p1LevelUpHandler;
    private System.Action<RageLevelConfig.RageLevel, int> _p2LevelUpHandler;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // ── ALWAYS validate RageMeter slots by playerIndex ────────────────────
        //
        // WHY: Unity's picker assigns the same component to both slots when two
        // components have the same type and name. We cannot rely on Inspector
        // assignment being correct. Instead, we ALWAYS scan all RageMeter
        // components on this GameObject and match them by playerIndex.
        //
        // This runs unconditionally — it overwrites whatever the Inspector set.
        // After this block, rageMeterP1.playerIndex is guaranteed to be 0
        // and rageMeterP2.playerIndex is guaranteed to be 1 (if both exist).

        RageMeter foundP1 = null;
        RageMeter foundP2 = null;

        // Search all RageMeter components on THIS GameObject first
        // (both meters should be on GameManager)
        var metersOnThis = GetComponents<RageMeter>();
        foreach (var m in metersOnThis)
        {
            if (m.playerIndex == 0) foundP1 = m;
            if (m.playerIndex == 1) foundP2 = m;
        }

        // If not found on this GameObject, search the whole scene
        // (handles the case where meters are on separate GameObjects)
        if (foundP1 == null || foundP2 == null)
        {
            var allMeters = FindObjectsByType<RageMeter>(FindObjectsSortMode.None);
            foreach (var m in allMeters)
            {
                if (m.playerIndex == 0 && foundP1 == null) foundP1 = m;
                if (m.playerIndex == 1 && foundP2 == null) foundP2 = m;
            }
        }

        // Apply found references — overwrites Inspector assignment
        if (foundP1 != null) rageMeterP1 = foundP1;
        if (foundP2 != null) rageMeterP2 = foundP2;

        // ── Diagnostic log — confirms exactly what is in each slot ────────────
        // Read this in Console or via adb logcat to verify P1 ≠ P2
        Debug.Log($"[GameManager] RageMeter slot validation:" +
                  $"\n  P1 slot → playerIndex={rageMeterP1?.playerIndex.ToString() ?? "NULL"}" +
                  $"  GetInstanceID={rageMeterP1?.GetInstanceID().ToString() ?? "NULL"}" +
                  $"\n  P2 slot → playerIndex={rageMeterP2?.playerIndex.ToString() ?? "NULL"}" +
                  $"  GetInstanceID={rageMeterP2?.GetInstanceID().ToString() ?? "NULL"}");

        if (rageMeterP1 != null && rageMeterP2 != null &&
            rageMeterP1.GetInstanceID() == rageMeterP2.GetInstanceID())
        {
            Debug.LogError("[GameManager] P1 and P2 slots point to the SAME RageMeter component! " +
                           "Add a second RageMeter component to GameManager and set its " +
                           "playerIndex to 1. Both components must have different playerIndex values.");
        }
        else if (rageMeterP1 != null && rageMeterP2 != null)
        {
            Debug.Log("[GameManager] P1 and P2 RageMeter components are DIFFERENT — correct.");
        }

        // ── Auto-find other systems ───────────────────────────────────────────
        if (weaponRack == null) weaponRack = FindAnyObjectByType<WeaponRack>();
        if (waveManager == null) waveManager = FindAnyObjectByType<ObjectWaveManager>();
        if (spawnManager == null) spawnManager = FindAnyObjectByType<SpawnManager>();
        if (dualBar == null) dualBar = FindAnyObjectByType<DualRageBarUI>();
    }

    private void Start()
    {
        if (rageMeterP1 == null)
        {
            Debug.LogError("[GameManager] RageMeter P1 (playerIndex=0) not found! " +
                           "Add a RageMeter component to GameManager and set playerIndex = 0.");
            return;
        }

        // Store lambdas so we can unsubscribe the same instance in OnDestroy
        _p1LevelUpHandler = (level, index) => OnAnyPlayerLevelUp(0, level, index);
        _p2LevelUpHandler = (level, index) => OnAnyPlayerLevelUp(1, level, index);

        rageMeterP1.OnRageLevelUp += _p1LevelUpHandler;

        if (rageMeterP2 != null)
            rageMeterP2.OnRageLevelUp += _p2LevelUpHandler;
        else
            Debug.LogWarning("[GameManager] RageMeter P2 (playerIndex=1) not found. " +
                             "Add a second RageMeter component and set its playerIndex = 1.");

        GameStarted = true;
        Debug.Log("[GameManager] Game started — two-player mode. " +
                  $"P1 instanceID={rageMeterP1.GetInstanceID()}  " +
                  $"P2 instanceID={rageMeterP2?.GetInstanceID().ToString() ?? "none"}");
    }

    private void OnDestroy()
    {
        // Use stored lambda references — unsubscribing anonymous lambdas created
        // inline in Start() does nothing because they are different instances.
        if (rageMeterP1 != null && _p1LevelUpHandler != null)
            rageMeterP1.OnRageLevelUp -= _p1LevelUpHandler;

        if (rageMeterP2 != null && _p2LevelUpHandler != null)
            rageMeterP2.OnRageLevelUp -= _p2LevelUpHandler;
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    private void OnAnyPlayerLevelUp(int playerIdx,
                                    RageLevelConfig.RageLevel level,
                                    int levelIndex)
    {
        Debug.Log($"[GameManager] P{playerIdx + 1} reached rage level " +
                  $"{levelIndex}: '{level.levelName}'");

        if (unlockOnEitherPlayer)
        {
            OnSharedRageLevelUp?.Invoke(level, levelIndex);
        }
        else
        {
            // Require BOTH players to have reached at least this tier
            int p1Level = rageMeterP1?.CurrentLevelIndex ?? 0;
            int p2Level = rageMeterP2?.CurrentLevelIndex ?? 0;
            int minLevel = Mathf.Min(p1Level, p2Level);

            if (minLevel >= levelIndex)
                OnSharedRageLevelUp?.Invoke(level, levelIndex);
            else
                Debug.Log($"[GameManager] Weapon unlock gated — " +
                          $"P{(p1Level < p2Level ? 1 : 2)} at level {minLevel}, " +
                          $"need {levelIndex}.");
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns the RageMeter for a given player index (0 = P1, 1 = P2).</summary>
    public RageMeter GetMeterForPlayer(int playerIdx) =>
        playerIdx == 0 ? rageMeterP1 : rageMeterP2;

    /// <summary>Returns the index of the player currently leading in rage.</summary>
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