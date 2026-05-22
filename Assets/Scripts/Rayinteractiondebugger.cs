using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Comprehensive ray interaction debugger.
/// Attach to any GameObject in the scene.
/// Logs every layer of the OVR ray interaction system every 3 seconds.
/// Also draws a manual raycast from the controller every frame
/// and fires the button directly if the ray hits it.
///
/// SETUP
/// ─────
/// 1. Attach to GameManagers GameObject.
/// 2. Assign confirmButton — drag the ConfirmButton GO here.
/// 3. Build and run — read logs with:
///    adb logcat -s Unity -d | findstr "RayDebug"
/// </summary>
public class RayInteractionDebugger : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The confirm button to test raycasting against.")]
    [SerializeField] private GameObject confirmButton;

    [Tooltip("The Setup Canvas containing the UI.")]
    [SerializeField] private Canvas setupCanvas;

    [Tooltip("The SetupUIManager to call OnConfirmPressed on.")]
    [SerializeField] private SetupUIManager setupUI;

    [Header("Settings")]
    [Tooltip("How often to log the full system state (seconds).")]
    [SerializeField] private float logInterval = 3f;

    [Tooltip("Draw a debug ray visible in Scene view.")]
    [SerializeField] private bool drawDebugRay = true;

    [Tooltip("Maximum ray distance for UI detection.")]
    [SerializeField] private float rayDistance = 5f;

    [Tooltip("If true, automatically fires the confirm button when ray hits it. " +
             "This bypasses the OVR interaction system entirely for testing.")]
    [SerializeField] private bool autoFireOnRayHit = true;

    // ── Runtime State ─────────────────────────────────────────────────────────

    private float nextLogTime = 0f;
    private float lastFireTime = -999f;
    private const float fireCooldown = 1.0f;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        // Auto-find references
        if (confirmButton == null)
        {
            var setupUIFound = FindAnyObjectByType<SetupUIManager>();
            if (setupUIFound != null)
                setupUI = setupUIFound;
        }

        if (setupCanvas == null)
            setupCanvas = FindAnyObjectByType<Canvas>();

        Debug.Log("[RayDebug] ════════════════════════════════════════");
        Debug.Log("[RayDebug] RayInteractionDebugger started.");
        Debug.Log("[RayDebug] Will log system state every " + logInterval + " seconds.");
        Debug.Log("[RayDebug] ════════════════════════════════════════");

        // Run initial full diagnostic
        LogFullSystemState();
    }

    private void Update()
    {
        // Periodic full system log
        if (Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + logInterval;
            LogFullSystemState();
        }

        // Manual raycast from right controller every frame
        PerformManualRaycast();
    }

    // ── Manual Raycast ────────────────────────────────────────────────────────

    /// <summary>
    /// Performs a manual physics raycast from the right controller every frame.
    /// This completely bypasses the OVR interaction system to test if
    /// the geometry is correct regardless of SDK configuration.
    /// </summary>
    private void PerformManualRaycast()
    {
        if (Camera.main == null) return;

        // Get controller position and direction
        // Try OVR controller first, fall back to camera forward
        Vector3 rayOrigin;
        Vector3 rayDirection;

        var cameraRig = FindAnyObjectByType<OVRCameraRig>();
        if (cameraRig != null)
        {
            // Use right hand anchor as ray origin
            Transform rightHand = cameraRig.rightHandAnchor;
            if (rightHand != null)
            {
                rayOrigin = rightHand.position;
                rayDirection = rightHand.forward;
            }
            else
            {
                rayOrigin = Camera.main.transform.position;
                rayDirection = Camera.main.transform.forward;
            }
        }
        else
        {
            rayOrigin = Camera.main.transform.position;
            rayDirection = Camera.main.transform.forward;
        }

        // Draw debug ray in Scene view
        if (drawDebugRay)
        {
            Debug.DrawRay(rayOrigin, rayDirection * rayDistance,
                Color.cyan, Time.deltaTime);
        }

        // ── Physics raycast ───────────────────────────────────────────────────
        Ray ray = new Ray(rayOrigin, rayDirection);

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            if (drawDebugRay)
                Debug.DrawRay(rayOrigin, rayDirection * hit.distance,
                    Color.green, Time.deltaTime);

            // Check if we hit the confirm button or its canvas
            bool hitButton = confirmButton != null &&
                (hit.transform.gameObject == confirmButton ||
                 hit.transform.IsChildOf(confirmButton.transform));

            bool hitCanvas = setupCanvas != null &&
                (hit.transform.gameObject == setupCanvas.gameObject ||
                 hit.transform.IsChildOf(setupCanvas.transform));

            if (hitButton || hitCanvas)
            {
                Debug.Log($"[RayDebug] ✓ Physics ray HIT: {hit.transform.name} " +
                          $"at distance {hit.distance:F2}m " +
                          $"point:{hit.point}");

                // Auto-fire the confirm button if enabled
                if (autoFireOnRayHit &&
                    Time.time - lastFireTime > fireCooldown &&
                    OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger,
                        OVRInput.Controller.RTouch))
                {
                    lastFireTime = Time.time;
                    Debug.Log("[RayDebug] ► FIRING confirm button via direct call!");

                    if (setupUI != null)
                        setupUI.OnConfirmPressed();
                    else if (confirmButton != null)
                    {
                        var btn = confirmButton.GetComponent<Button>();
                        btn?.onClick.Invoke();
                    }
                }
            }
        }

        // ── UI Raycast (EventSystem) ──────────────────────────────────────────
        if (EventSystem.current != null && setupCanvas != null)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current);

            // Convert controller direction to screen point
            Vector3 screenPoint = Camera.main.WorldToScreenPoint(
                rayOrigin + rayDirection * 1.0f);
            pointerData.position = new Vector2(screenPoint.x, screenPoint.y);

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            if (results.Count > 0)
            {
                foreach (var result in results)
                {
                    Debug.Log($"[RayDebug] UI Raycast HIT: {result.gameObject.name} " +
                              $"depth:{result.depth} distance:{result.distance:F2}");
                }
            }
        }
    }

    // ── Full System Diagnostic ────────────────────────────────────────────────

    private void LogFullSystemState()
    {
        Debug.Log("[RayDebug] ════════ FULL SYSTEM STATE ════════");

        // 1. EventSystem
        LogEventSystem();

        // 2. Canvas and Raycaster
        LogCanvasState();

        // 3. OVR Ray Interactors
        LogOVRRayInteractors();

        // 4. Button state
        LogButtonState();

        // 5. Controller state
        LogControllerState();

        // 6. Camera state
        LogCameraState();

        Debug.Log("[RayDebug] ════════════════════════════════════");
    }

    private void LogEventSystem()
    {
        Debug.Log("[RayDebug] ── EventSystem ──");

        if (EventSystem.current == null)
        {
            Debug.LogError("[RayDebug] ✗ NO EventSystem in scene! " +
                           "Right-click Hierarchy → UI → Event System to add one.");
            return;
        }

        Debug.Log($"[RayDebug]   EventSystem GO: {EventSystem.current.gameObject.name}");
        Debug.Log($"[RayDebug]   EventSystem enabled: {EventSystem.current.enabled}");

        // Check input module
        var standaloneInput = EventSystem.current
            .GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        var ovrInput = EventSystem.current.GetComponent<OVRInputModule>();

        if (ovrInput != null)
            Debug.Log("[RayDebug]   ✓ OVRInputModule found on EventSystem");
        else
            Debug.LogWarning("[RayDebug]   ✗ OVRInputModule MISSING from EventSystem. " +
                             "Add OVRInputModule and remove StandaloneInputModule.");

        if (standaloneInput != null)
            Debug.LogWarning("[RayDebug]   ⚠ StandaloneInputModule still present. " +
                             "Remove it — it conflicts with OVRInputModule.");
    }

    private void LogCanvasState()
    {
        Debug.Log("[RayDebug] ── Canvas ──");

        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Debug.Log($"[RayDebug]   Total canvases in scene: {allCanvases.Length}");

        foreach (var canvas in allCanvases)
        {
            Debug.Log($"[RayDebug]   Canvas: '{canvas.gameObject.name}' " +
                      $"mode:{canvas.renderMode} " +
                      $"active:{canvas.gameObject.activeInHierarchy} " +
                      $"sortOrder:{canvas.sortingOrder}");

            // Check for OVR Raycaster
            var ovrRaycaster = canvas.GetComponent<OVRRaycaster>();
            var graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();

            if (ovrRaycaster != null)
                Debug.Log($"[RayDebug]     ✓ OVRRaycaster present " +
                          $"enabled:{ovrRaycaster.enabled}");
            else
                Debug.LogWarning($"[RayDebug]     ✗ OVRRaycaster MISSING on '{canvas.gameObject.name}'. " +
                                 "Add OVRRaycaster component to this Canvas.");

            if (graphicRaycaster != null)
                Debug.LogWarning($"[RayDebug]     ⚠ GraphicRaycaster present — " +
                                 "may conflict with OVRRaycaster. Consider removing it.");

            // Check camera reference
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                if (canvas.worldCamera != null)
                    Debug.Log($"[RayDebug]     World camera: {canvas.worldCamera.name}");
                else
                    Debug.LogWarning("[RayDebug]     ⚠ World Space canvas has no camera assigned. " +
                                     "OVRRaycaster assigns it automatically but check if it did.");
            }
        }
    }

    private void LogOVRRayInteractors()
    {
        Debug.Log("[RayDebug] ── OVR Ray Interactors ──");

        // Check for ray interactors under OVRCameraRig
        var cameraRig = FindAnyObjectByType<OVRCameraRig>();
        if (cameraRig == null)
        {
            Debug.LogError("[RayDebug]   ✗ OVRCameraRig not found!");
            return;
        }

        Debug.Log($"[RayDebug]   OVRCameraRig found: {cameraRig.gameObject.name}");
        Debug.Log($"[RayDebug]   OVRCameraRig position: {cameraRig.transform.position}");

        // Check right hand anchor
        Transform rightHand = cameraRig.rightHandAnchor;
        Transform rightController = cameraRig.rightControllerAnchor;

        Debug.Log($"[RayDebug]   rightHandAnchor: " +
                  $"{(rightHand != null ? rightHand.name : "NULL")}");
        Debug.Log($"[RayDebug]   rightControllerAnchor: " +
                  $"{(rightController != null ? rightController.name : "NULL")}");

        if (rightController != null)
        {
            Debug.Log($"[RayDebug]   rightControllerAnchor pos: {rightController.position}");
            Debug.Log($"[RayDebug]   rightControllerAnchor forward: {rightController.forward}");

            // Check children for ray interactor components
            var allComponents = rightController.GetComponentsInChildren<MonoBehaviour>(true);
            bool foundRayInteractor = false;
            foreach (var comp in allComponents)
            {
                string typeName = comp.GetType().Name;
                if (typeName.Contains("Ray") || typeName.Contains("Interactor"))
                {
                    Debug.Log($"[RayDebug]   Found on rightController: {typeName} " +
                              $"enabled:{comp.enabled}");
                    foundRayInteractor = true;
                }
            }

            if (!foundRayInteractor)
                Debug.LogWarning("[RayDebug]   ✗ No RayInteractor found under rightControllerAnchor. " +
                                 "Add Ray Interaction Building Block from Meta → Tools → Building Blocks.");
        }

        // Also check TrackingSpace children broadly
        Transform trackingSpace = cameraRig.trackingSpace;
        if (trackingSpace != null)
        {
            var allInTracking = trackingSpace.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var comp in allInTracking)
            {
                string typeName = comp.GetType().Name;
                if (typeName.Contains("RayInteractor") || typeName == "RayCaster")
                {
                    Debug.Log($"[RayDebug]   RayInteractor in TrackingSpace: " +
                              $"{comp.gameObject.name}/{typeName} " +
                              $"active:{comp.gameObject.activeInHierarchy} " +
                              $"enabled:{comp.enabled}");
                }
            }
        }
    }

    private void LogButtonState()
    {
        Debug.Log("[RayDebug] ── Confirm Button ──");

        if (confirmButton == null)
        {
            Debug.LogWarning("[RayDebug]   confirmButton not assigned in Inspector. " +
                             "Drag the ConfirmButton GO into this field.");

            // Try to find it
            var allButtons = FindObjectsByType<Button>(FindObjectsSortMode.None);
            Debug.Log($"[RayDebug]   Found {allButtons.Length} Button components in scene:");
            foreach (var btn in allButtons)
            {
                Debug.Log($"[RayDebug]     Button: '{btn.gameObject.name}' " +
                          $"interactable:{btn.interactable} " +
                          $"active:{btn.gameObject.activeInHierarchy}");
            }
            return;
        }

        Debug.Log($"[RayDebug]   Button GO: {confirmButton.name}");
        Debug.Log($"[RayDebug]   Active in hierarchy: {confirmButton.activeInHierarchy}");
        Debug.Log($"[RayDebug]   World position: {confirmButton.transform.position}");
        Debug.Log($"[RayDebug]   World rotation: {confirmButton.transform.eulerAngles}");
        Debug.Log($"[RayDebug]   Scale: {confirmButton.transform.lossyScale}");

        var button = confirmButton.GetComponent<Button>();
        if (button != null)
        {
            Debug.Log($"[RayDebug]   Button.interactable: {button.interactable}");
            Debug.Log($"[RayDebug]   Button onClick listeners: " +
                      $"{button.onClick.GetPersistentEventCount()}");
        }
        else
            Debug.LogWarning("[RayDebug]   ✗ No Button component on confirmButton GO.");

        // Check RectTransform
        var rect = confirmButton.GetComponent<RectTransform>();
        if (rect != null)
        {
            Debug.Log($"[RayDebug]   RectTransform size: {rect.rect.size}");
            Debug.Log($"[RayDebug]   RectTransform pivot: {rect.pivot}");

            // Calculate world corners
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Debug.Log($"[RayDebug]   World corners:");
            Debug.Log($"[RayDebug]     BL:{corners[0]} TL:{corners[1]}");
            Debug.Log($"[RayDebug]     TR:{corners[2]} BR:{corners[3]}");
        }

        // Distance from camera to button
        if (Camera.main != null)
        {
            float dist = Vector3.Distance(
                Camera.main.transform.position,
                confirmButton.transform.position);
            Debug.Log($"[RayDebug]   Distance from camera: {dist:F2}m");

            // Check if button is in front of camera
            Vector3 toCam = confirmButton.transform.position
                          - Camera.main.transform.position;
            float dot = Vector3.Dot(Camera.main.transform.forward, toCam.normalized);
            Debug.Log($"[RayDebug]   In front of camera (dot>0): {dot:F2} " +
                      $"{(dot > 0 ? "✓ YES" : "✗ NO — button is behind camera")}");
        }

        // Check collider for physics raycasting
        var collider = confirmButton.GetComponent<Collider>();
        if (collider != null)
            Debug.Log($"[RayDebug]   Collider: {collider.GetType().Name} " +
                      $"isTrigger:{collider.isTrigger} enabled:{collider.enabled}");
        else
            Debug.Log("[RayDebug]   No Collider on button (physics raycast will not hit it, " +
                      "UI raycast through OVRRaycaster should still work).");
    }

    private void LogControllerState()
    {
        Debug.Log("[RayDebug] ── Controller State ──");

        bool rightConnected = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
        bool leftConnected = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

        Debug.Log($"[RayDebug]   Right controller connected: {rightConnected}");
        Debug.Log($"[RayDebug]   Left controller connected : {leftConnected}");

        if (rightConnected)
        {
            Vector3 rightPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
            Quaternion rightRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

            Debug.Log($"[RayDebug]   Right controller local pos: {rightPos}");
            Debug.Log($"[RayDebug]   Right controller local rot: {rightRot.eulerAngles}");

            // Convert to world space using tracking space
            var cameraRig = FindAnyObjectByType<OVRCameraRig>();
            if (cameraRig != null && cameraRig.trackingSpace != null)
            {
                Vector3 worldPos = cameraRig.trackingSpace.TransformPoint(rightPos);
                Vector3 worldForward = cameraRig.trackingSpace.rotation * rightRot * Vector3.forward;
                Debug.Log($"[RayDebug]   Right controller WORLD pos: {worldPos}");
                Debug.Log($"[RayDebug]   Right controller WORLD forward: {worldForward}");

                // Check if controller is pointing at the canvas
                if (confirmButton != null)
                {
                    Vector3 toButton = confirmButton.transform.position - worldPos;
                    float dot = Vector3.Dot(worldForward, toButton.normalized);
                    float angleDeg = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
                    Debug.Log($"[RayDebug]   Angle from controller to button: {angleDeg:F1}° " +
                              $"{(angleDeg < 15f ? "✓ POINTING AT BUTTON" : "✗ not pointing at button")}");
                }
            }
        }
        else
        {
            Debug.LogWarning("[RayDebug]   ✗ Right controller not connected. " +
                             "Make sure controllers are on and connected to the headset.");
        }
    }

    private void LogCameraState()
    {
        Debug.Log("[RayDebug] ── Camera State ──");

        if (Camera.main == null)
        {
            Debug.LogError("[RayDebug]   ✗ Camera.main is NULL. " +
                           "No camera tagged MainCamera in scene.");
            return;
        }

        Debug.Log($"[RayDebug]   Camera GO: {Camera.main.gameObject.name}");
        Debug.Log($"[RayDebug]   Camera world pos: {Camera.main.transform.position}");
        Debug.Log($"[RayDebug]   Camera world forward: {Camera.main.transform.forward}");
        Debug.Log($"[RayDebug]   Near clip plane: {Camera.main.nearClipPlane}");
        Debug.Log($"[RayDebug]   Far clip plane: {Camera.main.farClipPlane}");
        Debug.Log($"[RayDebug]   Culling mask: {Camera.main.cullingMask}");

        // Check if UI layer is in culling mask
        int uiLayer = LayerMask.NameToLayer("UI");
        bool rendersUI = (Camera.main.cullingMask & (1 << uiLayer)) != 0;
        Debug.Log($"[RayDebug]   Renders UI layer: {(rendersUI ? "✓ YES" : "✗ NO — canvas may be invisible")}");
    }
}