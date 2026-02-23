using TMPro;
using UnityEngine;

public class WaveWarningHUD : MonoBehaviour
{
    [Header("Refs")]
    public GameStateManager gameState;
    public WaveProgressTracker waveProgress;

    [Header("UI")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI label;

    [Header("Behavior")]
    public bool showOnlyAtNight = true;
    public bool showWhenWaveInProgress = true;
    public string warningText = "Wave";

    private void Awake()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (waveProgress == null) waveProgress = FindFirstObjectByType<WaveProgressTracker>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        Apply();
    }

    private void OnEnable()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (waveProgress == null) waveProgress = FindFirstObjectByType<WaveProgressTracker>();

        if (gameState != null) gameState.OnPhaseChanged += HandlePhaseChanged;

        if (waveProgress != null)
        {
            waveProgress.OnWaveStarted += HandleWaveStarted;
            waveProgress.OnWaveCompleted += HandleWaveCompleted;
        }

        Apply();
    }

    private void OnDisable()
    {
        if (gameState != null) gameState.OnPhaseChanged -= HandlePhaseChanged;

        if (waveProgress != null)
        {
            waveProgress.OnWaveStarted -= HandleWaveStarted;
            waveProgress.OnWaveCompleted -= HandleWaveCompleted;
        }
    }

    private void HandlePhaseChanged(DayNightPhase _) => Apply();

    private void HandleWaveStarted(int waveId)
    {
        if (label != null) label.text = string.IsNullOrWhiteSpace(warningText) ? $"Wave {waveId}" : $"{warningText} {waveId}";
        Apply();
    }

    private void HandleWaveCompleted(int _) => Apply();

    private void Apply()
    {
        bool isNight = gameState != null && gameState.CurrentPhase == DayNightPhase.Night;
        bool inWave = waveProgress != null && waveProgress.waveInProgress;

        bool visible = true;
        if (showOnlyAtNight) visible &= isNight;
        if (showWhenWaveInProgress) visible &= inWave;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (label != null && string.IsNullOrWhiteSpace(label.text))
        {
            int w = waveProgress != null ? waveProgress.currentWave : 0;
            label.text = string.IsNullOrWhiteSpace(warningText) ? (w > 0 ? $"Wave {w}" : "Wave") : (w > 0 ? $"{warningText} {w}" : warningText);
        }
    }
}