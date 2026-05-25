using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tracks one player's rage on a 0–maxRage scale.
///
/// CHANGES FROM SINGLE-PLAYER VERSION
/// ────────────────────────────────────
/// 1. ComboMultiplier is now public — DualRageBarUI reads it to show ×N labels.
/// 2. PlayerIndex field added (0 = P1, 1 = P2) — used by GameManager and
///    ObjectWaveManager to inject the correct meter into each DestructibleObject.
/// 3. Individual Slider/Text/LevelText UI refs are now OPTIONAL — if you leave
///    them null, DualRageBarUI handles all bar display. The meter still works
///    and fires events; it just won't try to touch a null Slider.
/// 4. Photon Fusion upgrade path: when you add Fusion, this class changes from
///    MonoBehaviour to NetworkBehaviour and currentRage becomes a [Networked]
///    float. The rest of the logic stays identical. See FUSION UPGRADE below.
///
/// PHOTON FUSION UPGRADE PATH
/// ───────────────────────────
/// Step 1: Add Fusion SDK to project.
/// Step 2: Change "MonoBehaviour" → "NetworkBehaviour" (Fusion namespace).
/// Step 3: Add [Networked] attribute to currentRage:
///           [Networked] private float currentRage { get; set; }
/// Step 4: Move UpdateUI() into OnChanged callback:
///           [OnChangedRender(nameof(currentRage))] void OnRageChanged() => UpdateUI();
/// Step 5: Gate RegisterHit() with if (!HasStateAuthority) return; so only the
///         local player who owns this meter can write to it.
/// Step 6: DualRageBarUI reads CurrentRage (which is now the Networked property)
///         — no changes needed there.
/// </summary>
public class RageMeter : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Player Identity")]
    [Tooltip("0 = Player 1, 1 = Player 2. " +
             "Used by GameManager to assign this meter to the correct player. " +
             "Also used for logging.")]
    [SerializeField] public int playerIndex = 0;

    [Header("Rage Level Config")]
    [SerializeField] private RageLevelConfig levelConfig;

    [Header("Rage Settings")]
    [Tooltip("Upper bound of the rage meter. Bar is full at this value.")]
    [SerializeField] private float maxRage = 100f;

    [Tooltip("Rage points lost per second while not hitting. " +
             "Lower = slower drain. Recommended: 1.0 for multiplayer.")]
    [SerializeField] private float rageDecayPerSecond = 1f;

    [Header("Scoring Multipliers")]
    [Tooltip("Scales collision impulse → rage. Recommended: 0.25 for 25% max per break.")]
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

    [Tooltip("Optional per-player rage text. Leave null when DualRageBarUI handles display.")]
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
    /// WeaponRack subscribes here to unlock weapons.
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
    public float ComboMultiplier => comboMultiplier;   // ← now public for DualRageBarUI
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
    /// Called by DestructibleObject on every valid hit.
    ///
    /// FUSION NOTE: when upgrading to NetworkBehaviour, add:
    ///   if (!HasStateAuthority) return;
    /// at the top of this method so only the owner's input writes rage.
    /// </summary>
    /// <summary>
    /// Registers a hit, updates local combo/UI/haptics, and returns the rage
    /// gain so the caller can forward it to NetworkedRageState.
    /// </summary>
    public float RegisterHit(float force, float swingSpeed, bool objectBroke)
    {
        // Combo update
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

    /// <summary>Inject the RageMeter reference used by SetRageMeter on DestructibleObject.</summary>
    public void SetRageMeter(RageMeter meter) { } // passthrough — exists for compatibility

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
                      $"{level.levelName} (index {currentLevelIndex})");
        }
        else
        {
            currentLevelIndex = newIndex;
        }

        UpdateUI();
    }

    private void StopVibration() =>
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.All);

    /// <summary>
    /// Updates per-player UI elements (Slider, Text, LevelName, FillColor).
    /// All fields are optional — if left null in the Inspector, this is a no-op
    /// and DualRageBarUI handles all display via the OnRageChanged event.
    /// </summary>
    private void UpdateUI()
    {
        if (rageSlider != null)
            rageSlider.value = currentRage / maxRage;

        string levelName = levelConfig != null
            ? levelConfig.GetLevelForRage(currentRage).levelName
            : GetFallbackLabel();

        if (rageText != null)
        {
            rageText.text = comboMultiplier > 1.05f
                ? $"{currentRage:F0}  ×{comboMultiplier:F1}"
                : $"{currentRage:F0}";
        }

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

    public string GetRageLabel() =>
        levelConfig != null
            ? levelConfig.GetLevelForRage(currentRage).levelName
            : GetFallbackLabel();
}