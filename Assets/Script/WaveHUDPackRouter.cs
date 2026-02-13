using UnityEngine;

public class WaveHUDPackRouter : MonoBehaviour
{
    [Header("Refs")]
    public GameStateManager gameState;
    public WaveProgressTracker waveProgress;
    public WaveEventBannerHUD banner;

    [Header("Wall Breach")]
    [Min(0f)] public float wallBreachCooldown = 1.0f;

    private float _lastWallBreachTime = -999f;

    private void Awake()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (waveProgress == null) waveProgress = FindFirstObjectByType<WaveProgressTracker>();
        if (banner == null) banner = FindFirstObjectByType<WaveEventBannerHUD>();
    }

    private void OnEnable()
    {
        if (waveProgress == null) waveProgress = FindFirstObjectByType<WaveProgressTracker>();

        if (waveProgress != null)
        {
            waveProgress.OnWaveStarted += HandleWaveStarted;
            waveProgress.OnWaveCompleted += HandleWaveCompleted;
        }

        WallDeathHandler.OnAnyWallDestroyed += HandleAnyWallDestroyed;
    }

    private void OnDisable()
    {
        if (waveProgress != null)
        {
            waveProgress.OnWaveStarted -= HandleWaveStarted;
            waveProgress.OnWaveCompleted -= HandleWaveCompleted;
        }

        WallDeathHandler.OnAnyWallDestroyed -= HandleAnyWallDestroyed;
    }

    private void HandleWaveStarted(int waveId)
    {
        if (banner != null) banner.Show($"Wave {waveId} Started");
    }

    private void HandleWaveCompleted(int waveId)
    {
        if (banner != null) banner.Show($"Wave {waveId} Complete");
    }

    private void HandleAnyWallDestroyed(WallDeathHandler wall)
    {
        float now = Time.unscaledTime;
        if (now - _lastWallBreachTime < wallBreachCooldown) return;
        _lastWallBreachTime = now;

        if (banner != null) banner.Show("Wall Breach!");
    }
}
