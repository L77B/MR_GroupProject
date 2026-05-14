using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles controller button inputs for the Rage Room experience.
///
/// BUTTON MAPPINGS
/// ───────────────
/// Right Controller A  → SetOriginToPlayerPosition()
///                        Moves the spawn origin to where the player
///                        is currently standing.
///
/// Right Controller B  → ResetOrigin()
///                        Resets the spawn origin back to world (0,0,0).
///
/// Left Controller X   → Reload current scene
///                        Full scene restart.
///
/// WHY OVRInput.GetDown() IS USED
/// ────────────────────────────────
/// OVRInput.GetDown() is the correct API for single-frame button press
/// detection in the OVR SDK. It returns true only on the exact frame
/// the button transitions from up to down — equivalent to Unity's
/// Input.GetKeyDown() but for Quest controllers.
///
/// It internally depends on OVRInput.Update() having been called first,
/// which is done by OVRManager every frame. To guarantee correct ordering
/// this script is set to execute AFTER OVRManager via Script Execution Order
/// in Project Settings (or via the [DefaultExecutionOrder] attribute below).
///
/// SETUP
/// ─────
/// 1. Attach to GameManagers GameObject.
/// 2. Go to Edit → Project Settings → Script Execution Order.
/// 3. Add ControllerInputHandler and set order to 100.
///    OVRManager must execute before this script.
/// 4. Assign roomOriginManager and gameManager or leave empty to auto-find.
/// </summary>

// Ensures this script executes after OVRManager (which runs at default order 0)
// OVRInput.Update() inside OVRManager must run before we read button states
[DefaultExecutionOrder(100)]
public class ControllerInputHandler : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The RoomOriginManager that controls spawn positioning. " +
             "Auto-found at startup if not assigned.")]
    [SerializeField] private RoomOriginManager roomOriginManager;

    [Tooltip("The GameManager for scene restart. " +
             "Auto-found at startup if not assigned.")]
    [SerializeField] private GameManager gameManager;

    [Header("Button Cooldown")]
    [Tooltip("Minimum seconds between button presses to prevent " +
             "accidental double-fires when holding a button. " +
             "OVRInput.GetDown() already handles single-frame detection " +
             "but this adds an extra safety window for scene reload.")]
    [SerializeField] private float buttonCooldown = 0.5f;

    [Header("Debug")]
    [Tooltip("Logs each button press to the Console so you can confirm " +
             "inputs are being received correctly.")]
    [SerializeField] private bool debugLogging = true;

    // ── Runtime State ─────────────────────────────────────────────────────────

    // Cooldown timestamp — only needed for X (scene reload) to prevent
    // accidental double-reload during the haptic delay
    private float lastPressTimeX = 0f;
    private bool reloadPending = false;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-find references if not assigned in Inspector
        if (roomOriginManager == null)
            roomOriginManager = FindAnyObjectByType<RoomOriginManager>();

        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        if (roomOriginManager == null)
            Debug.LogWarning("[ControllerInputHandler] RoomOriginManager not found. " +
                             "Right A and B buttons will not work.");
    }

    /// <summary>
    /// Reads button presses every frame using OVRInput.GetDown().
    ///
    /// OVRInput.GetDown() returns true ONLY on the single frame the button
    /// is first pressed down — it does not repeat while held.
    /// This is the correct method for action triggers in VR.
    ///
    /// Button constants:
    ///   OVRInput.Button.One   = A (right) or X (left)
    ///   OVRInput.Button.Two   = B (right) or Y (left)
    ///   OVRInput.Controller.RTouch = right Touch controller
    ///   OVRInput.Controller.LTouch = left Touch controller
    /// </summary>
    private void Update()
    {
        // ── Right Controller A ────────────────────────────────────────────────
        // OVRInput.Button.One on RTouch = A button
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            if (debugLogging)
                Debug.Log("[ControllerInputHandler] Right A pressed → " +
                          "SetOriginToPlayerPosition()");
            OnButtonA();
        }

        // ── Right Controller B ────────────────────────────────────────────────
        // OVRInput.Button.Two on RTouch = B button
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            if (debugLogging)
                Debug.Log("[ControllerInputHandler] Right B pressed → ResetOrigin()");
            OnButtonB();
        }

        // ── Left Controller X ─────────────────────────────────────────────────
        // OVRInput.Button.One on LTouch = X button
        // Extra cooldown guard prevents double-reload during haptic delay
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch)
            && !reloadPending
            && Time.time - lastPressTimeX > buttonCooldown)
        {
            lastPressTimeX = Time.time;

            if (debugLogging)
                Debug.Log("[ControllerInputHandler] Left X pressed → Reloading scene.");
            OnButtonX();
        }
    }

    // ── Button Actions ────────────────────────────────────────────────────────

    /// <summary>
    /// Right A — Set spawn origin to player's current position.
    /// Press when standing in the correct play area.
    /// All weapons and breakable objects will spawn around this position.
    /// </summary>
    private void OnButtonA()
    {
        if (roomOriginManager != null)
            roomOriginManager.SetOriginToPlayerPosition();
        else
            Debug.LogWarning("[ControllerInputHandler] RoomOriginManager missing.");
    }

    /// <summary>
    /// Right B — Reset spawn origin back to world (0,0,0).
    /// Use to undo a bad origin placement.
    /// </summary>
    private void OnButtonB()
    {
        if (roomOriginManager != null)
            roomOriginManager.ResetOrigin();
        else
            Debug.LogWarning("[ControllerInputHandler] RoomOriginManager missing.");
    }

    /// <summary>
    /// Left X — Reload the current scene with haptic confirmation.
    /// The 0.3s haptic buzz plays before the reload so the player
    /// feels confirmation that the button was registered.
    /// </summary>
    private void OnButtonX()
    {
        reloadPending = true;

        // Haptic confirmation buzz before reload
        OVRInput.SetControllerVibration(0.8f, 0.8f, OVRInput.Controller.All);
        StartCoroutine(ReloadAfterHaptic());
    }

    /// <summary>
    /// Waits for the haptic buzz then reloads the scene.
    /// Sets reloadPending = true to block any further X presses during the delay.
    /// </summary>
    private System.Collections.IEnumerator ReloadAfterHaptic()
    {
        // Short haptic buzz duration
        yield return new WaitForSeconds(0.3f);

        // Stop vibration
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.All);

        // Reload current scene — resets all game state
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}