using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Manages spawning of prefabs into the MRUK room.
///
/// DEFAULT SPAWN BEHAVIOUR
/// ────────────────────────
/// On game start, the first prefab in spawnPrefabs[] (index 0) is spawned
/// automatically once MRUK has finished scanning the room.
/// This ensures something is always present when the player first looks around.
///
/// BUTTON-TRIGGERED SPAWNING
/// ──────────────────────────
/// SpawnNext() cycles through spawnPrefabs[] each time it is called.
/// Called by WebSocketClientExample.IncomingMessageParser() when the
/// ESP32 button is pressed.
///
/// SETUP
/// ─────
/// 1. Assign spawner (FindSpawnPositions component).
/// 2. Assign spawnPrefabs[] — index 0 is the default weapon spawned at start.
/// 3. Attach this script to the GameManagers GameObject.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    // ── Inspector References ──────────────────────────────────────────────────

    [Tooltip("The FindSpawnPositions component that handles room position logic.")]
    public FindSpawnPositions spawner;

    [Tooltip("Array of prefabs to spawn. " +
             "Index 0 is spawned automatically at game start. " +
             "SpawnNext() cycles through the rest on button press.")]
    public GameObject[] spawnPrefabs;

    [Tooltip("Set TRUE when GameFlowManager is in the scene. " +
             "Prevents SpawnManager from auto-spawning on MRUK load. " +
             "GameFlowManager calls SpawnByIndex(0) directly instead.")]
    public bool controlledByFlowManager = true;

    // ── Runtime State ─────────────────────────────────────────────────────────

    /// <summary>
    /// Tracks which prefab was most recently spawned.
    /// Starts at -1 so SpawnNext() correctly picks index 0 on first press.
    /// </summary>
    private int currentIndex = -1;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        // Validate setup before attempting to register the MRUK callback
        if (!IsReady()) return;

        // If controlledByFlowManager is true, GameFlowManager calls
        // SpawnByIndex(0) after the player confirms position.
        // This replaces the unreliable FindAnyObjectByType approach.
        if (controlledByFlowManager)
        {
            Debug.Log("[SpawnManager] Controlled by GameFlowManager. " +
                      "Waiting for manual spawn trigger.");
            return;
        }

        // Not controlled externally — auto-spawn on MRUK load
        MRUK.Instance.RegisterSceneLoadedCallback(SpawnDefault);
        Debug.Log("[SpawnManager] Waiting for MRUK scene to load.");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns the next prefab in the array each time it is called.
    /// Wraps around to index 0 after the last prefab.
    /// Called by WebSocketClientExample when the ESP32 button is pressed.
    /// </summary>
    public void SpawnNext()
    {
        if (!IsReady()) return;

        // Advance to next index — wraps around using modulo
        currentIndex = (currentIndex + 1) % spawnPrefabs.Length;
        SpawnByIndex(currentIndex);
    }

    /// <summary>
    /// Spawns a specific prefab by its index in the spawnPrefabs array.
    /// Clears any previously spawned prefabs before spawning the new one.
    /// </summary>
    /// <param name="index">Index into spawnPrefabs[]. Must be 0 or greater.</param>
    public void SpawnByIndex(int index)
    {
        if (!IsReady()) return;

        // Validate index range
        if (index < 0 || index >= spawnPrefabs.Length)
        {
            Debug.LogWarning($"[SpawnManager] Invalid prefab index {index}. " +
                             $"Valid range is 0 to {spawnPrefabs.Length - 1}.");
            return;
        }

        // Get the current scanned room from MRUK
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogWarning("[SpawnManager] No MRUK room found. " +
                             "Wait until scene data is fully loaded.");
            return;
        }

        // ── Check if any current weapon is being held ────────────────────────
        // Only check on subsequent swaps when a weapon is already present.
        // currentIndex starts at -1 so the first spawn skips this check entirely.
        // This prevents NullReferenceException from WeaponPickup.rack being null
        // on dynamically spawned weapons that are not registered with WeaponRack.
        if (currentIndex >= 0)
        {
            foreach (var spawnedObj in spawner.SpawnedObjects)
            {
                if (spawnedObj == null) continue;

                var pickup = spawnedObj.GetComponent<WeaponPickup>();

                // Guard: only check IsHeld if rack is initialised
                // Weapons spawned by SpawnManager may not be registered
                // with WeaponRack so rack can be null — skip those
                try
                {
                    if (pickup != null && pickup.IsHeld)
                    {
                        Debug.LogWarning("[SpawnManager] Cannot swap — weapon is held.");
                        return;
                    }
                }
                catch (System.Exception)
                {
                    // Swallow any null reference from uninitialised WeaponPickup
                    // and continue — do not block the spawn
                }
            }
        }

        // Clear previous spawn before placing the new one
        spawner.ClearSpawnedPrefabs();

        // ── For index 0 (bat) try to spawn near the player first ──────────────
        // Instead of using FindSpawnPositions random surface logic,
        // we directly instantiate the bat near the player on the closest
        // table surface within reach.
        if (index == 0 && Camera.main != null)
        {
            Vector3 playerPos = Camera.main.transform.position;
            playerPos.y = 0f; // floor level for surface search

            Vector3 bestPos = Vector3.zero;
            bool found = false;
            float bestDist = float.MaxValue;

            // Try up to 10 random positions on TABLE surfaces
            // Pick the one closest to the player
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (room.GenerateRandomPositionOnSurface(
                    MRUK.SurfaceType.FACING_UP,
                    0.1f,
                    new LabelFilter(MRUKAnchor.SceneLabels.TABLE),
                    out Vector3 pos,
                    out Vector3 normal))
                {
                    float dist = Vector3.Distance(pos, Camera.main.transform.position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestPos = pos;
                        found = true;
                    }
                }
            }

            if (found && bestDist < 3.0f) // Only use if within 3m of player
            {
                // Spawn directly at best position
                var bat = Instantiate(
                    spawnPrefabs[index],
                    bestPos,
                    Quaternion.identity);

                currentIndex = index;
                Debug.Log($"[SpawnManager] Bat spawned near player at {bestPos} " +
                          $"distance: {bestDist:F2}m");
                return;
            }
            else
            {
                Debug.LogWarning($"[SpawnManager] No table within 3m of player. " +
                                 $"Closest was {bestDist:F2}m away. " +
                                 "Falling back to floor in front of player.");

                // Fallback — place bat 1m in front of player on floor
                Vector3 forward = Camera.main.transform.forward;
                forward.y = 0f;
                forward.Normalize();
                Vector3 floorPos = Camera.main.transform.position
                                  + forward * 1.0f;
                floorPos.y = 0.05f; // just above floor

                var bat = Instantiate(
                    spawnPrefabs[index],
                    floorPos,
                    Quaternion.identity);

                currentIndex = index;
                Debug.Log($"[SpawnManager] Bat placed on floor at {floorPos} " +
                          "(fallback — no table within reach)");
                return;
            }
        }

        // ── For index > 0 use normal FindSpawnPositions logic ─────────────────
        spawner.SpawnObject = spawnPrefabs[index];
        spawner.StartSpawn(room);

        // Track which index is currently active
        currentIndex = index;

        Debug.Log($"[SpawnManager] Spawned prefab index {index}: " +
                  $"'{spawnPrefabs[index].name}'.");
    }

    // ── Internal Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Spawns the default weapon (index 0) when the MRUK scene is loaded.
    /// Called automatically via RegisterSceneLoadedCallback in Start().
    /// This ensures the first weapon is always present when the game begins
    /// without requiring the player to press the ESP32 button.
    /// </summary>
    private void SpawnDefault()
    {
        Debug.Log("[SpawnManager] MRUK scene loaded. Spawning default weapon (index 0).");

        // SpawnByIndex(0) spawns the first prefab in the array.
        // currentIndex is set to 0 so SpawnNext() advances to index 1
        // on the first button press after the game starts.
        SpawnByIndex(0);
    }

    /// <summary>
    /// Validates that all required references are assigned and MRUK is available.
    /// Returns false and logs a warning for the first problem found.
    /// </summary>
    private bool IsReady()
    {
        if (spawner == null)
        {
            Debug.LogWarning("[SpawnManager] Spawner (FindSpawnPositions) is not assigned. " +
                             "Drag a FindSpawnPositions component into the Spawner slot.");
            return false;
        }

        if (spawnPrefabs == null || spawnPrefabs.Length == 0)
        {
            Debug.LogWarning("[SpawnManager] No spawn prefabs assigned. " +
                             "Add at least one prefab to the Spawn Prefabs array.");
            return false;
        }

        if (MRUK.Instance == null)
        {
            Debug.LogWarning("[SpawnManager] MRUK instance not found. " +
                             "Make sure an MRUK component exists in the scene.");
            return false;
        }

        return true;
    }
}