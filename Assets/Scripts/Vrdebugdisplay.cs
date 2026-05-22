using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays Unity Debug.Log messages as floating text inside the VR headset.
/// No PC connection needed — all logs appear in-world as you play.
///
/// SETUP
/// ─────
/// 1. Create a World Space Canvas (child of CenterEyeAnchor or scene root).
///    Scale: 0.001, 0.001, 0.001
///    Sort Order: 20 (above other UI)
/// 2. Add a TextMeshPro child inside the Canvas.
///    Width: 600, Height: 400, Font Size: 14, Alignment: Top Left
/// 3. Add this script to the Canvas GameObject.
/// 4. Drag the TextMeshPro into the displayText slot.
///
/// USAGE
/// ─────
/// All Debug.Log, Debug.LogWarning, Debug.LogError calls automatically
/// appear in the panel. Prefix filters let you show only your own logs.
///
/// Press Right Thumbstick click to toggle visibility on/off.
/// Press Left Thumbstick click to clear all messages.
/// </summary>
public class VRDebugDisplay : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("UI")]
    [Tooltip("TextMeshPro component that displays the log messages.")]
    [SerializeField] private TextMeshProUGUI displayText;

    [Tooltip("Background image for readability. Optional.")]
    [SerializeField] private UnityEngine.UI.Image backgroundImage;

    [Header("Filter")]
    [Tooltip("Only show messages that START WITH one of these prefixes. " +
             "Leave empty to show ALL Unity messages. " +
             "Add '[' to show only your bracketed custom logs e.g. [SpawnManager].")]
    [SerializeField] private string[] filterPrefixes = new string[] { "[" };

    [Tooltip("Also show LogWarning and LogError messages regardless of prefix filter.")]
    [SerializeField] private bool alwaysShowErrors = true;

    [Header("Display Settings")]
    [Tooltip("Maximum number of log lines shown at once. " +
             "Older messages scroll off the top.")]
    [SerializeField] private int maxLines = 15;

    [Tooltip("Show timestamps next to each message.")]
    [SerializeField] private bool showTimestamps = true;

    [Tooltip("Start visible or hidden.")]
    [SerializeField] private bool startVisible = true;

    [Header("Positioning")]
    [Tooltip("Position relative to CenterEyeAnchor (camera). " +
             "Adjust so panel sits comfortably in view.")]
    [SerializeField] private Vector3 offsetFromCamera = new Vector3(0f, 0.15f, 1.2f);

    [Tooltip("If true, panel follows the player's head. " +
             "If false, stays fixed at its start position.")]
    [SerializeField] private bool followHead = true;

    [Header("Controller Shortcuts")]
    [Tooltip("Right thumbstick click — toggle panel visibility.")]
    [SerializeField] private bool enableToggle = true;

    [Tooltip("Left thumbstick click — clear all messages.")]
    [SerializeField] private bool enableClear = true;

    // ── Runtime State ─────────────────────────────────────────────────────────

    private Queue<string> messageQueue = new Queue<string>();
    private bool isVisible = true;
    private Camera playerCamera;

    // Colour codes for different log types
    private const string COLOR_LOG = "#FFFFFF"; // White — normal logs
    private const string COLOR_WARNING = "#FFD700"; // Gold  — warnings
    private const string COLOR_ERROR = "#FF4444"; // Red   — errors

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        playerCamera = Camera.main;
        isVisible = startVisible;

        // Subscribe to ALL Unity log messages
        // This catches Debug.Log, Debug.LogWarning, Debug.LogError
        // from any script in the project
        Application.logMessageReceived += HandleLog;

        // Initial visibility
        SetVisible(startVisible);

        // Welcome message so you know the display is working
        AddMessage("[VRDebugDisplay] Active — showing filtered logs.", COLOR_LOG);
    }

    private void OnDestroy()
    {
        // Always unsubscribe to prevent callbacks after destroy
        Application.logMessageReceived -= HandleLog;
    }

    private void Start()
    {
        // Snap to correct position immediately
        if (playerCamera != null)
            SnapToPosition();
    }

    private void LateUpdate()
    {
        // Follow the player's head if enabled
        if (followHead && playerCamera != null)
        {
            transform.position = playerCamera.transform.TransformPoint(offsetFromCamera);
            transform.rotation = Quaternion.LookRotation(
                transform.position - playerCamera.transform.position);
        }

        // Controller shortcuts
        HandleControllerInput();
    }

    // ── Log Handler ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by Unity for every Debug.Log/LogWarning/LogError in the project.
    /// Filters messages and adds matching ones to the display queue.
    /// </summary>
    private void HandleLog(string message, string stackTrace, LogType type)
    {
        // Determine colour based on log type
        string color;
        switch (type)
        {
            case LogType.Warning:
                color = COLOR_WARNING;
                if (!alwaysShowErrors && !PassesFilter(message)) return;
                break;
            case LogType.Error:
            case LogType.Exception:
            case LogType.Assert:
                color = COLOR_ERROR;
                if (!alwaysShowErrors && !PassesFilter(message)) return;
                break;
            default:
                color = COLOR_LOG;
                // Apply prefix filter for normal logs
                if (!PassesFilter(message)) return;
                break;
        }

        AddMessage(message, color);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Toggle panel visibility.</summary>
    public void ToggleVisibility()
    {
        SetVisible(!isVisible);
    }

    /// <summary>Clear all messages from the display.</summary>
    public void ClearMessages()
    {
        messageQueue.Clear();
        if (displayText != null)
            displayText.text = "[Cleared]";
    }

    /// <summary>Show or hide the debug panel.</summary>
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (displayText != null)
            displayText.gameObject.SetActive(visible);
        if (backgroundImage != null)
            backgroundImage.gameObject.SetActive(visible);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks if a message passes the prefix filter.
    /// Returns true if filterPrefixes is empty (show everything)
    /// or if the message starts with any of the configured prefixes.
    /// </summary>
    private bool PassesFilter(string message)
    {
        if (filterPrefixes == null || filterPrefixes.Length == 0)
            return true;

        foreach (var prefix in filterPrefixes)
        {
            if (string.IsNullOrEmpty(prefix) || message.StartsWith(prefix))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds a formatted message to the queue and refreshes the display.
    /// Removes old messages when queue exceeds maxLines.
    /// </summary>
    private void AddMessage(string message, string color)
    {
        // Build formatted line with optional timestamp
        string timestamp = showTimestamps
            ? $"<color=#888888>[{Time.time:F1}s]</color> "
            : "";

        // Truncate very long messages to keep display readable
        if (message.Length > 120)
            message = message.Substring(0, 117) + "...";

        string formatted = $"{timestamp}<color={color}>{message}</color>";

        // Add to queue
        messageQueue.Enqueue(formatted);

        // Remove oldest message if over limit
        while (messageQueue.Count > maxLines)
            messageQueue.Dequeue();

        // Refresh the text display
        RefreshDisplay();
    }

    /// <summary>Rebuilds the display text from the current message queue.</summary>
    private void RefreshDisplay()
    {
        if (displayText == null) return;
        displayText.text = string.Join("\n", messageQueue);
    }

    /// <summary>Snaps panel to its position without smoothing.</summary>
    private void SnapToPosition()
    {
        transform.position = playerCamera.transform.TransformPoint(offsetFromCamera);
        transform.rotation = Quaternion.LookRotation(
            transform.position - playerCamera.transform.position);
    }

    /// <summary>
    /// Reads controller thumbstick clicks for panel control.
    /// Right stick click = toggle visibility.
    /// Left stick click = clear messages.
    /// </summary>
    private void HandleControllerInput()
    {
        // Right thumbstick click = toggle visibility
        // OVRInput.Button.SecondaryThumbstick = right controller thumbstick press
        if (enableToggle &&
            OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick))
        {
            ToggleVisibility();
        }

        // Left thumbstick click = clear messages
        // OVRInput.Button.PrimaryThumbstick = left controller thumbstick press
        if (enableClear &&
            OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick))
        {
            ClearMessages();
        }
    }
}