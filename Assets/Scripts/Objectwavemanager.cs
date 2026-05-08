using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

/// <summary>
/// Manages the waves of breakable objects in the Rage Room.
///
/// WAVE RULES
/// ──────────
/// - Each wave spawns exactly objectsPerWave objects at random positions in the MRUK room.
/// - The wave manager waits until ALL objects in the current wave have been broken.
/// - After a short pause (wavePauseDelay), the next wave spawns automatically.
/// - Waves continue indefinitely — there is no final wave.
///
/// SPAWN POSITIONS
/// ───────────────
/// Positions are generated using MRUK's GenerateRandomPositionInRoom().
/// A distance check ensures objects spawn within a comfortable reach of the player.
/// If MRUK is not available, a simple camera-relative fallback is used.
///
/// RAGE METER INJECTION
/// ─────────────────────
/// Breakable object prefabs do not need a RageMeter assigned in the prefab itself.
/// ObjectWaveManager calls SetRageMeter() on each spawned DestructibleObject at
/// runtime so all objects correctly report to the player's rage meter.
///
/// SINGLETON
/// ─────────
/// Accessed as ObjectWaveManager.Instance by DestructibleObject.Break().
///
/// SETUP
/// ─────
/// 1. Attach this script to a persistent GameManagers GameObject.
/// 2. Assign breakablePrefabs (one or more cube / prop prefabs with DestructibleObject).
/// 3. Assign rageMeter so it can be injected into spawned objects.
/// 4. MRUK must be in the scene — the manager waits for MRUK's SceneLoadedCallback
///    before spawning the first wave.
/// </summary>
public class ObjectWaveManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static ObjectWaveManager Instance { get; private set; }

    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("Wave Settings")]
    [Tooltip("Number of breakable objects spawned at the start of each wave.")]
    [SerializeField] private int objectsPerWave = 5;

    [Tooltip("Seconds of pause between the last object breaking and the next wave spawning. " +
             "Gives the player a moment to breathe before the next set appears.")]
    [SerializeField] private float wavePauseDelay = 1.5f;

    [Header("Spawn Settings")]
    [Tooltip("Array of breakable object prefabs. One is chosen at random for each spawn. " +
             "Every prefab must have a DestructibleObject component.")]
    [SerializeField] private GameObject[] breakablePrefabs;

    [Tooltip("Objects will not spawn closer than this distance (metres) to the player.")]
    [SerializeField] private float minSpawnDistance = 0.8f;

    [Tooltip("Objects will not spawn further than this distance (metres) from the player.")]
    [SerializeField] private float maxSpawnDistance = 3f;

    [Tooltip("How far above the floor objects are placed. " +
             "Adjust so objects appear at a comfortable striking height.")]
    [SerializeField] private float spawnHeightOffset = 1.0f;

    [Tooltip("Maximum number of individual position attempts before giving up on a single spawn. " +
             "Prevents infinite loops if the room has limited valid areas.")]
    [SerializeField] private int maxSpawnAttempts = 30;

    [Header("References")]
    [Tooltip("The player's RageMeter. Injected into each spawned DestructibleObject at runtime.")]
    [SerializeField] private RageMeter rageMeter;

    [Header("Audio FX")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Sound played at the start of each new wave.")]
    [SerializeField] private AudioClip waveStartClip;

    [Tooltip("Sound played when all objects in a wave have been broken.")]
    [SerializeField] private AudioClip waveCompleteClip;

    // ── Runtime State ─────────────────────────────────────────────────────────

    private int waveNumber = 0;  // Increments each time a new wave begins
    private int brokenCount = 0;  // Number of objects broken in the current wave

    /// <summary>Live list of DestructibleObjects still active in the current wave.</summary>
    private List<DestructibleObject> activeObjects = new();

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Enforce singleton pattern — only one wave manager should exist
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // MRUK scene data must be loaded before we can query room positions.
        // RegisterSceneLoadedCallback fires immediately if the scene is already loaded,
        // or fires once loading completes if it hasn't happened yet.
        if (MRUK.Instance != null)
            MRUK.Instance.RegisterSceneLoadedCallback(StartFirstWave);
        else
            // No MRUK in the scene — start right away with the camera-based fallback
            StartFirstWave();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by DestructibleObject.Break() each time an object is destroyed.
    /// Removes the object from the active list and increments the broken counter.
    /// When all objects in the wave are broken, kicks off the next wave after a delay.
    /// </summary>
    /// <param name="obj">The DestructibleObject that was just destroyed.</param>
    public void OnObjectBroken(DestructibleObject obj)
    {
        // Remove from active tracking — the object is about to be Destroyed
        activeObjects.Remove(obj);
        brokenCount++;

        Debug.Log($"[ObjectWaveManager] Wave {waveNumber}: {brokenCount}/{objectsPerWave} broken");

        // All objects in this wave are gone — begin next wave sequence
        if (brokenCount >= objectsPerWave)
            StartCoroutine(StartNextWaveAfterDelay());
    }

    // ── Internal Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Entry point for the very first wave — called once MRUK scene data is available.
    /// </summary>
    private void StartFirstWave()
    {
        StartCoroutine(SpawnWave());
    }

    /// <summary>
    /// Plays the wave complete sound, waits the configured pause, then spawns the next wave.
    /// </summary>
    private IEnumerator StartNextWaveAfterDelay()
    {
        audioSource?.PlayOneShot(waveCompleteClip);
        yield return new WaitForSeconds(wavePauseDelay);
        StartCoroutine(SpawnWave());
    }

    /// <summary>
    /// Spawns a full wave of objectsPerWave breakable objects.
    /// Each object is:
    ///   1. Placed at a random valid room position within player reach.
    ///   2. Assigned a RageMeter reference via SetRageMeter().
    ///   3. Tracked in the activeObjects list for wave completion detection.
    /// Spawning is spread over multiple frames (one yield per object) to avoid
    /// a single-frame performance spike when many objects appear at once.
    /// </summary>
    private IEnumerator SpawnWave()
    {
        waveNumber++;
        brokenCount = 0;
        activeObjects.Clear();

        audioSource?.PlayOneShot(waveStartClip);
        Debug.Log($"[ObjectWaveManager] Starting wave {waveNumber} ({objectsPerWave} objects)");

        // Get the current MRUK room for position generation (may be null in Editor)
        MRUKRoom room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;

        int spawned = 0;
        int attempts = 0;
        int maxTotal = maxSpawnAttempts * objectsPerWave; // Safety ceiling

        while (spawned < objectsPerWave && attempts < maxTotal)
        {
            attempts++;

            // Try to find a valid position for this object
            Vector3? spawnPos = GetRandomSpawnPosition(room);
            if (spawnPos == null) continue; // Position invalid — try again

            // Choose a random prefab from the array
            var prefab = breakablePrefabs[Random.Range(0, breakablePrefabs.Length)];

            // Spawn the object at the chosen position with a random rotation
            var obj = Instantiate(prefab, spawnPos.Value, Random.rotation);

            // Every spawned object must have a DestructibleObject component
            var destructible = obj.GetComponent<DestructibleObject>();
            if (destructible == null)
            {
                // Prefab is misconfigured — destroy it and skip
                Debug.LogWarning($"[ObjectWaveManager] Prefab '{prefab.name}' is missing " +
                                 "a DestructibleObject component. Skipping.");
                Destroy(obj);
                continue;
            }

            // Inject the RageMeter so the object can report hits
            destructible.SetRageMeter(rageMeter);

            // Track this object for wave completion counting
            activeObjects.Add(destructible);
            spawned++;

            // Yield so each object appears one frame apart — smoother performance
            yield return null;
        }

        if (spawned < objectsPerWave)
            Debug.LogWarning($"[ObjectWaveManager] Wave {waveNumber}: only spawned " +
                             $"{spawned}/{objectsPerWave} after {attempts} position attempts.");
    }

    /// <summary>
    /// Attempts to find a world-space spawn position that:
    ///   - Is inside the scanned MRUK room (uses MRUK's built-in random generator).
    ///   - Is between minSpawnDistance and maxSpawnDistance from the player's head.
    ///   - Is raised by spawnHeightOffset above the floor so objects float at strike height.
    ///
    /// Falls back to a simple camera-relative random offset if MRUK is unavailable.
    /// Returns null if no valid position could be found this attempt (caller retries).
    /// </summary>
    private Vector3? GetRandomSpawnPosition(MRUKRoom room)
    {
        // ── MRUK-based spawn ──────────────────────────────────────────────────
        if (room != null)
        {
            // GenerateRandomPositionInRoom returns a point on the floor inside the room.
            // minRadius pushes the point away from walls for stability.
            var floorPos = room.GenerateRandomPositionInRoom(minSpawnDistance, true);
            if (floorPos.HasValue)
            {
                // Lift the point above the floor
                Vector3 candidate = floorPos.Value + Vector3.up * spawnHeightOffset;

                // Measure horizontal distance from the player (ignore Y axis)
                if (Camera.main != null)
                {
                    float dist = Vector3.Distance(
                        new Vector3(candidate.x, 0, candidate.z),
                        new Vector3(Camera.main.transform.position.x, 0,
                                    Camera.main.transform.position.z));

                    // Reject positions that are too close or too far
                    if (dist >= minSpawnDistance && dist <= maxSpawnDistance)
                        return candidate;
                }
                else
                {
                    // No camera available — accept the MRUK position as-is
                    return candidate;
                }
            }
        }

        // ── Camera-relative fallback ──────────────────────────────────────────
        // Used when MRUK is not present (e.g. testing in the Unity Editor).
        if (Camera.main != null)
        {
            // Random horizontal offset in a ring between min and max distance
            Vector2 rnd = Random.insideUnitCircle.normalized
                           * Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 offset = new Vector3(rnd.x, spawnHeightOffset, rnd.y);
            return Camera.main.transform.position + offset;
        }

        // Could not generate any position
        return null;
    }
}