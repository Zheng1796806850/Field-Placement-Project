using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WaveHUDPackUI : MonoBehaviour
{
    [Header("Refs")]
    public GameStateManager gameState;
    public WaveProgressTracker waveProgress;

    [Header("Text")]
    public TextMeshProUGUI waveLabel;
    public TextMeshProUGUI phaseLabel;
    public TextMeshProUGUI countdownLabel;
    public TextMeshProUGUI enemyLabel;

    [Header("Enemy Progress")]
    public Image enemyProgressFill;
    public bool progressIsKilled = true;

    [Header("Countdown")]
    public bool showCountdownInDay = false;
    public bool showCountdownInNight = true;

    private void Awake()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (waveProgress == null) waveProgress = FindFirstObjectByType<WaveProgressTracker>();
    }

    private void OnEnable()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (waveProgress == null) waveProgress = FindFirstObjectByType<WaveProgressTracker>();

        if (gameState != null)
            gameState.OnPhaseChanged += HandlePhaseChanged;

        if (waveProgress != null)
        {
            waveProgress.OnWaveChanged += HandleWaveChanged;
            waveProgress.OnEnemyCountChanged += HandleEnemyCountChanged;
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (gameState != null)
            gameState.OnPhaseChanged -= HandlePhaseChanged;

        if (waveProgress != null)
        {
            waveProgress.OnWaveChanged -= HandleWaveChanged;
            waveProgress.OnEnemyCountChanged -= HandleEnemyCountChanged;
        }
    }

    private void Update()
    {
        UpdateCountdown();
    }

    private void RefreshAll()
    {
        UpdateWaveLabel();
        UpdatePhaseLabel();
        UpdateEnemyUI();
        UpdateCountdown();
    }

    private void HandlePhaseChanged(DayNightPhase phase)
    {
        UpdatePhaseLabel();
        UpdateCountdown();
    }

    private void HandleWaveChanged(int waveId)
    {
        UpdateWaveLabel();
        UpdateEnemyUI();
    }

    private void HandleEnemyCountChanged(int alive, int total)
    {
        UpdateEnemyUI();
    }

    private void UpdateWaveLabel()
    {
        if (waveLabel == null) return;

        int w = waveProgress != null ? waveProgress.currentWave : 0;
        if (w <= 0)
            waveLabel.text = "Wave -";
        else
            waveLabel.text = $"Wave {w}";
    }

    private void UpdatePhaseLabel()
    {
        if (phaseLabel == null) return;

        if (gameState == null)
        {
            phaseLabel.text = "";
            return;
        }

        phaseLabel.text = gameState.CurrentPhase == DayNightPhase.Night ? "Night" : "Day";
    }

    private void UpdateCountdown()
    {
        if (countdownLabel == null) return;
        if (gameState == null)
        {
            countdownLabel.text = "";
            return;
        }

        bool show = (gameState.CurrentPhase == DayNightPhase.Day) ? showCountdownInDay : showCountdownInNight;
        if (!show)
        {
            countdownLabel.text = "";
            return;
        }

        int t = Mathf.CeilToInt(Mathf.Max(0f, gameState.PhaseTimeRemaining));
        int m = t / 60;
        int s = t % 60;

        if (gameState.CurrentPhase == DayNightPhase.Night)
            countdownLabel.text = $"Dawn in {m:00}:{s:00}";
        else
            countdownLabel.text = $"Night in {m:00}:{s:00}";
    }

    private void UpdateEnemyUI()
    {
        int alive = waveProgress != null ? Mathf.Max(0, waveProgress.enemiesAlive) : 0;
        int total = waveProgress != null ? Mathf.Max(0, waveProgress.enemiesTotalThisWave) : 0;
        bool inWave = waveProgress != null && waveProgress.waveInProgress;

        if (enemyLabel != null)
        {
            if (!inWave || total <= 0)
                enemyLabel.text = "Enemies: -";
            else
                enemyLabel.text = $"Enemies: {alive}/{total}";
        }

        if (enemyProgressFill != null)
        {
            if (!inWave || total <= 0)
            {
                enemyProgressFill.fillAmount = 0f;
            }
            else
            {
                float alive01 = alive / (float)total;
                enemyProgressFill.fillAmount = progressIsKilled ? Mathf.Clamp01(1f - alive01) : Mathf.Clamp01(alive01);
            }
        }
    }
}
