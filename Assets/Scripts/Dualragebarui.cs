using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Collaborative dual rage bar for two players.
///
/// VISUAL BEHAVIOUR
/// ─────────────────
/// Three fill segments sit inside one bar track:
///
///   [=====BLUE=====][==RED==][=====GREEN=====]
///    P1 left→right   overlap  P2 right→left
///
/// Blue   (#185FA5)  — P1's portion, grows from the LEFT.
/// Green  (#3B6D11)  — P2's portion, grows from the RIGHT.
/// Red    (#E24B4A)  — the overlapping zone where both fills have met.
///                     Blinks continuously once it appears.
///
/// GOAL: When P1+P2 combined rage fills the ENTIRE bar (sum = 200, i.e.
/// both at 100), the whole bar turns red and blinks as a victory state.
///
/// RED ZONE SIZE
/// ──────────────
/// redZonePixels = max(0, (p1Pixels + p2Pixels) - barWidth)
///
/// The red zone starts where P1's blue fill ends (the meeting point)
/// and extends rightward, eating into P2's green.
///
/// CANVAS HIERARCHY
/// ─────────────────
///   Canvas (World Space)
///     └── DualRageBar           ← attach DualRageBarUI here
///           ├── BarTrack        ← Image, full width, dark bg  ← barTrack
///           ├── FillBlue        ← Image, #185FA5, anchored LEFT  ← fillBlue
///           ├── FillRed         ← Image, #E24B4A, free-positioned  ← fillRed
///           ├── FillGreen       ← Image, #3B6D11, anchored RIGHT  ← fillGreen
///           ├── FillVictory     ← Image, #E24B4A, full width, alpha 0  ← fillVictory
///           ├── LabelP1         ← TMP left, rage number  ← labelP1
///           ├── LabelP2         ← TMP right, rage number  ← labelP2
///           ├── LabelCenter     ← TMP center, "FULL RAGE!"  ← labelCenter
///           ├── LevelP1         ← TMP, level name  ← levelLabelP1
///           └── LevelP2         ← TMP, level name  ← levelLabelP2
///
/// RECT SETUP
/// ───────────
/// FillBlue:    Anchor Min=(0,0) Max=(0,1)  Pivot=(0,0.5)   sizeDelta.x = bluePixels
/// FillGreen:   Anchor Min=(1,0) Max=(1,1)  Pivot=(1,0.5)   sizeDelta.x = greenPixels
/// FillRed:     Anchor Min=(0,0) Max=(0,1)  Pivot=(0,0.5)   anchoredPosition.x = bluePixels
///              sizeDelta.x = redPixels
/// FillVictory: Anchor Min=(0,0) Max=(1,1)  Stretch full    alpha driven by blink coroutine
/// BarTrack:    Anchor stretch full width — used to read barWidth at runtime
/// </summary>
public class DualRageBarUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Bar Segments — assign RectTransforms")]
    [Tooltip("Full-width bar background. Used to read pixel width at runtime.")]
    [SerializeField] private RectTransform barTrack;

    [Tooltip("P1 blue fill image. Anchor LEFT. sizeDelta.x is set by script.")]
    [SerializeField] private RectTransform fillBlue;

    [Tooltip("Red overlap zone. Anchor LEFT, free x. Both anchoredPosition.x and sizeDelta.x set by script.")]
    [SerializeField] private RectTransform fillRed;

    [Tooltip("P2 green fill image. Anchor RIGHT. sizeDelta.x is set by script.")]
    [SerializeField] private RectTransform fillGreen;

    [Tooltip("Full-bar victory overlay. Stays alpha=0 until both fills sum to 100%. " +
             "Stretch anchor to fill entire bar. Image color = red.")]
    [SerializeField] private RectTransform fillVictory;

    [Header("Image Components (for colour and alpha)")]
    [SerializeField] private Image imageBlue;
    [SerializeField] private Image imageRed;
    [SerializeField] private Image imageGreen;
    [SerializeField] private Image imageVictory;

    [Header("Text Labels")]
    [Tooltip("Left side — shows P1 rage number.")]
    [SerializeField] private TextMeshProUGUI labelP1;

    [Tooltip("Right side — shows P2 rage number.")]
    [SerializeField] private TextMeshProUGUI labelP2;

    [Tooltip("Center — hidden normally, shows 'FULL RAGE!' on victory.")]
    [SerializeField] private TextMeshProUGUI labelCenter;

    [Tooltip("Level name label for P1 (e.g. 'FURIOUS').")]
    [SerializeField] private TextMeshProUGUI levelLabelP1;

    [Tooltip("Level name label for P2.")]
    [SerializeField] private TextMeshProUGUI levelLabelP2;

    [Header("Rage Meters")]
    [SerializeField] private RageMeter rageMeterP1;
    [SerializeField] private RageMeter rageMeterP2;

    [Header("Config")]
    [SerializeField] private RageLevelConfig levelConfig;

    [Tooltip("Blink frequency in seconds for the red zone (0.5 = fast blink).")]
    [SerializeField] private float blinkPeriod = 0.5f;

    [Tooltip("Blink frequency in seconds for the victory full-bar state.")]
    [SerializeField] private float victoryBlinkPeriod = 0.4f;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColorBlue = new Color(0.094f, 0.373f, 0.647f, 1f); // #185FA5
    private static readonly Color ColorGreen = new Color(0.231f, 0.427f, 0.067f, 1f); // #3B6D11
    private static readonly Color ColorRed = new Color(0.886f, 0.294f, 0.290f, 1f); // #E24B4A
    private static readonly Color ColorRedDim = new Color(0.886f, 0.294f, 0.290f, 0.2f);

    // ── State ─────────────────────────────────────────────────────────────────

    private bool victoryAchieved = false;
    private Coroutine redBlinkCoroutine;
    private Coroutine victoryBlinkCoroutine;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Set initial colours
        if (imageBlue) imageBlue.color = ColorBlue;
        if (imageGreen) imageGreen.color = ColorGreen;
        if (imageRed) imageRed.color = ColorRed;
        if (imageVictory) imageVictory.color = new Color(ColorRed.r, ColorRed.g, ColorRed.b, 0f);

        if (labelCenter) labelCenter.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (rageMeterP1 != null)
            rageMeterP1.OnRageChanged += OnAnyRageChanged;
        if (rageMeterP2 != null)
            rageMeterP2.OnRageChanged += OnAnyRageChanged;

        RefreshBar();
    }

    private void OnDestroy()
    {
        if (rageMeterP1 != null) rageMeterP1.OnRageChanged -= OnAnyRageChanged;
        if (rageMeterP2 != null) rageMeterP2.OnRageChanged -= OnAnyRageChanged;
    }

    // ── Event Handler ─────────────────────────────────────────────────────────

    private void OnAnyRageChanged(float rage, float delta) => RefreshBar();

    // ── Core Render ───────────────────────────────────────────────────────────

    /// <summary>
    /// Reads both rage values, computes segment widths, and updates all UI.
    ///
    /// SEGMENT MATH
    /// ─────────────
    /// barW        = total pixel width of the bar track
    /// p1px        = barW × (r1 / maxRage)          ← P1 wants this many pixels
    /// p2px        = barW × (r2 / maxRage)          ← P2 wants this many pixels
    /// combined    = p1px + p2px
    /// redPx       = max(0, combined - barW)         ← overlap = the red zone
    /// bluePx      = p1px - redPx                   ← blue shrinks as red grows
    /// greenPx     = p2px - redPx                   ← green shrinks as red grows
    ///
    /// Red zone position (anchoredPosition.x in left-anchored space):
    ///   redX = bluePx   ← red starts where blue ends
    ///
    /// VICTORY: when combined >= barW (both players together fill 100%)
    ///   → hide blue/red/green, show full-bar victory overlay, start blinking.
    /// </summary>
    public void RefreshBar()
    {
        if (barTrack == null) return;

        float barW = barTrack.rect.width;
        if (barW < 1f) return; // canvas not yet laid out

        float r1 = rageMeterP1 != null ? rageMeterP1.CurrentRage : 0f;
        float r2 = rageMeterP2 != null ? rageMeterP2.CurrentRage : 0f;
        float max1 = rageMeterP1 != null ? rageMeterP1.MaxRage : 100f;
        float max2 = rageMeterP2 != null ? rageMeterP2.MaxRage : 100f;

        float p1px = barW * Mathf.Clamp01(r1 / max1);
        float p2px = barW * Mathf.Clamp01(r2 / max2);
        float combined = p1px + p2px;

        // ── Victory check ─────────────────────────────────────────────────────
        bool isVictory = combined >= barW - 0.5f;

        if (isVictory && !victoryAchieved)
        {
            victoryAchieved = true;
            TriggerVictory();
            return;
        }

        if (victoryAchieved && !isVictory)
        {
            // Rage decayed below 100% combined — cancel victory state
            CancelVictory();
        }

        if (victoryAchieved) return; // skip normal render while in victory

        // ── Normal render ─────────────────────────────────────────────────────
        float redPx = Mathf.Max(0f, combined - barW);
        float bluePx = Mathf.Max(0f, p1px - redPx);
        float greenPx = Mathf.Max(0f, p2px - redPx);

        SetWidth(fillBlue, bluePx);
        SetWidth(fillGreen, greenPx);

        bool redVisible = redPx > 0.5f;

        if (fillRed != null)
        {
            fillRed.gameObject.SetActive(redVisible);
            if (redVisible)
            {
                SetWidth(fillRed, redPx);
                // Position red starting at the right edge of blue
                Vector2 ap = fillRed.anchoredPosition;
                ap.x = bluePx;
                fillRed.anchoredPosition = ap;

                // Start blinking coroutine if not already running
                if (redBlinkCoroutine == null)
                    redBlinkCoroutine = StartCoroutine(BlinkRed());
            }
            else
            {
                // Stop blinking when red zone disappears
                if (redBlinkCoroutine != null)
                {
                    StopCoroutine(redBlinkCoroutine);
                    redBlinkCoroutine = null;
                    if (imageRed) imageRed.color = ColorRed;
                }
            }
        }

        // ── Labels ────────────────────────────────────────────────────────────
        UpdateLabels(r1, r2, max1, max2);
    }

    // ── Victory State ─────────────────────────────────────────────────────────

    private void TriggerVictory()
    {
        // Hide the individual segments
        if (fillBlue) fillBlue.gameObject.SetActive(false);
        if (fillGreen) fillGreen.gameObject.SetActive(false);
        if (fillRed) fillRed.gameObject.SetActive(false);

        // Stop any existing blink
        if (redBlinkCoroutine != null) { StopCoroutine(redBlinkCoroutine); redBlinkCoroutine = null; }

        // Show victory overlay and start blinking
        if (fillVictory) fillVictory.gameObject.SetActive(true);
        if (victoryBlinkCoroutine == null)
            victoryBlinkCoroutine = StartCoroutine(BlinkVictory());

        // Show center label
        if (labelCenter)
        {
            labelCenter.gameObject.SetActive(true);
            labelCenter.text = "FULL RAGE!";
        }

        if (labelP1) labelP1.text = "";
        if (labelP2) labelP2.text = "";

        if (levelLabelP1) levelLabelP1.text = "MAX";
        if (levelLabelP2) levelLabelP2.text = "MAX";

        Debug.Log("[DualRageBarUI] VICTORY — full bar rage achieved by both players!");
    }

    private void CancelVictory()
    {
        victoryAchieved = false;

        if (victoryBlinkCoroutine != null)
        {
            StopCoroutine(victoryBlinkCoroutine);
            victoryBlinkCoroutine = null;
        }

        if (imageVictory) imageVictory.color = new Color(ColorRed.r, ColorRed.g, ColorRed.b, 0f);
        if (fillVictory) fillVictory.gameObject.SetActive(false);
        if (fillBlue) fillBlue.gameObject.SetActive(true);
        if (fillGreen) fillGreen.gameObject.SetActive(true);
        if (labelCenter) labelCenter.gameObject.SetActive(false);
    }

    // ── Blink Coroutines ──────────────────────────────────────────────────────

    /// <summary>
    /// Blinks the red overlap zone continuously while it has non-zero width.
    /// Alternates between full opacity and dim at blinkPeriod intervals.
    /// </summary>
    private IEnumerator BlinkRed()
    {
        bool bright = true;
        while (true)
        {
            if (imageRed)
                imageRed.color = bright ? ColorRed : ColorRedDim;
            bright = !bright;
            yield return new WaitForSeconds(blinkPeriod * 0.5f);
        }
    }

    /// <summary>
    /// Blinks the full victory bar between full red and dim red.
    /// </summary>
    private IEnumerator BlinkVictory()
    {
        bool bright = true;
        while (true)
        {
            if (imageVictory)
                imageVictory.color = bright
                    ? new Color(ColorRed.r, ColorRed.g, ColorRed.b, 1f)
                    : new Color(ColorRed.r, ColorRed.g, ColorRed.b, 0.25f);
            bright = !bright;
            yield return new WaitForSeconds(victoryBlinkPeriod * 0.5f);
        }
    }

    // ── Labels ────────────────────────────────────────────────────────────────

    private void UpdateLabels(float r1, float r2, float max1, float max2)
    {
        if (labelP1) labelP1.text = Mathf.RoundToInt(r1) > 3
            ? Mathf.RoundToInt(r1).ToString() : "";

        if (labelP2) labelP2.text = Mathf.RoundToInt(r2) > 3
            ? Mathf.RoundToInt(r2).ToString() : "";

        if (levelLabelP1) levelLabelP1.text = GetLevelName(r1);
        if (levelLabelP2) levelLabelP2.text = GetLevelName(r2);
    }

    private string GetLevelName(float rage)
    {
        if (levelConfig != null) return levelConfig.GetLevelForRage(rage).levelName;
        if (rage < 20f) return "Calm";
        if (rage < 40f) return "Warming up";
        if (rage < 60f) return "Angry";
        if (rage < 80f) return "Furious";
        return "Rage mode";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetWidth(RectTransform rt, float pixels)
    {
        if (rt == null) return;
        Vector2 sd = rt.sizeDelta;
        sd.x = pixels;
        rt.sizeDelta = sd;
    }
}