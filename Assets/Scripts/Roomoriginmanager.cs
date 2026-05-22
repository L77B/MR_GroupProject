using UnityEngine;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Controls where the player starts in the physical room by adjusting
/// MRUK's TrackingSpaceOffset matrix.
///
/// FIXED INSTALLATION MODE (recommended for Rage Room)
/// ─────────────────────────────────────────────────────
/// Set Autocentre On Load = false and Manual Offset = 0,0,0.
/// Clear the guardian on the headset and redraw it from the centre
/// of the play area. The tracking origin becomes the play area.
/// No runtime adjustment needed — guardian and spawns always align.
///
/// DYNAMIC MODE
/// ─────────────
/// Set Autocentre On Load = true. The origin shifts to wherever the
/// player is standing when MRUK loads. Useful when the play area
/// changes between sessions.
///
/// DIAGNOSTIC LOGS
/// ────────────────
/// Extensive logs help identify the tracking space coordinate values
/// needed for Manual Offset calibration. Read them with:
///   adb logcat -s Unity -d | findstr "RoomOriginManager"
/// </summary>
public class RoomOriginManager : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("Auto-Centre Settings")]
    [Tooltip("If true, automatically centres the play space when MRUK loads. " +
             "Set FALSE for a fixed installation where the guardian boundary " +
             "already matches the play area — no offset needed.")]
    [SerializeField] private bool autocentreOnLoad = true;

    /// <summary>Public read access for GameFlowManager.</summary>
    public bool AutocentreOnLoad => autocentreOnLoad;

    [Tooltip("Additional position offset applied after auto-centring or at startup. " +
             "For fixed installation: enter values from 'Camera LOCAL pos in tracking space' log. " +
             "X = right, Y = up (keep 0), Z = forward in tracking space.")]
    [SerializeField] private Vector3 manualOffset = Vector3.zero;

    [Tooltip("Rotation offset applied to the tracking space Y axis.")]
    [SerializeField] private float rotationOffsetDegrees = 0f;

    [Header("Debug")]
    [Tooltip("Enable verbose diagnostic logging.")]
    [SerializeField] private bool debugLogging = true;

    // ── Runtime State ─────────────────────────────────────────────────────────

    /// <summary>The offset currently applied to MRUK's TrackingSpaceOffset.</summary>
    public Vector3 CurrentOrigin { get; private set; } = Vector3.zero;

    // Cached OVRCameraRig reference
    private OVRCameraRig cameraRig;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        cameraRig = FindAnyObjectByType<OVRCameraRig>();

        Debug.Log($"[RoomOriginManager] Awake — " +
                  $"autocentreOnLoad={autocentreOnLoad} " +
                  $"manualOffset={manualOffset} " +
                  $"rotationOffset={rotationOffsetDegrees}°");

        if (cameraRig == null)
            Debug.LogWarning("[RoomOriginManager] OVRCameraRig not found in scene. " +
                             "Tracking space calculations will use fallback.");
    }

    private void Start()
    {
        Debug.Log("[RoomOriginManager] Start — waiting for MRUK scene to load.");

        if (MRUK.Instance == null)
        {
            Debug.LogWarning("[RoomOriginManager] MRUK.Instance not found. " +
                             "Make sure MRUK component is in the scene.");
            return;
        }

        if (!MRUK.Instance.EnableWorldLock)
            Debug.LogWarning("[RoomOriginManager] MRUK.EnableWorldLock is false. " +
                             "TrackingSpaceOffset will not work. " +
                             "Enable WorldLock on the MRUK component.");

        MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
        Debug.Log("[RoomOriginManager] Registered MRUK scene loaded callback.");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the room origin to a specific world-space position.
    /// All MRUK-spawned objects move together.
    /// </summary>
    public void SetOrigin(Vector3 worldPosition, float yRotation = 0f)
    {
        if (MRUK.Instance == null)
        {
            Debug.LogWarning("[RoomOriginManager] Cannot set origin — MRUK.Instance is null.");
            return;
        }

        Quaternion rotation = Quaternion.Euler(0f, yRotation, 0f);
        MRUK.Instance.TrackingSpaceOffset =
            Matrix4x4.TRS(worldPosition, rotation, Vector3.one);

        CurrentOrigin = worldPosition;

        Debug.Log($"[RoomOriginManager] ── Origin SET ──");
        Debug.Log($"[RoomOriginManager]   Position : {worldPosition}");
        Debug.Log($"[RoomOriginManager]   Rotation : {yRotation:F4}°");
        Debug.Log($"[RoomOriginManager]   Matrix   : {MRUK.Instance.TrackingSpaceOffset}");
    }

    /// <summary>
    /// Resets the tracking space offset to identity (Unity world origin).
    /// Objects spawn at (0,0,0) again — aligned with tracking origin.
    /// </summary>
    public void ResetOrigin()
    {
        if (MRUK.Instance == null) return;

        MRUK.Instance.TrackingSpaceOffset = Matrix4x4.identity;
        CurrentOrigin = Vector3.zero;

        Debug.Log("[RoomOriginManager] ── Origin RESET to world origin (0,0,0) ──");
        Debug.Log("[RoomOriginManager]   TrackingSpaceOffset = Matrix4x4.identity");
        Debug.Log("[RoomOriginManager]   All spawns will appear at tracking origin.");
    }

    /// <summary>
    /// Sets the origin to the player's current physical position using
    /// tracking space local coordinates to avoid double-offset errors.
    /// </summary>
    public void SetOriginToPlayerPosition()
    {
        if (Camera.main == null)
        {
            Debug.LogWarning("[RoomOriginManager] Camera.main not found.");
            return;
        }

        Debug.Log("[RoomOriginManager] ── SetOriginToPlayerPosition called ──");

        // Log current state before any changes
        LogCurrentState("BEFORE SetOriginToPlayerPosition");

        Transform tspc = cameraRig != null ? cameraRig.trackingSpace : null;

        Vector3 playerFloorPos;
        float playerYaw;

        if (tspc != null)
        {
            // Camera position in tracking space local coordinates
            // This is the raw position BEFORE any TrackingSpaceOffset is applied
            Vector3 localCamPos = tspc.InverseTransformPoint(
                Camera.main.transform.position);

            // Keep only horizontal — Y stays 0 for floor alignment
            playerFloorPos = new Vector3(localCamPos.x, 0f, localCamPos.z);

            float worldYaw = Camera.main.transform.eulerAngles.y;
            float spaceYaw = tspc.eulerAngles.y;
            playerYaw = worldYaw - spaceYaw;

            Debug.Log($"[RoomOriginManager]   Camera world pos     : {Camera.main.transform.position}");
            Debug.Log($"[RoomOriginManager]   Camera world yaw     : {worldYaw:F4}°");
            Debug.Log($"[RoomOriginManager]   TrackingSpace world  : {tspc.position} yaw:{spaceYaw:F4}°");
            Debug.Log($"[RoomOriginManager]   Camera LOCAL pos     : {localCamPos}");
            Debug.Log($"[RoomOriginManager]   Player floor pos     : {playerFloorPos} (Y forced to 0)");
            Debug.Log($"[RoomOriginManager]   Player local yaw     : {playerYaw:F4}°");
        }
        else
        {
            // Fallback — no tracking space available
            Vector3 camPos = Camera.main.transform.position;
            playerFloorPos = new Vector3(camPos.x, 0f, camPos.z);
            playerYaw = Camera.main.transform.eulerAngles.y;

            Debug.LogWarning("[RoomOriginManager]   OVRCameraRig tracking space not found. " +
                             "Using world space fallback — may cause offset errors.");
            Debug.Log($"[RoomOriginManager]   Camera world pos (fallback): {camPos}");
        }

        SetOrigin(playerFloorPos, playerYaw + rotationOffsetDegrees);

        // Log state after changes
        LogCurrentState("AFTER SetOriginToPlayerPosition");

        Debug.Log($"[RoomOriginManager] Origin set to player position: {playerFloorPos}");
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by MRUK when the room scan data is fully loaded.
    /// Logs extensive diagnostic information then applies the configured offset.
    /// </summary>
    private void OnSceneLoaded()
    {
        Debug.Log("[RoomOriginManager] ════════════════════════════════════════");
        Debug.Log("[RoomOriginManager] MRUK SCENE LOADED — Running diagnostics");
        Debug.Log("[RoomOriginManager] ════════════════════════════════════════");

        // Log full state at the moment MRUK loads
        LogCurrentState("ON MRUK SCENE LOADED");

        // Log MRUK room information
        LogMRUKRoomInfo();

        // Log the key calibration values
        LogCalibrationValues();

        Debug.Log("[RoomOriginManager] ════════════════════════════════════════");
        Debug.Log($"[RoomOriginManager] APPLYING ORIGIN — " +
                  $"autocentreOnLoad={autocentreOnLoad} " +
                  $"manualOffset={manualOffset}");

        // Apply origin
        if (autocentreOnLoad)
        {
            Debug.Log("[RoomOriginManager] Mode: AUTO-CENTRE — centering on player position.");
            AutoCentreOnRoom();
        }
        else if (manualOffset != Vector3.zero || rotationOffsetDegrees != 0f)
        {
            Debug.Log($"[RoomOriginManager] Mode: MANUAL OFFSET — " +
                      $"applying offset {manualOffset} rotation {rotationOffsetDegrees}°");
            SetOrigin(manualOffset, rotationOffsetDegrees);
        }
        else
        {
            Debug.Log("[RoomOriginManager] Mode: NO OFFSET — " +
                      "spawns appear at tracking origin (0,0,0). " +
                      "This is correct for a fixed installation where the guardian " +
                      "boundary matches the play area.");
        }

        Debug.Log("[RoomOriginManager] ════════════════════════════════════════");
    }

    /// <summary>
    /// Auto-centres the origin on the player's current position.
    /// Uses tracking space local coordinates for accuracy.
    /// </summary>
    private void AutoCentreOnRoom()
    {
        MRUKRoom room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;

        if (Camera.main != null)
        {
            Transform tspc = cameraRig != null ? cameraRig.trackingSpace : null;

            Vector3 origin;
            float rotation;

            if (tspc != null)
            {
                Vector3 localCam = tspc.InverseTransformPoint(
                    Camera.main.transform.position);

                origin = new Vector3(
                    localCam.x + manualOffset.x,
                    manualOffset.y,
                    localCam.z + manualOffset.z);

                float worldYaw = Camera.main.transform.eulerAngles.y;
                float spaceYaw = tspc.eulerAngles.y;
                rotation = (worldYaw - spaceYaw) + rotationOffsetDegrees;

                Debug.Log($"[RoomOriginManager] AutoCentre — Camera local: {localCam}");
                Debug.Log($"[RoomOriginManager] AutoCentre — Final origin: {origin} " +
                          $"rotation: {rotation:F4}°");
            }
            else
            {
                Vector3 playerPos = Camera.main.transform.position;
                origin = new Vector3(
                    playerPos.x + manualOffset.x,
                    manualOffset.y,
                    playerPos.z + manualOffset.z);
                rotation = Camera.main.transform.eulerAngles.y + rotationOffsetDegrees;

                Debug.LogWarning("[RoomOriginManager] AutoCentre using world space fallback.");
            }

            SetOrigin(origin, rotation);

            Debug.Log($"[RoomOriginManager] Auto-centred on player at {origin}. " +
                      "Objects will spawn within reach of the player.");
        }
        else if (room != null)
        {
            // Fallback — use room centre
            Vector3 roomCentre = room.transform.position;
            roomCentre.y = 0f;
            SetOrigin(roomCentre + manualOffset, rotationOffsetDegrees);

            Debug.Log($"[RoomOriginManager] AutoCentre fallback — " +
                      $"using room centre: {roomCentre}");
        }
        else
        {
            Debug.LogWarning("[RoomOriginManager] AutoCentre failed — " +
                             "no Camera.main and no MRUK room found.");
        }
    }

    /// <summary>
    /// Logs the current tracking space and camera state.
    /// Call before and after any origin change to see what changed.
    /// </summary>
    private void LogCurrentState(string label)
    {
        Debug.Log($"[RoomOriginManager] ── State: {label} ──");

        // Camera state
        if (Camera.main != null)
        {
            Debug.Log($"[RoomOriginManager]   Camera.main world pos : {Camera.main.transform.position}");
            Debug.Log($"[RoomOriginManager]   Camera.main world rot : {Camera.main.transform.eulerAngles}");
        }
        else
        {
            Debug.LogWarning("[RoomOriginManager]   Camera.main is NULL");
        }

        // Tracking space state
        Transform tspc = cameraRig != null ? cameraRig.trackingSpace : null;
        if (tspc != null)
        {
            Debug.Log($"[RoomOriginManager]   TrackingSpace pos     : {tspc.position}");
            Debug.Log($"[RoomOriginManager]   TrackingSpace rot     : {tspc.eulerAngles}");
            Debug.Log($"[RoomOriginManager]   TrackingSpace scale   : {tspc.localScale}");
        }
        else
        {
            Debug.LogWarning("[RoomOriginManager]   TrackingSpace is NULL — OVRCameraRig not found");
        }

        // MRUK TrackingSpaceOffset state
        if (MRUK.Instance != null)
        {
            Matrix4x4 m = MRUK.Instance.TrackingSpaceOffset;
            Vector3 offsetPos = m.GetPosition();
            Vector3 offsetRot = m.rotation.eulerAngles;
            Debug.Log($"[RoomOriginManager]   MRUK.TrackingSpaceOffset pos : {offsetPos}");
            Debug.Log($"[RoomOriginManager]   MRUK.TrackingSpaceOffset rot : {offsetRot}");
            Debug.Log($"[RoomOriginManager]   MRUK.EnableWorldLock         : {MRUK.Instance.EnableWorldLock}");
        }
        else
        {
            Debug.LogWarning("[RoomOriginManager]   MRUK.Instance is NULL");
        }

        Debug.Log($"[RoomOriginManager]   CurrentOrigin (stored)       : {CurrentOrigin}");
    }

    /// <summary>
    /// Logs MRUK room anchor information.
    /// Shows where MRUK thinks the room boundaries are in world space.
    /// </summary>
    private void LogMRUKRoomInfo()
    {
        if (MRUK.Instance == null)
        {
            Debug.LogWarning("[RoomOriginManager] MRUK.Instance null — cannot log room info.");
            return;
        }

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogWarning("[RoomOriginManager] No MRUK room found. " +
                             "Room scan may not be complete.");
            return;
        }

        Debug.Log($"[RoomOriginManager] ── MRUK Room Info ──");
        Debug.Log($"[RoomOriginManager]   Room GO name         : {room.gameObject.name}");
        Debug.Log($"[RoomOriginManager]   Room world pos       : {room.transform.position}");
        Debug.Log($"[RoomOriginManager]   Room world rot       : {room.transform.eulerAngles}");

        // Log floor anchor if available
        if (room.FloorAnchor != null)
        {
            Debug.Log($"[RoomOriginManager]   Floor anchor pos     : {room.FloorAnchor.transform.position}");
            Debug.Log($"[RoomOriginManager]   Floor anchor rot     : {room.FloorAnchor.transform.eulerAngles}");
        }
        else
        {
            Debug.Log("[RoomOriginManager]   Floor anchor         : null (not scanned)");
        }
    }

    /// <summary>
    /// Logs the specific values needed for Manual Offset calibration.
    /// Stand in your play area when the scene loads and read these values.
    /// Enter the LOCAL position X and Z into Manual Offset in the Inspector.
    /// </summary>
    private void LogCalibrationValues()
    {
        Debug.Log("[RoomOriginManager] ── CALIBRATION VALUES ──");
        Debug.Log("[RoomOriginManager]   Stand in your play area and read these values.");
        Debug.Log("[RoomOriginManager]   Enter Camera LOCAL pos X and Z as Manual Offset.");

        if (Camera.main == null)
        {
            Debug.LogWarning("[RoomOriginManager]   Camera.main is null — cannot log calibration.");
            return;
        }

        Transform tspc = cameraRig != null ? cameraRig.trackingSpace : null;

        if (tspc != null)
        {
            Vector3 localPos = tspc.InverseTransformPoint(Camera.main.transform.position);
            float localYaw = Camera.main.transform.eulerAngles.y - tspc.eulerAngles.y;

            Debug.Log($"[RoomOriginManager]   Camera WORLD pos             : {Camera.main.transform.position}");
            Debug.Log($"[RoomOriginManager]   Camera LOCAL pos (KEY VALUE) : {localPos}");
            Debug.Log($"[RoomOriginManager]   Camera LOCAL yaw             : {localYaw:F4}°");
            Debug.Log($"[RoomOriginManager]   ► Set Manual Offset X = {localPos.x:F4}");
            Debug.Log($"[RoomOriginManager]   ► Set Manual Offset Z = {localPos.z:F4}");
            Debug.Log($"[RoomOriginManager]   ► Set Rotation Offset = {localYaw:F4}°");
            Debug.Log("[RoomOriginManager]   ► Then set Autocentre On Load = false");
            Debug.Log("[RoomOriginManager]   ► Rebuild — spawns will always align to this position");
        }
        else
        {
            Debug.LogWarning("[RoomOriginManager]   TrackingSpace null — cannot compute local position.");
            Debug.Log($"[RoomOriginManager]   Camera WORLD pos (fallback)  : {Camera.main.transform.position}");
        }
    }
}