using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Controls where the player starts in the physical room by adjusting
/// MRUK's TrackingSpaceOffset matrix.
///
/// WHY THIS EXISTS
/// ────────────────
/// MRUK spawns all objects relative to the scanned room's coordinate space.
/// By default this maps the room's physical origin (where you were standing
/// when you ran Space Setup) to Unity's world origin (0,0,0).
///
/// If you want the experience to start at a different physical location —
/// for example, centred in the middle of the room — you move the
/// TrackingSpaceOffset, not OVRCameraRig. Moving OVRCameraRig breaks
/// MRUK alignment. Moving TrackingSpaceOffset moves everything correctly:
/// spawned weapons, breakable objects, room mesh, all stay aligned.
///
/// HOW TO USE
/// ──────────
/// Option A — Automatic: tick autocentreOnLoad. The manager finds the
///            centre of the scanned room floor and sets that as origin.
///            Everything spawns around the player's actual position.
///
/// Option B — Manual: untick autocentreOnLoad and call
///            SetOrigin(position, rotation) from code or a UI button.
///
/// Option C — Inspector offset: set manualOffset in the Inspector to
///            shift the origin by a fixed amount from the room centre.
///
/// SETUP
/// ─────
/// 1. Attach to GameManagers GameObject.
/// 2. Make sure MRUK.EnableWorldLock = true (required for offset to work).
/// 3. Make sure OVRCameraRig is at position (0,0,0) in the scene.
/// </summary>
public class RoomOriginManager : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("Auto-Centre Settings")]
    [Tooltip("If true, automatically centres the play space on the scanned " +
             "room floor when MRUK loads. This moves the spawn origin to " +
             "the centre of the physical room so objects appear around the player.")]
    [SerializeField] private bool autocentreOnLoad = true;

    [Tooltip("Additional position offset applied after auto-centring. " +
             "Use this to fine-tune the spawn area without disabling auto-centre. " +
             "X = right, Y = up, Z = forward in room space.")]
    [SerializeField] private Vector3 manualOffset = Vector3.zero;

    [Tooltip("Rotation offset applied to the tracking space. " +
             "Rotates the entire room coordinate system around the Y axis. " +
             "Use this if objects spawn at the wrong angle.")]
    [SerializeField] private float rotationOffsetDegrees = 0f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    // ── Runtime State ─────────────────────────────────────────────────────────

    /// <summary>The offset currently applied to MRUK's TrackingSpaceOffset.</summary>
    public Vector3 CurrentOrigin { get; private set; } = Vector3.zero;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        // Validate MRUK is present
        if (MRUK.Instance == null)
        {
            Debug.LogWarning("[RoomOriginManager] MRUK.Instance not found. " +
                             "Make sure MRUK is in the scene.");
            return;
        }

        // Validate WorldLock is enabled — TrackingSpaceOffset only works with WorldLock
        if (!MRUK.Instance.EnableWorldLock)
        {
            Debug.LogWarning("[RoomOriginManager] MRUK.EnableWorldLock is false. " +
                             "TrackingSpaceOffset will not work. " +
                             "Enable WorldLock on the MRUK component.");
        }

        // Wait for MRUK to finish scanning before applying any offset
        MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the room origin to a specific world-space position.
    /// All MRUK-spawned objects (weapons, breakables, room mesh) move together.
    /// Call this at runtime to reposition the entire play space.
    /// </summary>
    /// <param name="worldPosition">Target world-space position for the origin.</param>
    /// <param name="yRotation">Y-axis rotation for the tracking space.</param>
    public void SetOrigin(Vector3 worldPosition, float yRotation = 0f)
    {
        if (MRUK.Instance == null) return;

        // Build the offset matrix from position and rotation
        // TrackingSpaceOffset is a Matrix4x4 that MRUK applies to TrackingSpace
        Quaternion rotation = Quaternion.Euler(0f, yRotation, 0f);
        MRUK.Instance.TrackingSpaceOffset =
            Matrix4x4.TRS(worldPosition, rotation, Vector3.one);

        CurrentOrigin = worldPosition;

        if (debugLogging)
            Debug.Log($"[RoomOriginManager] Origin set to {worldPosition} " +
                      $"rotation {yRotation}°");
    }

    /// <summary>
    /// Resets the tracking space offset to identity (Unity world origin).
    /// Objects will spawn at (0,0,0) again.
    /// </summary>
    public void ResetOrigin()
    {
        if (MRUK.Instance == null) return;

        MRUK.Instance.TrackingSpaceOffset = Matrix4x4.identity;
        CurrentOrigin = Vector3.zero;

        if (debugLogging)
            Debug.Log("[RoomOriginManager] Origin reset to world origin (0,0,0).");
    }

    /// <summary>
    /// Sets the origin to the player's current physical position.
    /// Call this when the player is standing where they want to play
    /// so all spawns appear around them from that moment.
    /// </summary>
    public void SetOriginToPlayerPosition()
    {
        if (Camera.main == null)
        {
            Debug.LogWarning("[RoomOriginManager] Camera.main not found.");
            return;
        }

        // Use the player's current horizontal position as the new origin.
        // Y is set to 0 so the floor stays at floor level.
        Vector3 playerPos = Camera.main.transform.position;
        playerPos.y = 0f;

        float playerYaw = Camera.main.transform.eulerAngles.y;

        SetOrigin(playerPos, playerYaw + rotationOffsetDegrees);

        if (debugLogging)
            Debug.Log($"[RoomOriginManager] Origin set to player position: {playerPos}");
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by MRUK when the room scan data is fully loaded.
    /// Applies the configured origin offset.
    /// </summary>
    private void OnSceneLoaded()
    {
        if (debugLogging)
            Debug.Log("[RoomOriginManager] MRUK scene loaded. Applying origin offset.");

        if (autocentreOnLoad)
            AutoCentreOnRoom();
        else if (manualOffset != Vector3.zero || rotationOffsetDegrees != 0f)
            SetOrigin(manualOffset, rotationOffsetDegrees);
    }

    /// <summary>
    /// Finds the centre of the scanned room floor polygon and sets that
    /// as the tracking space origin so objects spawn around the player.
    ///
    /// HOW IT WORKS
    /// ─────────────
    /// MRUK's room has a floor anchor with a known world-space position.
    /// We use that position as the new origin, then add the manualOffset
    /// for any additional fine-tuning configured in the Inspector.
    /// </summary>
    private void AutoCentreOnRoom()
    {
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogWarning("[RoomOriginManager] No room found for auto-centre. " +
                             "Ensure Space Setup has been completed on the headset.");
            return;
        }

        // ── Option A: Use the player's current head position ──────────────────
        // The most reliable approach — wherever the player is standing when
        // the scene loads becomes the centre of the spawn area.
        if (Camera.main != null)
        {
            Vector3 playerPos = Camera.main.transform.position;

            // Keep Y at 0 so floor stays at floor level
            // Apply manual offset for fine-tuning
            Vector3 origin = new Vector3(
                playerPos.x + manualOffset.x,
                manualOffset.y,
                playerPos.z + manualOffset.z);

            float rotation = Camera.main.transform.eulerAngles.y
                           + rotationOffsetDegrees;

            SetOrigin(origin, rotation);

            if (debugLogging)
                Debug.Log($"[RoomOriginManager] Auto-centred on player at {origin}. " +
                          $"Objects will spawn within reach of the player.");
        }
        else
        {
            // ── Option B: Use room floor anchor centre ────────────────────────
            // Fallback when camera is not available
            Vector3 roomCentre = room.transform.position;
            roomCentre.y = 0f;

            SetOrigin(roomCentre + manualOffset, rotationOffsetDegrees);

            if (debugLogging)
                Debug.Log($"[RoomOriginManager] Auto-centred on room floor: {roomCentre}");
        }
    }
}