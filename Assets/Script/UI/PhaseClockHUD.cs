using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhaseClockHUD : MonoBehaviour
{
    [Header("Refs")]
    public GameStateManager gameState;

    [Header("UI")]
    public Image backgroundImage;
    public Image radialFill;
    public TextMeshProUGUI timeLabel;
    public TextMeshProUGUI phaseLabel;

    [Header("Day/Night Clock Visual")]
    public Sprite dayBackgroundSprite;
    public Sprite nightBackgroundSprite;
    public Sprite dayFillSprite;
    public Sprite nightFillSprite;
    public bool swapClockSpritesByPhase = true;

    [Header("Fill")]
    public bool fillShowsRemaining = true;
    public bool shrinkClockwise = true;
    public bool invertFillAmount = false;

    [Header("Text")]
    public bool showPhaseText = true;
    public string dayText = "Day";
    public string nightText = "Night";

    private void Awake()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (backgroundImage == null)
        {
            var bg = transform.Find("ClockBG");
            if (bg != null) backgroundImage = bg.GetComponent<Image>();
        }
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (gameState == null) return;

        if (swapClockSpritesByPhase)
            ApplyPhaseSprites(gameState.CurrentPhase);

        float total = gameState.CurrentPhase == DayNightPhase.Day ? Mathf.Max(0.01f, gameState.dayDuration) : Mathf.Max(0.01f, gameState.nightDuration);
        float remaining = Mathf.Clamp(gameState.PhaseTimeRemaining, 0f, total);
        float elapsed = Mathf.Clamp(gameState.PhaseElapsed, 0f, total);

        float fill01 = fillShowsRemaining ? (remaining / total) : (elapsed / total);
        if (invertFillAmount) fill01 = 1f - fill01;

        if (radialFill != null)
        {
            radialFill.fillClockwise = shrinkClockwise;
            radialFill.fillAmount = Mathf.Clamp01(fill01);
        }

        if (timeLabel != null)
        {
            int t = Mathf.CeilToInt(remaining);
            int m = t / 60;
            int s = t % 60;
            timeLabel.text = $"{m:00}:{s:00}";
        }

        if (phaseLabel != null)
        {
            if (!showPhaseText) phaseLabel.text = "";
            else phaseLabel.text = gameState.CurrentPhase == DayNightPhase.Day ? dayText : nightText;
        }
    }

    private void ApplyPhaseSprites(DayNightPhase phase)
    {
        if (phase == DayNightPhase.Day)
        {
            if (backgroundImage != null && dayBackgroundSprite != null && backgroundImage.sprite != dayBackgroundSprite)
                backgroundImage.sprite = dayBackgroundSprite;
            if (radialFill != null && dayFillSprite != null && radialFill.sprite != dayFillSprite)
                radialFill.sprite = dayFillSprite;
            return;
        }

        if (backgroundImage != null && nightBackgroundSprite != null && backgroundImage.sprite != nightBackgroundSprite)
            backgroundImage.sprite = nightBackgroundSprite;
        if (radialFill != null && nightFillSprite != null && radialFill.sprite != nightFillSprite)
            radialFill.sprite = nightFillSprite;
    }
}