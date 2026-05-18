using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

/// <summary>
/// Manages waves of breakable objects in the Rage Room.
///
/// ROOT CAUSE OF THE 0/N BUG — SDK SOURCE ANALYSIS
/// ─────────────────────────────────────────────────
/// After reading FindSpawnPositions.cs directly, two bugs were found:
///
/// BUG 1 — Missing else/continue in FindSpawnPositions.StartSpawn()
///   When GenerateRandomPositionOnSurface() returns false (no surface found),
///   the SDK code has no else branch — it falls through with spawnPosition
///   still at Vector3.zero. That zero position then fails IsPositionInRoom()
///   and loops all MaxIterations doing nothing, placing 0 objects silently.
///   This is an SDK bug we cannot patch — so we bypass StartSpawn() entirely.
///
/// BUG 2 — ClearSpawnedPrefabs + CheckOverlaps in the same frame
///   Destroy() is deferred to end of frame. If StartSpawn() is called in the
///   same frame as ClearSpawnedPrefabs(), the old wave's colliders are still
///   live in the physics engine and CheckOverlaps rejects every candidate.
///   Fixed by yielding one frame after clearing before spawning.
///
/// SOLUTION — CALL GenerateRandomPositionOnSurface DIRECTLY
/// ──────────────────────────────────────────────────────────
/// We call MRUKRoom.GenerateRandomPositionOnSurface() ourselves, check its
/// bool return value correctly, and Instantiate prefabs directly. This is
/// the same underlying path FindSpawnPositions uses, but without the SDK bug.
/// FindSpawnPositions is still kept as a reference for ClearSpawnedPrefabs()
/// compatibility — we just no longer call StartSpawn() on it.
///
/// SETUP
/// ─────
/// 1. Attach to the GameManagers GameObject alongside SpawnManager.
/// 2. findSpawnPositions: keep assigned — used only for ClearSpawnedPrefabs().
///    SpawnOnStart MUST be None on that component.
/// 3. breakablePrefabs[]: all entries must have DestructibleObject.
/// 4. rageMeter: injected into each object at spawn time.
/// 5. MRUK must be in the scene and room scan must complete before wave 1.
/// </summary>
public class ObjectWaveManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static ObjectWaveManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("MRUK Spawner")]
    [Tooltip("The FindSpawnPositions component for breakables. " +
             "SpawnOnStart must be None. Used only for ClearSpawnedPrefabs(). " +
             "Actual spawning now bypasses StartSpawn() due to an SDK bug " +
             "where a false return from GenerateRandomPositionOnSurface() " +
             "causes all iterations to fail silently with 0 objects placed.")]
    [SerializeField] private FindSpawnPositions findSpawnPositions;

    [Header("Breakable Object Prefabs")]
    [Tooltip("Every entry must have a DestructibleObject component. " +
             "Entries without one are skipped. Duplicate entries raise " +
             "the probability of that prefab being chosen.")]
    [SerializeField] private GameObject[] breakablePrefabs;

    [Header("Wave Settings")]
    [Tooltip("How many breakable objects to spawn per wave.")]
    [SerializeField] private int objectsPerWave = 3;

    [Tooltip("Seconds before unreachable objects are cleared and wave advances. " +
             "Set 0 to disable.")]
    [SerializeField] private float waveTimeoutSeconds = 20f;

    [Tooltip("Pause in seconds between last object breaking and next wave.")]
    [SerializeField] private float wavePauseDelay = 1.5f;

    [Tooltip("Max placement attempts per object. 1000 is safe. " +
             "Reduce if waves start noticeably slowly in large rooms.")]
    [SerializeField] private int maxSpawnIterations = 1000;

    [Tooltip("Clearance from surface edges needed for a valid spawn. " +
             "0.1m is correct. Raise only if objects clip into walls.")]
    [SerializeField] private float surfaceClearanceDistance = 0.1f;

    [Tooltip("Physics overlap check before placing each object. " +
             "Prevents objects spawning inside furniture or each other. " +
             "Safe to enable — one frame delay after clear prevents false rejects.")]
    [SerializeField] private bool checkOverlaps = true;

    [Header("References")]
    [Tooltip("Injected into each DestructibleObject at spawn time.")]
    [SerializeField] private RageMeter rageMeter;

    [Header("Audio FX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip waveStartClip;
    [SerializeField] private AudioClip waveCompleteClip;

    [Header("Flow Control")]
    [Tooltip("True = GameFlowManager calls StartFirstWaveManual() after " +
             "player confirms position. False = auto-start on MRUK load.")]
    [SerializeField] private bool controlledByFlowManager = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    // ── Runtime State ─────────────────────────────────────────────────────────

    public int WaveNumber { get; private set; } = 0;
    public int BrokenCount { get; private set; } = 0;
    public int RemainingCount => activeObjects.Count;
    public bool WaveActive { get; private set; } = false;

    private List<DestructibleObject> activeObjects = new();
    private List<GameObject> spawnedThisWave = new();
    private bool waveAdvancing = false;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (breakablePrefabs == null || breakablePrefabs.Length == 0)
        {
            Debug.LogError("[ObjectWaveManager] No breakable prefabs assigned! " +
                           "Add prefabs with DestructibleObject to the array.");
            return;
        }

        for (int i = 0; i < breakablePrefabs.Length; i++)
        {
            if (breakablePrefabs[i] == null)
            {
                Debug.LogWarning($"[ObjectWaveManager] breakablePrefabs[{i}] is null.");
                continue;
            }
            if (breakablePrefabs[i].GetComponent<DestructibleObject>() == null)
                Debug.LogWarning($"[ObjectWaveManager] Prefab '{breakablePrefabs[i].name}' " +
                                 $"at index [{i}] has no DestructibleObject — will be skipped.");
        }

        // Disable auto-spawning on FindSpawnPositions — we control all spawning.
        if (findSpawnPositions != null)
            findSpawnPositions.SpawnOnStart = MRUK.RoomFilter.None;

        if (controlledByFlowManager)
        {
            Debug.Log("[ObjectWaveManager] Controlled by GameFlowManager. " +
                      "Waiting for StartFirstWaveManual().");
            return;
        }

        // Auto-start mode (no GameFlowManager)
        if (MRUK.Instance != null)
            MRUK.Instance.RegisterSceneLoadedCallback(AutoStart);
        else
            AutoStart();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by GameFlowManager after the player confirms their play position.
    /// Objects will spawn in the correct location relative to the player.
    /// </summary>
    public void StartFirstWaveManual()
    {
        Debug.Log("[ObjectWaveManager] Manual start triggered by GameFlowManager.");
        StartCoroutine(SpawnWave());
    }

    /// <summary>
    /// Called by DestructibleObject.Break() each time an object is destroyed.
    /// </summary>
    public void OnObjectBroken(DestructibleObject obj)
    {
        activeObjects.Remove(obj);
        spawnedThisWave.Remove(obj != null ? obj.gameObject : null);
        BrokenCount++;

        if (debugLogging)
            Debug.Log($"[ObjectWaveManager] Wave {WaveNumber}: " +
                      $"{BrokenCount} broken, {activeObjects.Count} remaining.");

        CheckWaveComplete("OnObjectBroken");
    }

    /// <summary>Adds a breakable prefab at runtime (e.g. rage-level unlock).</summary>
    public void AddBreakablePrefab(GameObject prefab)
    {
        if (prefab == null) return;
        if (prefab.GetComponent<DestructibleObject>() == null)
        {
            Debug.LogWarning($"[ObjectWaveManager] '{prefab.name}' has no DestructibleObject.");
            return;
        }
        var arr = new GameObject[breakablePrefabs.Length + 1];
        breakablePrefabs.CopyTo(arr, 0);
        arr[breakablePrefabs.Length] = prefab;
        breakablePrefabs = arr;
        Debug.Log($"[ObjectWaveManager] Added '{prefab.name}'. Total: {breakablePrefabs.Length}");
    }

    /// <summary>Immediately ends the current wave and starts the next.</summary>
    public void ForceNextWave()
    {
        ClearWaveObjects();
        StartCoroutine(StartNextWaveAfterDelay());
        Debug.Log("[ObjectWaveManager] Wave force-ended.");
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void AutoStart()
    {
        Debug.Log("[ObjectWaveManager] MRUK ready — auto-starting first wave.");
        StartCoroutine(SpawnWave());
    }

    private void CheckWaveComplete(string caller)
    {
        if (waveAdvancing) return;
        if (activeObjects.Count > 0) return;
        waveAdvancing = true;
        Debug.Log($"[ObjectWaveManager] Wave {WaveNumber} complete ({caller}). " +
                  $"{BrokenCount} broken.");
        StartCoroutine(StartNextWaveAfterDelay());
    }

    private IEnumerator StartNextWaveAfterDelay()
    {
        WaveActive = false;
        audioSource?.PlayOneShot(waveCompleteClip);
        if (debugLogging)
            Debug.Log($"[ObjectWaveManager] Next wave in {wavePauseDelay}s.");
        yield return new WaitForSeconds(wavePauseDelay);
        StartCoroutine(SpawnWave());
    }

    private void ClearWaveObjects()
    {
        foreach (var go in spawnedThisWave)
            if (go != null) Destroy(go);
        spawnedThisWave.Clear();
        activeObjects.Clear();
    }

    /// <summary>
    /// Core wave coroutine. Spawns directly via GenerateRandomPositionOnSurface.
    ///
    /// WHY WE DON'T USE FindSpawnPositions.StartSpawn()
    /// ──────────────────────────────────────────────────
    /// The SDK source (FindSpawnPositions.cs line ~236) shows:
    ///
    ///   if (room.GenerateRandomPositionOnSurface(..., out pos, out normal))
    ///   {
    ///       spawnPosition = pos + normal * baseOffset;
    ///       // validity checks with continue...
    ///   }
    ///   // NO ELSE — falls through with spawnPosition = Vector3.zero
    ///   // Then IsPositionInRoom(Vector3.zero) returns false → continue
    ///   // This loops MaxIterations times placing nothing.
    ///
    /// We call GenerateRandomPositionOnSurface ourselves and check the bool
    /// correctly with an explicit `if (!gotPos) continue;` guard.
    /// </summary>
    private IEnumerator SpawnWave()
    {
        WaveNumber++;
        BrokenCount = 0;
        WaveActive = true;
        waveAdvancing = false;
        activeObjects.Clear();

        audioSource?.PlayOneShot(waveStartClip);

        if (debugLogging)
            Debug.Log($"[ObjectWaveManager] ── Wave {WaveNumber} starting " +
                      $"({objectsPerWave} objects) ──");

        // ── 1. Destroy previous wave objects ──────────────────────────────────
        // Yield one frame AFTER Destroy() calls so Unity deregisters the old
        // colliders from the physics engine before CheckOverlaps runs.
        // Without this yield, CheckBox sees the old wave's colliders and
        // rejects every spawn position (the reason Check Overlaps had to be
        // disabled in the Inspector as a workaround).
        ClearWaveObjects();
        findSpawnPositions?.ClearSpawnedPrefabs();
        yield return null;

        // ── 2. Get MRUK room ──────────────────────────────────────────────────
        MRUKRoom room = MRUK.Instance?.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogError("[ObjectWaveManager] MRUK room is null — wave cannot start. " +
                           "Ensure MRUK has finished scanning before StartFirstWaveManual().");
            WaveActive = false;
            yield break;
        }

        // Diagnostic: log what surfaces MRUK has for this room
        if (debugLogging)
        {
            Debug.Log($"[ObjectWaveManager] Room '{room.name}': " +
                      $"FloorAnchor={(room.FloorAnchor != null ? "present" : "NULL")}, " +
                      $"CeilingAnchor={(room.CeilingAnchor != null ? "present" : "NULL")}, " +
                      $"WallAnchors={room.WallAnchors?.Count ?? 0}");
        }

        // ── 3. Choose prefab ──────────────────────────────────────────────────
        GameObject chosenPrefab = GetRandomValidPrefab();
        if (chosenPrefab == null)
        {
            Debug.LogError("[ObjectWaveManager] No valid prefab in breakablePrefabs[].");
            WaveActive = false;
            yield break;
        }

        // Pre-compute bounds for overlap check (same logic as FindSpawnPositions)
        Bounds? prefabBounds = Utilities.GetPrefabBounds(chosenPrefab);
        float baseOffset = -(prefabBounds?.min.y ?? 0f);
        float centerOffset = prefabBounds?.center.y ?? 0f;

        Bounds adjustedBounds = new();
        if (prefabBounds.HasValue)
        {
            const float clearance = 0.01f;
            var min = prefabBounds.Value.min;
            var max = prefabBounds.Value.max;
            min.y += clearance;
            if (max.y < min.y) max.y = min.y;
            adjustedBounds.SetMinMax(min, max);
        }

        // ── 4. Place each object ──────────────────────────────────────────────
        if (debugLogging)
            Debug.Log($"[ObjectWaveManager] Placing {objectsPerWave}× '{chosenPrefab.name}' " +
                      $"on FLOOR|TABLE in '{room.name}'...");

        int placed = 0;

        for (int i = 0; i < objectsPerWave; i++)
        {
            bool placed_this_slot = false;

            for (int attempt = 0; attempt < maxSpawnIterations; attempt++)
            {
                // KEY FIX: check bool return before using pos/normal.
                // If false, pos = Vector3.zero and normal = Vector3.zero —
                // using them causes IsPositionInRoom to fail for all iterations.
                bool gotPos = room.GenerateRandomPositionOnSurface(
                    MRUK.SurfaceType.FACING_UP,
                    0f,
                    new LabelFilter(
                        MRUKAnchor.SceneLabels.FLOOR |
                        MRUKAnchor.SceneLabels.TABLE),
                    out Vector3 pos,
                    out Vector3 normal);

                if (!gotPos) continue;  // <── this is the fix the SDK is missing

                Vector3 spawnPos = pos + normal * baseOffset;
                Vector3 centerPos = spawnPos + normal * centerOffset;
                Quaternion spawnRot = Quaternion.FromToRotation(Vector3.up, normal);

                // Must be inside room
                if (!room.IsPositionInRoom(centerPos)) continue;

                // Must not be inside furniture volumes
                if (room.IsPositionInSceneVolume(centerPos)) continue;

                // Must have clearance above surface (nothing blocking)
                if (room.Raycast(new Ray(pos, normal), surfaceClearanceDistance, out _))
                    continue;

                // Must not overlap existing physics colliders
                if (checkOverlaps && prefabBounds.HasValue)
                {
                    if (Physics.CheckBox(
                            spawnPos + spawnRot * adjustedBounds.center,
                            adjustedBounds.extents,
                            spawnRot,
                            ~0,
                            QueryTriggerInteraction.Ignore))
                        continue;
                }

                // All checks passed — instantiate
                GameObject spawned = Object.Instantiate(chosenPrefab, spawnPos, spawnRot);
                var destructible = spawned.GetComponent<DestructibleObject>();

                if (destructible == null)
                {
                    Debug.LogWarning($"[ObjectWaveManager] Spawned '{spawned.name}' has no " +
                                     "DestructibleObject. Destroying it. Fix the prefab.");
                    Destroy(spawned);
                    break;
                }

                destructible.SetRageMeter(rageMeter);
                activeObjects.Add(destructible);
                spawnedThisWave.Add(spawned);
                placed++;
                placed_this_slot = true;

                if (debugLogging)
                    Debug.Log($"[ObjectWaveManager] Object {placed}/{objectsPerWave} " +
                              $"placed at {spawnPos:F2} after {attempt + 1} attempt(s).");
                break;
            }

            if (!placed_this_slot)
                Debug.LogWarning($"[ObjectWaveManager] Could not place object {i + 1}" +
                                 $"/{objectsPerWave} after {maxSpawnIterations} attempts. " +
                                 "Is there a scanned FLOOR or TABLE in the MRUK room?");
        }

        // ── 5. Results ────────────────────────────────────────────────────────
        Debug.Log($"[ObjectWaveManager] Wave {WaveNumber} spawn complete: " +
                  $"{placed}/{objectsPerWave} objects placed using '{chosenPrefab.name}'.");

        if (placed < objectsPerWave)
            Debug.LogWarning($"[ObjectWaveManager] Only {placed}/{objectsPerWave} placed. " +
                             $"FloorAnchor={(room.FloorAnchor != null ? "present" : "NULL — MRUK did not scan a floor")}. " +
                             "Try reducing objectsPerWave or increasing surfaceClearanceDistance.");

        if (placed == 0)
        {
            Debug.LogWarning("[ObjectWaveManager] Zero objects placed — skipping wave.");
            CheckWaveComplete("ZeroSpawn");
            yield break;
        }

        // ── 6. Safety coroutines ──────────────────────────────────────────────
        StartCoroutine(WaveTimeout());
        StartCoroutine(MonitorActiveObjects());
    }

    private IEnumerator WaveTimeout()
    {
        if (waveTimeoutSeconds <= 0f) yield break;
        yield return new WaitForSeconds(waveTimeoutSeconds);
        if (waveAdvancing || activeObjects.Count == 0) yield break;

        Debug.LogWarning($"[ObjectWaveManager] Wave {WaveNumber} TIMEOUT — " +
                         $"{activeObjects.Count} objects still alive. Clearing.");
        ClearWaveObjects();
        CheckWaveComplete("WaveTimeout");
    }

    private IEnumerator MonitorActiveObjects()
    {
        while (WaveActive && !waveAdvancing)
        {
            yield return new WaitForSeconds(0.5f);
            if (activeObjects.Count == 0) yield break;

            int before = activeObjects.Count;
            activeObjects.RemoveAll(o => o == null || o.gameObject == null);
            spawnedThisWave.RemoveAll(go => go == null);
            int removed = before - activeObjects.Count;

            if (removed > 0)
            {
                BrokenCount += removed;
                if (debugLogging)
                    Debug.LogWarning($"[ObjectWaveManager] Monitor: {removed} objects " +
                                     "destroyed silently. " +
                                     $"{activeObjects.Count} remaining.");
                if (activeObjects.Count == 0)
                {
                    CheckWaveComplete("MonitorActiveObjects");
                    yield break;
                }
            }
        }
    }

    private GameObject GetRandomValidPrefab()
    {
        if (breakablePrefabs == null || breakablePrefabs.Length == 0) return null;

        for (int attempt = 0; attempt < breakablePrefabs.Length * 2; attempt++)
        {
            var candidate = breakablePrefabs[Random.Range(0, breakablePrefabs.Length)];
            if (candidate == null) continue;
            if (candidate.GetComponent<DestructibleObject>() == null) continue;
            return candidate;
        }
        return null;
    }
}