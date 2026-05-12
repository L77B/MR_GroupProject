using UnityEngine;
using TMPro;

/// <summary>
/// Displays a world-space health label above a breakable object.
/// The label always faces the player (billboard effect) and updates
/// every time the object takes damage.
///
/// SETUP
/// ─────
/// 1. Create a World Space Canvas as a child of the breakable object prefab.
/// 2. Add a TextMeshPro - Text (UI) child inside that Canvas.
/// 3. Attach this script to the Canvas GameObject.
/// 4. Drag the TextMeshPro component into the labelText slot.
/// 5. DestructibleObject.TakeHit() calls UpdateHealth() automatically.
///
/// CANVAS SETTINGS
/// ───────────────
/// Render Mode    → World Space
/// Width          → 0.3
/// Height         → 0.1
/// Scale          → 0.003, 0.003, 0.003
/// Position       → (0, 0.6, 0) — above the object centre
///
/// VISUAL DESIGN
/// ─────────────
/// Health colour changes from green → yellow → red as health decreases.
/// A small background panel behind the text improves readability in MR.
/// </summary>
public class HealthLabel : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("UI References")]
    [Tooltip("The TextMeshPro component that displays the health value. " +
             "Must be a child of this World Space Canvas.")]
    [SerializeField] private TextMeshProUGUI labelText;

    [Header("Label Settings")]
    [Tooltip("Text shown before the health number. E.g. 'HP: ' shows 'HP: 75'.")]
    [SerializeField] private string prefix = "HP: ";

    [Tooltip("If true, shows health as a percentage (0-100%) instead of raw value.")]
    [SerializeField] private bool showAsPercentage = false;

    [Tooltip("If true, shows a health bar made of Unicode block characters " +
             "below the number. E.g. '████░░░░'.")]
    [SerializeField] private bool showHealthBar = true;

    [Tooltip("Number of segments in the health bar.")]
    [SerializeField] private int barSegments = 8;

    [Header("Colours")]
    [Tooltip("Label colour when health is above highThreshold.")]
    [SerializeField] private Color highHealthColor = Color.green;

    [Tooltip("Label colour when health is between lowThreshold and highThreshold.")]
    [SerializeField] private Color midHealthColor = Color.yellow;

    [Tooltip("Label colour when health is below lowThreshold.")]
    [SerializeField] private Color lowHealthColor = Color.red;

    [Tooltip("Health percentage above which the label is green.")]
    [SerializeField] private float highThreshold = 0.6f;

    [Tooltip("Health percentage below which the label is red.")]
    [SerializeField] private float lowThreshold = 0.3f;

    [Header("Billboard")]
    [Tooltip("If true, the label always rotates to face the player's camera. " +
             "Keeps the text readable from any angle.")]
    [SerializeField] private bool billboard = true;

    [Tooltip("Vertical offset above the object's centre where the label appears.")]
    [SerializeField] private float heightOffset = 0.4f;

    // ── Runtime State ─────────────────────────────────────────────────────────

    private float maxHealth;     // Set once by DestructibleObject on initialisation
    private float currentHealth; // Updated on every hit via UpdateHealth()
    private Camera playerCamera; // Cached for billboard rotation

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Cache the main camera for billboard rotation
        // On Quest 3 the centre eye camera is tagged MainCamera
        playerCamera = Camera.main;

        // Apply height offset so label sits above the object
        transform.localPosition = new Vector3(0f, heightOffset, 0f);
    }

    private void LateUpdate()
    {
        // Billboard — rotate label to always face the player
        // LateUpdate ensures this runs after all position updates
        if (billboard && playerCamera != null)
        {
            // Look at the camera but only rotate around Y axis
            // so the label stays upright and does not tilt
            Vector3 dirToCamera = playerCamera.transform.position - transform.position;
            dirToCamera.y = 0f; // Keep label vertical — only rotate on Y

            if (dirToCamera != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(-dirToCamera);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by DestructibleObject.Awake() to set the starting health values.
    /// Must be called before any UpdateHealth() calls.
    /// </summary>
    /// <param name="max">The maximum health of this object.</param>
    public void Initialise(float max)
    {
        maxHealth = max;
        currentHealth = max;
        RefreshLabel();
    }

    /// <summary>
    /// Called by DestructibleObject.TakeHit() after each hit to update the display.
    /// </summary>
    /// <param name="current">The object's current health after the hit.</param>
    /// <param name="max">The object's maximum health (for percentage calculation).</param>
    public void UpdateHealth(float current, float max)
    {
        currentHealth = current;
        maxHealth = max;
        RefreshLabel();
    }

    /// <summary>
    /// Called by DestructibleObject.Break() when the object is destroyed.
    /// Hides the label immediately so it does not float above fragments.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the label text and updates its colour based on current health.
    /// Called after every health change.
    /// </summary>
    private void RefreshLabel()
    {
        if (labelText == null) return;

        // Clamp health to valid range before display
        float clamped = Mathf.Clamp(currentHealth, 0f, maxHealth);
        float percentage = maxHealth > 0f ? clamped / maxHealth : 0f;

        // ── Build display string ──────────────────────────────────────────────
        string healthString;

        if (showAsPercentage)
        {
            // Show as percentage e.g. "HP: 75%"
            healthString = $"{prefix}{percentage * 100f:F0}%";
        }
        else
        {
            // Show as raw value e.g. "HP: 75 / 100"
            healthString = $"{prefix}{clamped:F0} / {maxHealth:F0}";
        }

        // ── Append health bar if enabled ──────────────────────────────────────
        // Uses Unicode block characters to draw a simple bar:
        // Full block ██ = healthy segments
        // Light shade ░░ = missing health segments
        if (showHealthBar)
        {
            int filledSegments = Mathf.RoundToInt(percentage * barSegments);
            filledSegments = Mathf.Clamp(filledSegments, 0, barSegments);

            string bar = "\n"; // New line below the number
            for (int i = 0; i < barSegments; i++)
                bar += i < filledSegments ? "█" : "░";

            healthString += bar;
        }

        labelText.text = healthString;

        // ── Update colour based on health percentage ──────────────────────────
        if (percentage > highThreshold)
            labelText.color = highHealthColor;  // Green — healthy
        else if (percentage > lowThreshold)
            labelText.color = midHealthColor;   // Yellow — damaged
        else
            labelText.color = lowHealthColor;   // Red — critical
    }
}