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

        // ── Wait for MRUK scene data before spawning ──────────────────────────
        // RegisterSceneLoadedCallback fires immediately if already loaded,
        // or fires once loading completes otherwise.
        if (MRUK.Instance != null)
            MRUK.Instance.RegisterSceneLoadedCallback(StartFirstWave);
        else
            StartFirstWave(); // No MRUK — start immediately (Editor fallback)
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

        // All objects broken — begin the next wave sequence
        if (BrokenCount >= objectsPerWave)
            StartCoroutine(StartNextWaveAfterDelay());
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
    /// Called once MRUK confirms scene data is available.
    /// </summary>
    private void StartFirstWave()
    {
        if (debugLogging)
            Debug.Log("[ObjectWaveManager] MRUK ready. Starting first wave.");
        StartCoroutine(SpawnWave());
    }

    /// <summary>
    /// Plays the wave complete sound, waits wavePauseDelay seconds,
    /// then spawns the next wave.
    /// </summary>
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
        //   - Checks overlaps with existing physics objects
        //   - Respects wall clearance distance
        //   - Places objects at the correct height so they sit on the surface
        //   - Instantiates up to SpawnAmount objects
        if (room != null)
            findSpawnPositions.StartSpawn(room);
        else
            findSpawnPositions.StartSpawn(); // Fallback: tries all available rooms

        // ── Step 6: Wait one frame for instantiation to complete ──────────────
        // StartSpawn() is synchronous but we yield one frame to ensure all
        // GameObjects are fully initialised before we try to access their components.
        yield return null;

        // ── Step 7: Register spawned objects ─────────────────────────────────
        // SpawnedObjects is FindSpawnPositions' IReadOnlyList of everything
        // it just instantiated. We iterate it to wire up each object.
        int registered = 0;
        foreach (GameObject spawnedObj in findSpawnPositions.SpawnedObjects)
        {
            if (spawnedObj == null) continue;

            var destructible = spawnedObj.GetComponent<DestructibleObject>();
            if (destructible == null)
            {
                Debug.LogWarning($"[ObjectWaveManager] Spawned object '{spawnedObj.name}' " +
                                 "has no DestructibleObject component. " +
                                 "It will not count toward wave completion.");
                continue;
            }

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

        // Warn if FindSpawnPositions could not fill the full wave
        // (e.g. room too small, too many overlaps)
        if (registered < objectsPerWave)
            Debug.LogWarning($"[ObjectWaveManager] Wave {WaveNumber}: only registered " +
                             $"{registered}/{objectsPerWave} objects. " +
                             "The room may be too small or maxIterations too low on " +
                             "the FindSpawnPositions component.");
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