using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

/// <summary>
/// Manages the waves of breakable objects in the Rage Room.
///
/// SPAWN STRATEGY — REUSING FindSpawnPositions
/// ─────────────────────────────────────────────
/// Rather than reimplementing room position logic, this script drives the
/// existing Meta MRUK FindSpawnPositions component directly — the same
/// component already used by SpawnManager for room layout spawning.
///
/// FindSpawnPositions already handles:
///   - Random positions on floor surfaces (SpawnLocation.OnTopOfSurfaces)
///   - Overlap checking so objects do not intersect each other or furniture
///   - Wall clearance via SurfaceClearanceDistance
///   - Correct height placement so objects sit flush on the surface
///   - Cleanup of previously spawned objects via ClearSpawnedPrefabs()
///
/// ObjectWaveManager simply sets the prefab and amount on FindSpawnPositions,
/// calls StartSpawn(room), then tracks which objects were spawned so it can
/// detect when all of them have been broken.
///
/// WAVE RULES
/// ──────────
/// - Each wave spawns objectsPerWave objects via FindSpawnPositions.
/// - When ALL objects in the wave are broken, the next wave spawns after a pause.
/// - Waves continue indefinitely.
///
/// PREFAB LIST
/// ───────────
/// Assign any number of breakable object prefabs to breakablePrefabs[].
/// Each prefab MUST have a DestructibleObject component.
/// A random prefab is chosen for each wave from the list.
/// Duplicate entries to increase the spawn probability of a specific prefab.
///
/// SETUP
/// ─────
/// 1. Attach this script to the GameManagers GameObject.
/// 2. Assign findSpawnPositions — drag the FindSpawnPositions component here.
///    This is the same FindSpawnPositions used by SpawnManager, or a dedicated
///    one configured specifically for breakable objects.
/// 3. Assign breakablePrefabs[] with your destructible object prefabs.
/// 4. Assign rageMeter.
/// 5. MRUK must be in the scene.
///
/// FINDSPAWNPOSITIONS INSPECTOR SETTINGS (configure on the component itself)
/// ──────────────────────────────────────────────────────────────────────────
///   SpawnLocations    → OnTopOfSurfaces  (places on floor and furniture tops)
///   CheckOverlaps     → true             (prevents objects overlapping)
///   SpawnOnStart      → None             (ObjectWaveManager controls spawning)
///   SpawnAmount       → (ignored — ObjectWaveManager sets this at runtime)
/// </summary>
public class ObjectWaveManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static ObjectWaveManager Instance { get; private set; }

    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("MRUK Spawner")]
    [Tooltip("The FindSpawnPositions component that handles all room position logic. " +
             "This reuses Meta's built-in floor placement, overlap checking, and " +
             "surface detection — no custom position logic needed. " +
             "On the FindSpawnPositions component itself set: " +
             "SpawnLocations = OnTopOfSurfaces, CheckOverlaps = true, SpawnOnStart = None.")]
    [SerializeField] private FindSpawnPositions findSpawnPositions;

    [Header("Breakable Object Prefabs")]
    [Tooltip("List of breakable object prefabs to spawn each wave. " +
             "Every prefab MUST have a DestructibleObject component. " +
             "One prefab is chosen at random each wave. " +
             "Duplicate entries to increase spawn probability of a specific prefab.")]
    [SerializeField] private GameObject[] breakablePrefabs;

    [Header("Wave Settings")]
    [Tooltip("Number of breakable objects to spawn per wave. " +
             "This value is passed directly to FindSpawnPositions.SpawnAmount " +
             "at runtime so you only need to set it here.")]
    [SerializeField] private int objectsPerWave = 5;

    [Tooltip("Seconds before unreachable objects are auto-cleared and wave advances. " +
             "Set 0 to disable timeout.")]
    [SerializeField] private float waveTimeoutSeconds = 20f;

    [Tooltip("Seconds of pause between the last object breaking and the next wave spawning.")]
    [SerializeField] private float wavePauseDelay = 1.5f;

    [Header("References")]
    [Tooltip("The player's RageMeter. Injected into each spawned " +
             "DestructibleObject at runtime — no need to pre-assign in the prefab.")]
    [SerializeField] private RageMeter rageMeter;

    [Header("Audio FX")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Sound played at the start of each new wave.")]
    [SerializeField] private AudioClip waveStartClip;

    [Tooltip("Sound played when all objects in a wave have been broken.")]
    [SerializeField] private AudioClip waveCompleteClip;

    [Header("Flow Control")]
    [Tooltip("Set TRUE when GameFlowManager is in the scene. " +
             "Prevents ObjectWaveManager from auto-spawning on MRUK load. " +
             "GameFlowManager will call StartFirstWaveManual() instead. " +
             "Set FALSE for standalone use without GameFlowManager.")]
    [SerializeField] private bool controlledByFlowManager = true;

    [Header("Debug")]
    [Tooltip("Log wave and spawn information to the Console.")]
    [SerializeField] private bool debugLogging = true;

    // ── Runtime State ─────────────────────────────────────────────────────────

    /// <summary>Current wave number. Starts at 0, increments each SpawnWave().</summary>
    public int WaveNumber { get; private set; } = 0;

    /// <summary>Number of objects broken in the current wave.</summary>
    public int BrokenCount { get; private set; } = 0;

    /// <summary>Number of objects still alive in the current wave.</summary>
    public int RemainingCount => activeObjects.Count;

    /// <summary>True while a wave is actively running.</summary>
    public bool WaveActive { get; private set; } = false;

    /// <summary>
    /// Live list of DestructibleObjects still active in the current wave.
    /// Populated by scanning FindSpawnPositions.SpawnedObjects after each spawn.
    /// </summary>
    private List<DestructibleObject> activeObjects = new();
    private bool waveAdvancing = false;
    private int registeredThisWave = 0; // actual objects spawned (may be less than objectsPerWave)

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Enforce singleton — only one ObjectWaveManager should exist per player
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // ── Validate FindSpawnPositions reference ─────────────────────────────
        if (findSpawnPositions == null)
        {
            Debug.LogError("[ObjectWaveManager] FindSpawnPositions not assigned! " +
                           "Drag a FindSpawnPositions component into the inspector slot.");
            return;
        }

        // ── Validate breakable prefabs ────────────────────────────────────────
        if (breakablePrefabs == null || breakablePrefabs.Length == 0)
        {
            Debug.LogError("[ObjectWaveManager] No breakable prefabs assigned! " +
                           "Drag your destructible object prefabs into 'Breakable Prefabs'.");
            return;
        }

        // Warn about any prefabs missing a DestructibleObject component
        // Better to catch this at startup than discover it silently mid-game
        for (int i = 0; i < breakablePrefabs.Length; i++)
        {
            if (breakablePrefabs[i] == null)
            {
                Debug.LogWarning($"[ObjectWaveManager] breakablePrefabs[{i}] is null. " +
                                 "Remove empty entries from the array.");
                continue;
            }

            if (breakablePrefabs[i].GetComponent<DestructibleObject>() == null)
                Debug.LogWarning($"[ObjectWaveManager] Prefab '{breakablePrefabs[i].name}' " +
                                 $"at index [{i}] is missing a DestructibleObject component. " +
                                 "It will be skipped if selected.");
        }

        // ── Configure FindSpawnPositions ──────────────────────────────────────
        // Disable auto-spawning on start — ObjectWaveManager controls all spawning.
        // SpawnOnStart must be None so FindSpawnPositions does not spawn on its own.
        findSpawnPositions.SpawnOnStart = MRUK.RoomFilter.None;

        // ── Flow control gate ─────────────────────────────────────────────────
        // When controlledByFlowManager is true, GameFlowManager calls
        // StartFirstWaveManual() after the player confirms position.
        // We must NOT register the MRUK callback here in that case —
        // otherwise ObjectWaveManager spawns immediately on MRUK load
        // before the player has confirmed their position.
        if (controlledByFlowManager)
        {
            Debug.Log("[ObjectWaveManager] Controlled by GameFlowManager. " +
                      "Waiting for StartFirstWaveManual() call. " +
                      "Will NOT auto-register MRUK callback.");
            return; // EXIT — GameFlowManager calls StartFirstWaveManual() later
        }

        // ── Auto-start (only when NOT controlled by GameFlowManager) ──────────
        // Register MRUK callback so wave starts automatically on scene load.
        // Only reaches here when controlledByFlowManager = false.
        Debug.Log("[ObjectWaveManager] Auto-start mode — registering MRUK callback.");
        if (MRUK.Instance != null)
            MRUK.Instance.RegisterSceneLoadedCallback(StartFirstWave);
        else
            StartFirstWave();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by DestructibleObject.Break() each time an object is destroyed.
    /// Removes the object from active tracking and checks if the wave is complete.
    /// </summary>
    /// <param name="obj">The DestructibleObject that was just destroyed.</param>
    public void OnObjectBroken(DestructibleObject obj)
    {
        activeObjects.Remove(obj);
        BrokenCount++;

        if (debugLogging)
            Debug.Log($"[ObjectWaveManager] Wave {WaveNumber}: " +
                      $"{BrokenCount}/{objectsPerWave} broken, " +
                      $"{activeObjects.Count} remaining.");

        // Primary condition: activeObjects list is empty = wave done.
        // This is the single source of truth. BrokenCount is just a stat.
        // Using activeObjects.Count == 0 means the wave advances correctly
        // whether objects were broken by the player OR vanished by other means.
        CheckWaveComplete("OnObjectBroken");
    }

    /// <summary>
    /// Adds a prefab to the breakable list at runtime.
    /// Useful for unlocking new object types at higher rage levels.
    /// </summary>
    public void AddBreakablePrefab(GameObject prefab)
    {
        if (prefab == null) return;

        if (prefab.GetComponent<DestructibleObject>() == null)
        {
            Debug.LogWarning($"[ObjectWaveManager] '{prefab.name}' has no DestructibleObject.");
            return;
        }

        // Extend the array by one entry
        var newArray = new GameObject[breakablePrefabs.Length + 1];
        breakablePrefabs.CopyTo(newArray, 0);
        newArray[breakablePrefabs.Length] = prefab;
        breakablePrefabs = newArray;

        if (debugLogging)
            Debug.Log($"[ObjectWaveManager] Added '{prefab.name}'. " +
                      $"Total prefabs: {breakablePrefabs.Length}");
    }

    /// <summary>
    /// Forces the current wave to end immediately and starts the next one.
    /// All remaining active objects are destroyed via FindSpawnPositions.ClearSpawnedPrefabs().
    /// </summary>
    public void ForceNextWave()
    {
        // Use FindSpawnPositions' own cleanup so its internal list stays consistent
        findSpawnPositions.ClearSpawnedPrefabs();
        activeObjects.Clear();

        StartCoroutine(StartNextWaveAfterDelay());
        Debug.Log("[ObjectWaveManager] Wave forced to end.");
    }

    // ── Internal Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Entry point for the very first wave.
    /// Called once MRUK confirms scene data is available via callback.
    /// </summary>
    private void StartFirstWave()
    {
        if (debugLogging)
            Debug.Log("[ObjectWaveManager] MRUK ready. Starting first wave.");
        StartCoroutine(SpawnWave());
    }

    /// <summary>
    /// Public entry point for the first wave.
    /// Called by GameFlowManager after the player has confirmed their
    /// position so objects spawn in the correct location.
    /// Use this instead of the MRUK callback when GameFlowManager
    /// is controlling the game flow sequence.
    /// </summary>
    public void StartFirstWaveManual()
    {
        if (debugLogging)
            Debug.Log("[ObjectWaveManager] Manual start triggered by GameFlowManager.");
        StartCoroutine(SpawnWave());
    }

    /// <summary>
    /// Plays the wave complete sound, waits wavePauseDelay seconds,
    /// then spawns the next wave.
    /// </summary>
    /// <summary>
    /// Single point of wave completion logic.
    /// Uses activeObjects.Count == 0 as the only condition.
    /// The waveAdvancing guard prevents double-triggering.
    /// </summary>
    private void CheckWaveComplete(string caller)
    {
        if (waveAdvancing) return;
        if (activeObjects.Count > 0) return;

        waveAdvancing = true;
        Debug.Log($"[ObjectWaveManager] Wave {WaveNumber} complete " +
                  $"(triggered by {caller}). " +
                  $"{BrokenCount} broken. Starting next wave.");
        StartCoroutine(StartNextWaveAfterDelay());
    }

    /// <summary>
    /// Kills any remaining objects after waveTimeoutSeconds and advances the wave.
    /// Handles the case where objects spawn in unreachable positions.
    /// </summary>
    private IEnumerator WaveTimeout()
    {
        if (waveTimeoutSeconds <= 0f) yield break;

        yield return new WaitForSeconds(waveTimeoutSeconds);

        if (waveAdvancing) yield break;
        if (activeObjects.Count == 0) yield break;

        Debug.LogWarning($"[ObjectWaveManager] Wave {WaveNumber} TIMEOUT after " +
                         $"{waveTimeoutSeconds}s. {activeObjects.Count} objects still alive. " +
                         "Destroying unreachable objects and advancing wave.");

        // Destroy all remaining objects
        foreach (var obj in new System.Collections.Generic.List<DestructibleObject>(activeObjects))
        {
            if (obj != null && obj.gameObject != null)
                Destroy(obj.gameObject);
        }
        activeObjects.Clear();
        CheckWaveComplete("WaveTimeout");
    }

    /// <summary>
    /// Periodically checks if any tracked objects have been destroyed
    /// without calling OnObjectDestroyed (e.g. fell out of bounds, physics despawn).
    /// Cleans them up and triggers wave advance if all objects are gone.
    /// </summary>
    private IEnumerator MonitorActiveObjects()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);

            if (activeObjects.Count == 0) break;

            // Remove any null entries (objects destroyed without calling OnObjectDestroyed)
            int before = activeObjects.Count;
            activeObjects.RemoveAll(o => o == null || o.gameObject == null);
            int removed = before - activeObjects.Count;

            if (removed > 0)
            {
                BrokenCount += removed;
                Debug.LogWarning($"[ObjectWaveManager] Cleaned up {removed} objects " +
                                 "that were destroyed without notifying the wave manager. " +
                                 $"Total gone: {BrokenCount}/{objectsPerWave}");

                // Check if wave should advance now
                if (activeObjects.Count == 0)
                {
                    CheckWaveComplete("MonitorActiveObjects");
                    yield break;
                }
            }
        }
    }

    private IEnumerator StartNextWaveAfterDelay()
    {
        WaveActive = false;
        audioSource?.PlayOneShot(waveCompleteClip);

        if (debugLogging)
            Debug.Log($"[ObjectWaveManager] Wave {WaveNumber} complete. " +
                      $"Next wave in {wavePauseDelay}s.");

        yield return new WaitForSeconds(wavePauseDelay);
        StartCoroutine(SpawnWave());
    }

    /// <summary>
    /// Core wave spawning coroutine.
    ///
    /// HOW IT USES FindSpawnPositions
    /// ────────────────────────────────
    /// 1. ClearSpawnedPrefabs() destroys all objects from the previous wave
    ///    and clears FindSpawnPositions' internal SpawnedObjects list.
    /// 2. A random prefab is selected from breakablePrefabs[].
    /// 3. SpawnObject is set to the chosen prefab on FindSpawnPositions.
    /// 4. SpawnAmount is set to objectsPerWave.
    /// 5. StartSpawn(room) is called — FindSpawnPositions handles all position
    ///    logic: floor detection, overlap checking, wall clearance, height offset.
    /// 6. After spawning, we scan SpawnedObjects to get references to the new
    ///    GameObjects, extract their DestructibleObject components, inject the
    ///    RageMeter, and register them for wave completion tracking.
    ///
    /// NOTE ON MIXED PREFABS PER WAVE
    /// ────────────────────────────────
    /// FindSpawnPositions spawns one prefab type per StartSpawn() call.
    /// If you want different prefab types within the same wave, call
    /// StartSpawn() multiple times with different prefabs and split
    /// objectsPerWave across the calls. The current implementation
    /// uses one random prefab per wave for simplicity.
    /// </summary>
    private IEnumerator SpawnWave()
    {
        WaveNumber++;
        BrokenCount = 0;
        WaveActive = true;
        activeObjects.Clear();

        audioSource?.PlayOneShot(waveStartClip);

        if (debugLogging)
            Debug.Log($"[ObjectWaveManager] ── Wave {WaveNumber} starting " +
                      $"({objectsPerWave} objects) ──");

        // ── Step 1: Clear objects from the previous wave ──────────────────────
        // ClearSpawnedPrefabs() destroys all GameObjects in FindSpawnPositions'
        // internal SpawnedObjects list and clears the list itself.
        // This ensures no leftover objects from the previous wave remain.
        findSpawnPositions.ClearSpawnedPrefabs();

        // ── Step 2: Choose a random prefab for this wave ──────────────────────
        // Pick one prefab type for the entire wave.
        // To use mixed prefabs per wave, call StartSpawn() multiple times.
        GameObject chosenPrefab = GetRandomValidPrefab();
        if (chosenPrefab == null)
        {
            Debug.LogError("[ObjectWaveManager] No valid prefab found. " +
                           "Check that breakablePrefabs[] contains prefabs with " +
                           "DestructibleObject components.");
            yield break;
        }

        // ── Step 3: Configure FindSpawnPositions ──────────────────────────────
        // Set the prefab and amount directly on the component.
        // FindSpawnPositions reads these values when StartSpawn() is called.
        findSpawnPositions.SpawnObject = chosenPrefab;
        findSpawnPositions.SpawnAmount = objectsPerWave;

        // Force OnTopOfSurfaces so objects always spawn ON a surface (floor
        // or table) rather than Floating in mid-air which causes infinite falling.
        // This overrides whatever is set in the Inspector at runtime to guarantee
        // correct behaviour every wave regardless of Inspector configuration.
        findSpawnPositions.SpawnLocations =
            FindSpawnPositions.SpawnLocation.OnTopOfSurfaces;

        // Include both FLOOR and TABLE as valid spawn surfaces.
        // TABLE alone fails if no table is scanned; FLOOR alone ignores tables.
        // This combination uses whichever surfaces are available in the room.
        // Change to MRUKAnchor.SceneLabels.TABLE only if you exclusively want
        // table spawning and your room always has a scanned table.
        findSpawnPositions.Labels =
            MRUKAnchor.SceneLabels.FLOOR |
            MRUKAnchor.SceneLabels.TABLE;

        // ── Step 4: Get the current MRUK room ─────────────────────────────────
        // FindSpawnPositions.StartSpawn(room) needs the current room to query
        // floor positions and check bounds against the room geometry.
        MRUKRoom room = MRUK.Instance != null
            ? MRUK.Instance.GetCurrentRoom()
            : null;

        if (room == null)
        {
            Debug.LogWarning("[ObjectWaveManager] No MRUK room found. " +
                             "Objects may not spawn correctly without room data.");
        }

        // ── Step 5: Spawn via FindSpawnPositions ──────────────────────────────
        // This single call handles everything:
        //   - Generates random positions on floor surfaces
        // ── Step 6: Register all spawned objects ──────────────────────────────
        // Find all DestructibleObjects just instantiated — they are the ones
        // not already in activeObjects from a previous wave.
        int registered = 0;
        var allDestructibles = FindObjectsByType<DestructibleObject>(
            FindObjectsSortMode.None);

        foreach (var destructible in allDestructibles)
        {
            if (destructible == null) continue;
            if (activeObjects.Contains(destructible)) continue; // already tracked

            // Inject the RageMeter so hits are reported to the scoring system
            destructible.SetRageMeter(rageMeter);

            // Register for wave completion tracking
            activeObjects.Add(destructible);
            registered++;
        }

        if (debugLogging)
            Debug.Log($"[ObjectWaveManager] Wave {WaveNumber}: " +
                      $"spawned and registered {registered} objects " +
                      $"using '{chosenPrefab.name}'.");

        registeredThisWave = registered;

        // Warn if FindSpawnPositions could not fill the full wave
        if (registered < objectsPerWave)
            Debug.LogWarning($"[ObjectWaveManager] Wave {WaveNumber}: only {registered}/{objectsPerWave} " +
                             "objects registered. Wave will complete when these {registered} are broken. " +
                             "Increase MaxIterations on FindSpawnPositions or reduce objectsPerWave.");

        // If nothing spawned at all advance immediately
        if (registered == 0)
        {
            Debug.LogWarning("[ObjectWaveManager] Zero objects spawned — skipping wave.");
            CheckWaveComplete("ZeroSpawn");
        }
    }

    /// <summary>
    /// Picks a random prefab from breakablePrefabs[] that has a valid
    /// DestructibleObject component. Skips null or invalid entries.
    /// Returns null if no valid prefab exists in the array.
    /// </summary>
    private GameObject GetRandomValidPrefab()
    {
        if (breakablePrefabs == null || breakablePrefabs.Length == 0)
            return null;

        // Shuffle attempts to avoid getting stuck on a run of null entries
        // Try up to the full array length before giving up
        for (int attempt = 0; attempt < breakablePrefabs.Length * 2; attempt++)
        {
            GameObject candidate = breakablePrefabs[
                Random.Range(0, breakablePrefabs.Length)];

            if (candidate == null) continue;
            if (candidate.GetComponent<DestructibleObject>() == null) continue;

            return candidate; // Valid prefab found
        }

        return null;
    }
}