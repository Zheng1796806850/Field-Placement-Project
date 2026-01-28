using UnityEngine;

public class WaveSpawnController2D : MonoBehaviour
{
    [Header("Wave Data")]
    public WaveConfigSO waveConfig;

    [Tooltip("由 GameFlowManager 自动注入/或手动拖拽。")]
    public WaveProgressTracker waveProgress;

    [Header("Spawning")]
    public GameObject enemyPrefab;

    [Tooltip("敌人出生点列表（Transform 位置）。")]
    public Transform[] spawnPoints;

    [Header("Fallback (if waveId not found)")]
    [Min(0)] public int fallbackSpawnCount = 5;
    [Min(0f)] public float fallbackHpMultiplier = 1f;
    [Min(0f)] public float fallbackSpeedMultiplier = 1f;

    [Header("Debug")]
    public bool logSpawn = true;

    private bool _subscribed;

    private void Awake()
    {
        if (waveProgress == null)
            waveProgress = FindFirstObjectByType<WaveProgressTracker>();

        if (waveConfig == null && waveProgress != null)
            waveConfig = waveProgress.waveConfig;
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        if (waveProgress == null)
            waveProgress = FindFirstObjectByType<WaveProgressTracker>();

        if (waveConfig == null && waveProgress != null)
            waveConfig = waveProgress.waveConfig;

        TrySubscribe();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;
        if (waveProgress == null) return;

        waveProgress.OnWaveStarted += HandleWaveStarted;
        _subscribed = true;

        if (logSpawn) Debug.Log($"[WaveSpawn] {name}: Subscribed to WaveProgressTracker.OnWaveStarted");
    }

    private void TryUnsubscribe()
    {
        if (!_subscribed) return;

        if (waveProgress != null)
            waveProgress.OnWaveStarted -= HandleWaveStarted;

        _subscribed = false;
    }

    private void HandleWaveStarted(int waveId)
    {
        SpawnWave(waveId);
    }

    [ContextMenu("Spawn Current Wave Now")]
    public void SpawnCurrentWaveNow()
    {
        int waveId = (waveProgress != null) ? Mathf.Max(1, waveProgress.currentWave) : 1;
        SpawnWave(waveId);
    }

    private void SpawnWave(int waveId)
    {
        if (enemyPrefab == null)
        {
            Debug.LogError($"{name}: enemyPrefab is not assigned.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError($"{name}: spawnPoints is empty.");
            return;
        }

        int spawnCount = fallbackSpawnCount;
        float hpMul = fallbackHpMultiplier;
        float speedMul = fallbackSpeedMultiplier;

        if (waveConfig != null && waveConfig.TryGetWave(waveId, out var def) && def != null)
        {
            spawnCount = def.spawnCount;
            hpMul = def.hpMultiplier;
            speedMul = def.speedMultiplier;
        }

        if (logSpawn)
            Debug.Log($"[WaveSpawn] Wave {waveId}: spawn={spawnCount}, hpMul={hpMul}, speedMul={speedMul}");

        for (int i = 0; i < spawnCount; i++)
        {
            Transform p = spawnPoints[i % spawnPoints.Length];
            var go = Instantiate(enemyPrefab, p.position, p.rotation);
            ApplyMultipliers(go, hpMul, speedMul);
        }
    }

    private void ApplyMultipliers(GameObject enemy, float hpMul, float speedMul)
    {
        if (enemy == null) return;

        var hp = enemy.GetComponentInChildren<Health>();
        if (hp != null)
        {
            hp.ApplyMaxHPMultiplier(hpMul, fillToMax: true);
        }

        var ai = enemy.GetComponentInChildren<EnemyAI2D>();
        if (ai != null)
        {
            ai.ApplySpeedMultiplier(speedMul);
        }
    }
}
