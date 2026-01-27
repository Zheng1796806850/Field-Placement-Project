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

    [Header("Runtime")]
    public GameResult result = GameResult.None;

    public event Action<GameResult, string> OnGameEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (gameStateManager == null) gameStateManager = GameStateManager.Instance;
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
