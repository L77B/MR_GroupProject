using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Collaborative dual rage bar — P1 fills BLUE from LEFT, P2 fills GREEN from RIGHT.
/// When they meet a RED blinking zone appears. When combined = full bar, FULL RED blinks.
///
/// SINGLETON GUARD — ONE BAR ONLY
/// ────────────────────────────────
/// Fusion's NetworkSceneManagerDefault re-instantiates every root GameObject
/// when it takes over the scene, creating duplicate Canvases and duplicate bars.
/// This class uses a static Instance guard: if a second DualRageBarUI tries to
/// Awake(), it immediately destroys its own parent Canvas and stops. Only the
/// first bar ever created survives.
///
/// Pair this with PersistentCanvas.cs on the Canvas parent to survive
/// Fusion scene takeovers cleanly.
/// </summary>
public class DualRageBarUI : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    private static DualRageBarUI _instance;

    // ── Inspector — RectTransforms ────────────────────────────────────────────

    [Header("Bar RectTransforms")]
    [SerializeField] private RectTransform barTrack;
    [SerializeField] private RectTransform fillBlue;
    [SerializeField] private RectTransform fillGreen;
    [SerializeField] private RectTransform fillRed;
    [SerializeField] private RectTransform fillVictory;

    [Header("Image Components")]
    [SerializeField] private Image imageBlue;
    [SerializeField] private Image imageGreen;
    [SerializeField] private Image imageRed;
    [SerializeField] private Image imageVictory;

    [Header("Text Labels")]
    [SerializeField] private TextMeshProUGUI labelP1;
    [SerializeField] private TextMeshProUGUI labelP2;

    [Tooltip("Starts INACTIVE. Shown only on victory.")]
    [SerializeField] private TextMeshProUGUI labelCenter;

    [SerializeField] private TextMeshProUGUI levelLabelP1;
    [SerializeField] private TextMeshProUGUI levelLabelP2;

    [Header("Config")]
    [SerializeField] private RageLevelConfig levelConfig;

    [Tooltip("Blink interval for the red meeting zone (seconds).")]
    [SerializeField] private float blinkPeriod = 0.25f;

    [Tooltip("Blink interval for the full victory bar (seconds).")]
    [SerializeField] private float victoryBlinkPeriod = 0.2f;

    [Tooltip("How long the victory state stays locked before rage decay can cancel it. " +
             "Default 10s so players see the full red bar clearly.")]
    [SerializeField] private float victoryHoldSeconds = 10f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColBlue = new Color(0.094f, 0.373f, 0.647f, 1.0f);
    private static readonly Color ColGreen = new Color(0.231f, 0.427f, 0.067f, 1.0f);
    private static readonly Color ColRed = new Color(0.886f, 0.294f, 0.290f, 1.0f);
    private static readonly Color ColDim = new Color(0.886f, 0.294f, 0.290f, 0.15f);
    private static readonly Color ColHidden = new Color(0.886f, 0.294f, 0.290f, 0.0f);

    // ── Runtime state ─────────────────────────────────────────────────────────

    private RageMeter activeMeterP1;
    private RageMeter activeMeterP2;

    private bool victoryAchieved = false;
    private bool victoryLocked = false;
    private bool redIsBlinking = false;
    private Coroutine redBlink = null;
    private Coroutine vicBlink = null;
    private Coroutine victoryHold = null;
    private float debugTimer = 0f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // ── Singleton guard ───────────────────────────────────────────────────
        // If another DualRageBarUI already exists, this is a Fusion duplicate.
        // Destroy the root Canvas that contains this duplicate and stop.
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[DualRageBarUI] Duplicate detected — destroying this Canvas. " +
                             "Only one DualRageBarUI is allowed in the scene.");
            // Destroy the Canvas root (parent of DualRageBar) not just this GO
            // so the whole duplicate UI disappears, not just the script.
            Transform root = transform;
            while (root.parent != null) root = root.parent;
            Destroy(root.gameObject);
            return;
        }

        _instance = this;

        // ── Colours ───────────────────────────────────────────────────────────
        if (imageBlue) imageBlue.color = ColBlue;
        if (imageGreen) imageGreen.color = ColGreen;
        if (imageRed) imageRed.color = ColRed;
        if (imageVictory) imageVictory.color = ColHidden;

        // ── Hide dynamic elements ─────────────────────────────────────────────
        if (labelCenter != null) labelCenter.gameObject.SetActive(false);
        if (fillVictory != null) fillVictory.gameObject.SetActive(false);
        if (fillRed != null) fillRed.gameObject.SetActive(false);

        SetWidth(fillBlue, 0f);
        SetWidth(fillGreen, 0f);
        SetWidth(fillRed, 0f);

        Debug.Log("[DualRageBarUI] Awake — singleton registered. " +
                  "Will find meters by playerIndex each Update().");
    }

    private void OnDestroy()
    {
        // Clear singleton so a fresh bar can register if the scene reloads
        if (_instance == this) _instance = null;
    }

    private void Start()
    {
        Debug.Log("[DualRageBarUI] Start — Update() drives render every frame.");
    }

    // ── Meter fetch ───────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the current active P1 and P2 RageMeters by playerIndex.
    /// When multiple components exist due to Fusion duplications, picks the one
    /// with the highest instanceID — newest component = currently active one.
    /// Called every Update() so new meters are always picked up.
    /// </summary>
    private void FetchCurrentMeters()
    {
        RageMeter newP1 = null, newP2 = null;

        var all = FindObjectsByType<RageMeter>(FindObjectsSortMode.None);
        foreach (var m in all)
        {
            if (m.playerIndex == 0 &&
                (newP1 == null || m.GetInstanceID() > newP1.GetInstanceID()))
                newP1 = m;

            if (m.playerIndex == 1 &&
                (newP2 == null || m.GetInstanceID() > newP2.GetInstanceID()))
                newP2 = m;
        }

        activeMeterP1 = newP1;
        activeMeterP2 = newP2;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        FetchCurrentMeters();
        RefreshBar();

        if (!debugLogging) return;
        debugTimer += Time.deltaTime;
        if (debugTimer < 2f) return;
        debugTimer = 0f;

        float barW = barTrack != null ? barTrack.rect.width : -1f;
        float r1 = activeMeterP1 != null ? activeMeterP1.CurrentRage : -1f;
        float r2 = activeMeterP2 != null ? activeMeterP2.CurrentRage : -1f;
        int id1 = activeMeterP1 != null ? activeMeterP1.GetInstanceID() : 0;
        int id2 = activeMeterP2 != null ? activeMeterP2.GetInstanceID() : 0;

        Debug.Log($"[DualRageBarUI] barW={barW:F0}px  " +
                  $"P1={r1:F1}(id={id1})  P2={r2:F1}(id={id2})  " +
                  $"victory={victoryAchieved}  locked={victoryLocked}  " +
                  $"redBlinking={redIsBlinking}");
    }

    // ── Core render ───────────────────────────────────────────────────────────

    private void RefreshBar()
    {
        if (barTrack == null) return;

        float barW = barTrack.rect.width;
        if (barW < 2f) return;

        float r1 = activeMeterP1 != null ? activeMeterP1.CurrentRage : 0f;
        float r2 = activeMeterP2 != null ? activeMeterP2.CurrentRage : 0f;
        float max1 = activeMeterP1 != null ? activeMeterP1.MaxRage : 100f;
        float max2 = activeMeterP2 != null ? activeMeterP2.MaxRage : 100f;

        float p1px = barW * Mathf.Clamp01(r1 / max1);
        float p2px = barW * Mathf.Clamp01(r2 / max2);
        float combined = p1px + p2px;

        // ── Victory ───────────────────────────────────────────────────────────
        bool isVictory = combined >= barW - 1f;

        if (isVictory && !victoryAchieved)
        {
            victoryAchieved = true;
            victoryLocked = true;
            ShowVictory();
            if (victoryHold != null) StopCoroutine(victoryHold);
            victoryHold = StartCoroutine(VictoryHoldTimer());
            return;
        }

        if (victoryAchieved && !isVictory && !victoryLocked)
            CancelVictory();

        if (victoryAchieved) return;

        // ── Segment widths ────────────────────────────────────────────────────
        float redPx = Mathf.Max(0f, combined - barW);
        float bluePx = Mathf.Max(0f, p1px - redPx);
        float greenPx = Mathf.Max(0f, p2px - redPx);

        SetWidth(fillBlue, bluePx);
        SetWidth(fillGreen, greenPx);

        bool showRed = redPx > 0.5f;

        if (fillRed != null)
        {
            if (showRed)
            {
                if (!fillRed.gameObject.activeSelf)
                    fillRed.gameObject.SetActive(true);

                SetWidth(fillRed, redPx);

                var ap = fillRed.anchoredPosition;
                ap.x = bluePx;
                fillRed.anchoredPosition = ap;

                if (!redIsBlinking)
                {
                    redIsBlinking = true;
                    if (redBlink != null) StopCoroutine(redBlink);
                    redBlink = StartCoroutine(BlinkRed());
                }
            }
            else
            {
                if (fillRed.gameObject.activeSelf)
                    fillRed.gameObject.SetActive(false);

                if (redIsBlinking)
                {
                    redIsBlinking = false;
                    if (redBlink != null) { StopCoroutine(redBlink); redBlink = null; }
                    if (imageRed) imageRed.color = ColRed;
                }
            }
        }

        UpdateLabels(r1, r2);
    }

    // ── Victory ───────────────────────────────────────────────────────────────

    private void ShowVictory()
    {
        if (fillBlue != null) fillBlue.gameObject.SetActive(false);
        if (fillGreen != null) fillGreen.gameObject.SetActive(false);
        if (fillRed != null) fillRed.gameObject.SetActive(false);

        redIsBlinking = false;
        if (redBlink != null) { StopCoroutine(redBlink); redBlink = null; }

        if (fillVictory != null) fillVictory.gameObject.SetActive(true);

        if (vicBlink != null) StopCoroutine(vicBlink);
        vicBlink = StartCoroutine(BlinkVictory());

        if (labelCenter != null)
        {
            labelCenter.gameObject.SetActive(true);
            labelCenter.text = "FULL RAGE!";
        }

        if (labelP1) labelP1.text = "";
        if (labelP2) labelP2.text = "";
        if (levelLabelP1) levelLabelP1.text = "MAX";
        if (levelLabelP2) levelLabelP2.text = "MAX";

        Debug.Log($"[DualRageBarUI] *** VICTORY — full red bar! Holding for {victoryHoldSeconds}s ***");
    }

    private IEnumerator VictoryHoldTimer()
    {
        yield return new WaitForSeconds(victoryHoldSeconds);
        victoryLocked = false;
        Debug.Log("[DualRageBarUI] Victory hold expired — decay can now cancel.");
    }

    private void CancelVictory()
    {
        victoryAchieved = false;
        victoryLocked = false;

        if (victoryHold != null) { StopCoroutine(victoryHold); victoryHold = null; }
        if (vicBlink != null) { StopCoroutine(vicBlink); vicBlink = null; }

        if (imageVictory) imageVictory.color = ColHidden;
        if (fillVictory != null) fillVictory.gameObject.SetActive(false);
        if (fillBlue != null) fillBlue.gameObject.SetActive(true);
        if (fillGreen != null) fillGreen.gameObject.SetActive(true);
        if (labelCenter != null) labelCenter.gameObject.SetActive(false);

        Debug.Log("[DualRageBarUI] Victory cancelled — rage decayed below full.");
    }

    // ── Blink coroutines ──────────────────────────────────────────────────────

    private IEnumerator BlinkRed()
    {
        var wait = new WaitForSeconds(blinkPeriod * 0.5f);
        bool bright = true;
        while (true)
        {
            if (imageRed) imageRed.color = bright ? ColRed : ColDim;
            bright = !bright;
            yield return wait;
        }
    }

    private IEnumerator BlinkVictory()
    {
        var wait = new WaitForSeconds(victoryBlinkPeriod * 0.5f);
        bool bright = true;
        while (true)
        {
            if (imageVictory) imageVictory.color = bright ? ColRed : ColDim;
            bright = !bright;
            yield return wait;
        }
    }

    // ── Labels ────────────────────────────────────────────────────────────────

    private void UpdateLabels(float r1, float r2)
    {
        if (labelP1 != null)
            labelP1.text = r1 > 3f ? Mathf.RoundToInt(r1).ToString() : "";

        if (labelP2 != null)
            labelP2.text = r2 > 3f ? Mathf.RoundToInt(r2).ToString() : "";

        if (levelLabelP1 != null) levelLabelP1.text = LevelName(r1);
        if (levelLabelP2 != null) levelLabelP2.text = LevelName(r2);
    }

    private string LevelName(float rage)
    {
        if (levelConfig != null) return levelConfig.GetLevelForRage(rage).levelName;
        if (rage < 20f) return "Calm";
        if (rage < 40f) return "Warming up";
        if (rage < 60f) return "Angry";
        if (rage < 80f) return "Furious";
        return "Rage mode";
    }

    private static void SetWidth(RectTransform rt, float px)
    {
        if (rt == null) return;
        var sd = rt.sizeDelta;
        if (Mathf.Approximately(sd.x, px)) return;
        sd.x = px;
        rt.sizeDelta = sd;
    }
}