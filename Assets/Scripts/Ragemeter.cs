using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tracks one player's rage on a 0–maxRage scale.
///
/// WHAT CHANGED FROM YOUR UPLOADED VERSION
/// ─────────────────────────────────────────
/// Two methods added at the bottom of the Public API section:
///   AddRage(float)  — direct rage injection used by RageSimulator
///   ResetRage()     — instant reset to zero used by RageSimulator loop mode
///
/// NOTHING ELSE IS RENAMED OR CHANGED.
/// playerIndex stays as [SerializeField] public int — exactly as you had it.
/// All existing method signatures, event names, and property names are identical.
/// </summary>
public class RageMeter : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Player Identity")]
    [Tooltip("0 = Player 1, 1 = Player 2. " +
             "Used by GameManager and DualRageBarUI to find the correct component. " +
             "Must be 0 on the first RageMeter and 1 on the second RageMeter " +
             "on the GameManager GameObject.")]
    [SerializeField] public int playerIndex = 0;

    [Header("Rage Level Config")]
    [Tooltip("ScriptableObject defining rage level thresholds, colours, haptics, " +
             "and weapon unlock prefabs.")]
    [SerializeField] private RageLevelConfig levelConfig;

    [Header("Rage Settings")]
    [Tooltip("Upper bound of the rage meter. Bar is full at this value.")]
    [SerializeField] private float maxRage = 100f;

    [Tooltip("Rage points lost per second while not hitting. " +
             "Recommended: 1.0 for multiplayer (slow drain).")]
    [SerializeField] private float rageDecayPerSecond = 1f;

    [Header("Scoring Multipliers")]
    [Tooltip("Scales collision impulse → rage. Recommended: 0.25.")]
    [SerializeField] private float forceMultiplier = 0.25f;

    [Tooltip("Scales swing speed → rage. Recommended: 0.8.")]
    [SerializeField] private float speedMultiplier = 0.8f;

    [Tooltip("Flat rage bonus on full object destruction. Recommended: 8.")]
    [SerializeField] private float breakBonus = 8f;

    [Header("Combo / Frequency")]
    [Tooltip("Seconds within which consecutive hits build the combo multiplier.")]
    [SerializeField] private float comboWindow = 2f;

    [Tooltip("How much the multiplier increases per hit inside the window.")]
    [SerializeField] private float comboMultiplierStep = 0.1f;

    [Tooltip("Maximum combo multiplier cap.")]
    [SerializeField] private float maxComboMultiplier = 2f;

    [Header("UI References (optional — leave null if using DualRageBarUI)")]
    [Tooltip("Optional per-player Slider. Leave null when DualRageBarUI handles display.")]
    [SerializeField] private Slider rageSlider;

    [Tooltip("Optional per-player rage text.")]
    [SerializeField] private TextMeshProUGUI rageText;

    [Tooltip("Optional per-player level name label.")]
    [SerializeField] private TextMeshProUGUI levelNameText;

    [Tooltip("Optional per-player fill image colour.")]
    [SerializeField] private Image rageBarFillImage;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired on every rage value change (gain or decay).
    /// DualRageBarUI subscribes here to refresh the shared bar.
    /// Parameters: (float newRage, float delta)
    /// </summary>
    public event Action<float, float> OnRageChanged;

    /// <summary>
    /// Fired only when the player crosses UP into a new rage tier.
    /// GameManager subscribes here and gates weapon unlock via OnSharedRageLevelUp.
    /// Parameters: (RageLevel newLevel, int levelIndex)
    /// </summary>
    public event Action<RageLevelConfig.RageLevel, int> OnRageLevelUp;

    // ── Runtime State ─────────────────────────────────────────────────────────

    private float currentRage;
    private float comboMultiplier = 1f;
    private float timeSinceLastHit = 999f;
    private int currentLevelIndex = 0;

    // ── Read-only Properties ──────────────────────────────────────────────────

    public float CurrentRage => currentRage;
    public float MaxRage => maxRage;
    public float ComboMultiplier => comboMultiplier;
    public int CurrentLevelIndex => currentLevelIndex;

    public RageLevelConfig.RageLevel CurrentLevel =>
        levelConfig != null ? levelConfig.levels[currentLevelIndex] : null;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        currentRage = 0f;
        comboMultiplier = 1f;
        timeSinceLastHit = 999f;
        currentLevelIndex = 0;
        UpdateUI();

        Debug.Log($"[RageMeter] Initialised — playerIndex={playerIndex} " +
                  $"instanceID={GetInstanceID()}");
    }

    private void Update()
    {
        timeSinceLastHit += Time.deltaTime;

        if (timeSinceLastHit > comboWindow)
            comboMultiplier = 1f;

        if (currentRage > 0f)
            ApplyRageDelta(-rageDecayPerSecond * Time.deltaTime);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by DestructibleObject each time the bat lands a valid hit.
    /// </summary>
    /// <summary>
    /// Registers a hit, updates local combo/UI/haptics, and returns the rage
    /// gain so the caller can forward it to NetworkedRageState.
    /// </summary>
    public float RegisterHit(float force, float swingSpeed, bool objectBroke)
    {
        if (timeSinceLastHit <= comboWindow)
            comboMultiplier = Mathf.Min(comboMultiplier + comboMultiplierStep, maxComboMultiplier);
        else
            comboMultiplier = 1f + comboMultiplierStep;

        timeSinceLastHit = 0f;

        float gain = force * forceMultiplier + swingSpeed * speedMultiplier;
        if (objectBroke) gain += breakBonus;
        gain *= comboMultiplier;

        ApplyRageDelta(gain);

        Debug.Log($"[RageMeter P{playerIndex + 1}] Hit — " +
                  $"Force:{force:F1} Speed:{swingSpeed:F1} Broke:{objectBroke} " +
                  $"Combo:×{comboMultiplier:F2} Gain:+{gain:F1} Total:{currentRage:F1}");

        return gain;
    }

    /// <summary>
    /// Directly adds rage points, bypassing force/speed/combo calculation.
    /// Used by RageSimulator for exact per-second fill rates.
    /// Positive = gain, negative = subtract. Always clamped to [0, maxRage].
    ///
    /// NEW — added for RageSimulator compatibility.
    /// </summary>
    public void AddRage(float points)
    {
        if (Mathf.Approximately(points, 0f)) return;
        ApplyRageDelta(points);
    }

    /// <summary>
    /// Instantly resets rage to zero and refreshes all UI.
    /// Resets combo multiplier and level index.
    /// Used by RageSimulator loop/demo mode.
    ///
    /// NEW — added for RageSimulator compatibility.
    /// </summary>
    public void ResetRage()
    {
        currentRage = 0f;
        comboMultiplier = 1f;
        timeSinceLastHit = 999f;
        currentLevelIndex = 0;
        OnRageChanged?.Invoke(0f, 0f);
        UpdateUI();
        Debug.Log($"[RageMeter P{playerIndex + 1}] Rage reset to 0.");
    }

    /// <summary>
    /// Passthrough — exists so DestructibleObject.SetRageMeter() compiles.
    /// </summary>
    public void SetRageMeter(RageMeter meter) { }

    /// <summary>Returns the current level name string.</summary>
    public string GetRageLabel() =>
        levelConfig != null
            ? levelConfig.GetLevelForRage(currentRage).levelName
            : GetFallbackLabel();

    // ── Internal ──────────────────────────────────────────────────────────────

    private void ApplyRageDelta(float delta)
    {
        float previous = currentRage;
        currentRage = Mathf.Clamp(currentRage + delta, 0f, maxRage);

        if (Mathf.Approximately(previous, currentRage)) return;

        OnRageChanged?.Invoke(currentRage, currentRage - previous);
        CheckLevelProgression(previous);
        UpdateUI();
    }

    private void CheckLevelProgression(float previousRage)
    {
        if (levelConfig == null) return;

        int newIndex = levelConfig.GetLevelIndexForRage(currentRage);
        if (newIndex == currentLevelIndex) return;

        if (newIndex > currentLevelIndex)
        {
            currentLevelIndex = newIndex;
            var level = levelConfig.levels[currentLevelIndex];
            OnRageLevelUp?.Invoke(level, currentLevelIndex);

            OVRInput.SetControllerVibration(
                level.hapticIntensity, level.hapticIntensity,
                OVRInput.Controller.All);
            Invoke(nameof(StopVibration), 0.4f);

            Debug.Log($"[RageMeter P{playerIndex + 1}] Level UP → " +
                      $"'{level.levelName}' (index {currentLevelIndex})");
        }
        else
        {
            currentLevelIndex = newIndex;
        }

        UpdateUI();
    }

    private void StopVibration() =>
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.All);

    private void UpdateUI()
    {
        if (rageSlider != null)
            rageSlider.value = currentRage / maxRage;

        string levelName = levelConfig != null
            ? levelConfig.GetLevelForRage(currentRage).levelName
            : GetFallbackLabel();

        if (rageText != null)
            rageText.text = comboMultiplier > 1.05f
                ? $"{currentRage:F0}  ×{comboMultiplier:F1}"
                : $"{currentRage:F0}";

        if (levelNameText != null)
            levelNameText.text = levelName;

        if (rageBarFillImage != null && levelConfig != null)
            rageBarFillImage.color = levelConfig.GetLevelForRage(currentRage).hudColor;
    }

    private string GetFallbackLabel()
    {
        if (currentRage < 20f) return "Calm";
        if (currentRage < 40f) return "Warming Up";
        if (currentRage < 60f) return "Angry";
        if (currentRage < 80f) return "Furious";
        return "Rage Mode";
    }
}