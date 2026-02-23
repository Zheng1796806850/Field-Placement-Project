using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PhaseClockHUD : MonoBehaviour
{
    [Header("Refs")]
    public GameStateManager gameState;

    [Header("UI")]
    public Image radialFill;
    public TextMeshProUGUI timeLabel;
    public TextMeshProUGUI phaseLabel;

    [Header("Fill")]
    public bool fillShowsRemaining = true;

    [Header("Text")]
    public bool showPhaseText = true;
    public string dayText = "Day";
    public string nightText = "Night";

    private void Awake()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (gameState == null) return;

        float total = gameState.CurrentPhase == DayNightPhase.Day ? Mathf.Max(0.01f, gameState.dayDuration) : Mathf.Max(0.01f, gameState.nightDuration);
        float remaining = Mathf.Clamp(gameState.PhaseTimeRemaining, 0f, total);
        float elapsed = Mathf.Clamp(gameState.PhaseElapsed, 0f, total);

        float fill01 = fillShowsRemaining ? (remaining / total) : (elapsed / total);

        if (radialFill != null) radialFill.fillAmount = Mathf.Clamp01(fill01);

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
}