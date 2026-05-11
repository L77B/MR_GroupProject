using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tracks the player's rage score on a 0–100 scale.
///
/// SCORING MODEL
/// ─────────────
/// Each hit contributes rage based on three factors:
///   1. Force       — how hard the bat struck the object (collision impulse)
///   2. Swing speed — how fast the bat was moving at the moment of impact
///   3. Break bonus — a flat bonus awarded when the object is fully destroyed
///
/// On top of the base score a COMBO MULTIPLIER scales the gain:
///   - Each hit within the comboWindow resets the timer and increases the multiplier.
///   - If the player stops hitting, the multiplier resets to 1× after comboWindow seconds.
///   - The multiplier is capped at maxComboMultiplier to prevent runaway scoring.
///
/// DECAY
/// ─────
/// Rage decays at rageDecayPerSecond every frame so the meter falls when the
/// player is idle, rewarding sustained aggression.
///
/// EVENTS
/// ──────
/// OnRageChanged  — fired whenever the value changes (for HUD updates).
/// OnRageLevelUp  — fired when the player crosses into a higher tier
///                  (for WeaponRack unlocks, haptics, announcements).
///
/// UI WIRING (assign in Inspector)
/// ────────────────────────────────
///   rageSlider      — Slider whose value shows 0–1 fill
///   rageText        — Text showing score + combo info
///   levelNameText   — Large label e.g. "FURIOUS"
///   rageBarFillImage — The fill Image of the slider (for colour changes)
/// </summary>
public class RageMeter : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("Rage Level Config")]
    [Tooltip("ScriptableObject defining the rage level thresholds, colours, and weapon unlocks. " +
             "Create via Assets → Create → RageRoom → Rage Level Config.")]
    [SerializeField] private RageLevelConfig levelConfig;

    [Header("Rage Settings")]
    [Tooltip("Upper bound of the rage meter. The bar is full at this value.")]
    [SerializeField] private float maxRage = 100f;

    [Tooltip("How many rage points are lost per second while the player is not hitting.")]
    [SerializeField] private float rageDecayPerSecond = 4f;

    [Header("Scoring Multipliers")]
    [Tooltip("Scales the collision impulse into rage points. Higher = harder hits give more rage.")]
    [SerializeField] private float forceMultiplier = 1.5f;

    [Tooltip("Scales the bat's tip speed into rage points. Higher = faster swings give more rage.")]
    [SerializeField] private float speedMultiplier = 4f;

    [Tooltip("Flat rage bonus added whenever an object is fully destroyed.")]
    [SerializeField] private float breakBonus = 20f;

    [Header("Combo / Frequency")]
    [Tooltip("If consecutive hits occur within this many seconds, the combo multiplier builds up.")]
    [SerializeField] private float comboWindow = 2f;

    [Tooltip("How much the combo multiplier increases with each hit inside the window.")]
    [SerializeField] private float comboMultiplierStep = 0.15f;

    [Tooltip("The maximum value the combo multiplier can reach.")]
    [SerializeField] private float maxComboMultiplier = 3f;

    [Header("UI References")]
    [Tooltip("Slider component that visually represents rage level (0 = empty, 1 = full).")]
    [SerializeField] private Slider rageSlider;

    [Tooltip("Text element showing the numeric rage value and current combo multiplier.")]
    [SerializeField] private Text rageText;

    [Tooltip("Large text element showing the current rage level name e.g. 'FURIOUS'.")]
    [SerializeField] private Text levelNameText;

    [Tooltip("The Fill Image child of the slider. Its colour updates to match the current level colour.")]
    [SerializeField] private Image rageBarFillImage;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired whenever the rage value changes (both increases and decreases).
    /// Parameters: (float newRage, float delta) — delta is positive on gain, negative on decay.
    /// Subscribe here to drive additional UI or effects.
    /// </summary>
    public event Action<float, float> OnRageChanged;

    /// <summary>
    /// Fired only when the player crosses UP into a new rage tier (not on decay).
    /// Parameters: (RageLevel newLevel, int levelIndex).
    /// WeaponRack subscribes here to unlock weapons.
    /// </summary>
    public event Action<RageLevelConfig.RageLevel, int> OnRageLevelUp;

    // ── Runtime State ─────────────────────────────────────────────────────────

    private float currentRage;           // Current rage value (0–maxRage)
    private float comboMultiplier = 1f;  // Scales rage gain; resets when combo expires
    private float timeSinceLastHit = 999f; // Tracks how long since the last hit
    private int currentLevelIndex = 0; // Index into levelConfig.levels[]

    // ── Read-only Properties (for external systems) ───────────────────────────

    public float CurrentRage => currentRage;
    public float MaxRage => maxRage;
    public float ComboMultiplier => comboMultiplier;
    public int CurrentLevelIndex => currentLevelIndex;

    /// <summary>Returns the full RageLevel data for the player's current tier.</summary>
    public RageLevelConfig.RageLevel CurrentLevel =>
        levelConfig != null ? levelConfig.levels[currentLevelIndex] : null;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        // Reset all values to a clean initial state
        currentRage = 0f;
        comboMultiplier = 1f;
        timeSinceLastHit = 999f;
        currentLevelIndex = 0;

        // Populate the UI immediately so the bar shows 0% at startup
        UpdateUI();
    }

    private void Update()
    {
        // Advance the timer that tracks how recently the player last hit something
        timeSinceLastHit += Time.deltaTime;

        // When the combo window expires, drop the multiplier back to 1×
        if (timeSinceLastHit > comboWindow)
            comboMultiplier = 1f;

        // Gradually drain rage while the player is idle
        if (currentRage > 0f)
        {
            float decayDelta = -rageDecayPerSecond * Time.deltaTime;
            ApplyRageDelta(decayDelta);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by DestructibleObject each time the bat lands a valid hit.
    /// Calculates rage gain from force, speed, break bonus, and the current combo multiplier,
    /// then applies it to the rage value.
    /// </summary>
    /// <param name="force">
    ///   Collision impulse magnitude passed from BatImpactHandler.
    ///   Higher values mean harder strikes.
    /// </param>
    /// <param name="swingSpeed">
    ///   Bat tip speed in m/s at the moment of impact.
    ///   Higher values mean faster, more aggressive swings.
    /// </param>
    /// <param name="objectBroke">
    ///   True if this hit was the killing blow that destroyed the object.
    ///   Triggers the breakBonus on top of the normal gain.
    /// </param>
    public void RegisterHit(float force, float swingSpeed, bool objectBroke)
    {
        // ── Combo multiplier update ───────────────────────────────────────────
        if (timeSinceLastHit <= comboWindow)
        {
            // Still within the combo window — increase the multiplier
            comboMultiplier = Mathf.Min(comboMultiplier + comboMultiplierStep, maxComboMultiplier);
        }
        else
        {
            // Fresh combo starting — begin at 1× plus one step so the very
            // first hit in a new chain already rewards the player
            comboMultiplier = 1f + comboMultiplierStep;
        }

        // Reset the combo timer for this hit
        timeSinceLastHit = 0f;

        // ── Rage gain calculation ─────────────────────────────────────────────

        float rageGain = 0f;

        // Hard hits add more rage than soft taps
        rageGain += force * forceMultiplier;

        // Fast swings add more rage than slow pushes
        rageGain += swingSpeed * speedMultiplier;

        // Fully destroying an object gives a significant bonus
        if (objectBroke)
            rageGain += breakBonus;

        // Scale everything by the current combo multiplier
        rageGain *= comboMultiplier;

        // Apply the calculated gain to the rage value
        ApplyRageDelta(rageGain);

        Debug.Log($"[RageMeter] Hit — Force:{force:F1} Speed:{swingSpeed:F1} " +
                  $"Broke:{objectBroke} Combo:×{comboMultiplier:F2} " +
                  $"Gain:+{rageGain:F1} Total:{currentRage:F1}");
    }

    // ── Internal Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Applies a positive (gain) or negative (decay) delta to the rage value,
    /// clamps the result, fires the change event, and checks for level transitions.
    /// </summary>
    private void ApplyRageDelta(float delta)
    {
        float previous = currentRage;

        // Clamp to valid range after applying the delta
        currentRage = Mathf.Clamp(currentRage + delta, 0f, maxRage);

        // Nothing changed (already at min or max) — skip the rest
        if (Mathf.Approximately(previous, currentRage)) return;

        // Notify subscribers of the new value
        OnRageChanged?.Invoke(currentRage, currentRage - previous);

        // Check whether the player has moved into a different rage tier
        CheckLevelProgression(previous);

        // Refresh all UI elements to reflect the new value
        UpdateUI();
    }

    /// <summary>
    /// Compares the current rage value against the level config thresholds.
    /// If the player has moved UP into a new tier, fires OnRageLevelUp and
    /// triggers controller haptics. Downward tier changes from decay update
    /// the index silently without firing the event.
    /// </summary>
    private void CheckLevelProgression(float previousRage)
    {
        if (levelConfig == null) return;

        int newIndex = levelConfig.GetLevelIndexForRage(currentRage);

        // Only act if the tier index has actually changed
        if (newIndex == currentLevelIndex) return;

        if (newIndex > currentLevelIndex)
        {
            // ── Level UP ──────────────────────────────────────────────────────
            currentLevelIndex = newIndex;
            var level = levelConfig.levels[currentLevelIndex];

            // Fire the event — WeaponRack listens here to unlock new weapons
            OnRageLevelUp?.Invoke(level, currentLevelIndex);

            // Buzz both controllers at the intensity defined for this level
            OVRInput.SetControllerVibration(
                level.hapticIntensity, level.hapticIntensity,
                OVRInput.Controller.All);

            // Schedule vibration stop so it doesn't run indefinitely
            Invoke(nameof(StopVibration), 0.4f);

            Debug.Log($"[RageMeter] Level UP → {level.levelName} (index {currentLevelIndex})");
        }
        else
        {
            // ── Level DOWN (decay) ────────────────────────────────────────────
            // Update index silently — no event, no haptics
            currentLevelIndex = newIndex;
        }

        // Colour and label may have changed; refresh UI
        UpdateUI();
    }

    /// <summary>
    /// Cancels controller vibration.
    /// Called via Invoke() a short delay after a level-up haptic burst.
    /// </summary>
    private void StopVibration()
    {
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.All);
    }

    /// <summary>
    /// Pushes the current rage state to all connected UI elements:
    ///   - Slider fill (0–1 normalised)
    ///   - Score and combo text
    ///   - Level name label
    ///   - Rage bar fill colour (driven by the current level's hudColor)
    /// </summary>
    private void UpdateUI()
    {
        // Normalise rage to 0–1 for the slider
        if (rageSlider != null)
            rageSlider.value = currentRage / maxRage;

        // Resolve the level name — use config if available, fallback otherwise
        string label = levelConfig != null
            ? levelConfig.GetLevelForRage(currentRage).levelName
            : GetFallbackLabel();

        // Show numeric rage value and live combo multiplier
        if (rageText != null)
            rageText.text = $"Rage: {currentRage:F0}%  ×{comboMultiplier:F1} combo";

        // Show the current level name as a prominent label
        if (levelNameText != null)
            levelNameText.text = label;

        // Tint the bar fill to match the current tier colour
        if (rageBarFillImage != null && levelConfig != null)
            rageBarFillImage.color = levelConfig.GetLevelForRage(currentRage).hudColor;
    }

    /// <summary>
    /// Returns a hard-coded level name string for when no RageLevelConfig is assigned.
    /// Allows the meter to function even without the ScriptableObject wired up.
    /// </summary>
    private string GetFallbackLabel()
    {
        if (currentRage < 20f) return "Calm";
        if (currentRage < 40f) return "Warming Up";
        if (currentRage < 60f) return "Angry";
        if (currentRage < 80f) return "Furious";
        return "Rage Mode";
    }

    /// <summary>
    /// Convenience accessor used by legacy or external UI code that only needs the label string.
    /// </summary>
    public string GetRageLabel() =>
        levelConfig != null
            ? levelConfig.GetLevelForRage(currentRage).levelName
            : GetFallbackLabel();
}