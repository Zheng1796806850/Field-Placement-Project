using System;
using UnityEngine;

public enum GameResult
{
    None,
    Victory,
    Defeat
}

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Refs")]
    public GameStateManager gameStateManager;
    public EndScreenUI endScreenUI;

    [Tooltip("负责记录波次/胜利条件。")]
    public WaveProgressTracker waveProgress;

    [Tooltip("负责刷怪（由 waveProgress 的 OnWaveStarted 驱动）。")]
    public WaveSpawnController2D waveSpawner;

    [Header("Runtime")]
    public GameResult result = GameResult.None;

    public event Action<GameResult, string> OnGameEnded;

    private bool _subscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (gameStateManager == null) gameStateManager = GameStateManager.Instance;
        if (waveProgress == null) waveProgress = FindFirstObjectByType<WaveProgressTracker>();
        if (waveSpawner == null) waveSpawner = FindFirstObjectByType<WaveSpawnController2D>();
    }

    private void Start()
    {
        TrySubscribe();
        WireSpawnerToTracker();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;

        if (gameStateManager == null)
            gameStateManager = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();

        if (gameStateManager == null)
        {
            Debug.LogWarning("[GameFlowManager] GameStateManager not found; cannot subscribe day/night events.");
            return;
        }

        gameStateManager.OnNightStarted += HandleNightStarted;
        gameStateManager.OnDayStarted += HandleDayStarted;

        _subscribed = true;
    }

    private void TryUnsubscribe()
    {
        if (!_subscribed) return;
        if (gameStateManager != null)
        {
            gameStateManager.OnNightStarted -= HandleNightStarted;
            gameStateManager.OnDayStarted -= HandleDayStarted;
        }
        _subscribed = false;
    }

    private void WireSpawnerToTracker()
    {
        if (waveSpawner != null && waveProgress != null)
        {
            waveSpawner.waveProgress = waveProgress;

            if (waveSpawner.waveConfig == null)
                waveSpawner.waveConfig = waveProgress.waveConfig;
        }
    }

    private void HandleNightStarted()
    {
        if (HasEnded) return;

        if (waveProgress == null)
            waveProgress = FindFirstObjectByType<WaveProgressTracker>();

        if (waveProgress == null)
        {
            Debug.LogError("[GameFlowManager] WaveProgressTracker not found; cannot start next wave.");
            return;
        }

        waveProgress.StartNextWave();
    }

    private void HandleDayStarted()
    {
        if (HasEnded) return;

        if (waveProgress == null)
            waveProgress = FindFirstObjectByType<WaveProgressTracker>();

        if (waveProgress != null)
        {
            waveProgress.HandleDayStarted();
        }
    }

    public bool HasEnded => result != GameResult.None;

    public void TriggerVictory(string reason = "")
    {
        if (HasEnded) return;
        EndGame(GameResult.Victory, reason);
    }

    public void TriggerDefeat(string reason = "")
    {
        if (HasEnded) return;
        EndGame(GameResult.Defeat, reason);
    }

    private void EndGame(GameResult r, string reason)
    {
        result = r;

        if (gameStateManager != null)
            gameStateManager.SetPaused(true);
        else
            Time.timeScale = 0f;

        if (endScreenUI != null)
            endScreenUI.Show(r, reason);

        OnGameEnded?.Invoke(r, reason);
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
    }

    public void QuitToDesktop()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
