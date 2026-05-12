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

        // If GameFlowManager is present it controls all spawning order.
        // SpawnManager should NOT auto-spawn — GameFlowManager calls
        // SpawnByIndex(0) after the player confirms their position.
        // Only auto-spawn if GameFlowManager is absent.
        GameFlowManager flowManager = FindAnyObjectByType<GameFlowManager>();
        if (flowManager != null)
        {
            Debug.Log("[SpawnManager] GameFlowManager detected. " +
                      "Waiting for manual spawn trigger.");
            return; // GameFlowManager calls SpawnByIndex(0) directly
        }

        // No GameFlowManager — fall back to automatic MRUK callback behaviour
        MRUK.Instance.RegisterSceneLoadedCallback(SpawnDefault);
        Debug.Log("[SpawnManager] Waiting for MRUK scene to load " +
                  "before spawning default weapon (index 0).");
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
        // If the player is holding the current weapon, warn and abort the swap
        // to avoid destroying a weapon mid-grab which is jarring in VR.
        // Comment this block out if you want forced swapping regardless.
        foreach (var spawnedObj in spawner.SpawnedObjects)
        {
            if (spawnedObj == null) continue;
            var pickup = spawnedObj.GetComponent<WeaponPickup>();
            if (pickup != null && pickup.IsHeld)
            {
                Debug.LogWarning("[SpawnManager] Cannot swap weapon — " +
                                 "player is currently holding the active weapon. " +
                                 "Release the weapon before pressing the button.");
                return;
            }
        }

        // Clear previous spawn before placing the new one
        // so only one set of prefabs exists at a time
        spawner.ClearSpawnedPrefabs();

        // Set the prefab and trigger the spawn
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