using UnityEngine;

public class WaveSpawnController2D : MonoBehaviour
{
    [Header("Wave Data")]
    public WaveConfigSO waveConfig;
    public WaveProgressTracker waveProgress;

    [Header("Spawning")]
    public GameObject enemyPrefab;
    public Transform[] spawnPoints;

    [Header("Fallback (if waveId not found)")]
    [Min(0)] public int fallbackSpawnCount = 5;
    [Min(0f)] public float fallbackHpMultiplier = 1f;
    [Min(0f)] public float fallbackSpeedMultiplier = 1f;
    [Min(0f)] public float fallbackWallDamageMultiplier = 1f;

    [Header("Enemy Tracking")]
    public bool autoAddWaveEnemyAgent = true;

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
        float wallDmgMul = fallbackWallDamageMultiplier;

        if (waveConfig != null && waveConfig.TryGetWave(waveId, out var def) && def != null)
        {
            spawnCount = def.spawnCount;
            hpMul = def.hpMultiplier;
            speedMul = def.speedMultiplier;
            wallDmgMul = def.wallDamageMultiplier;
        }

        spawnCount = Mathf.Max(0, spawnCount);

        if (waveProgress != null)
            waveProgress.SetExpectedEnemiesForWave(waveId, spawnCount);

        if (logSpawn)
            Debug.Log($"[WaveSpawn] Wave {waveId}: spawn={spawnCount}, hpMul={hpMul}, speedMul={speedMul}, wallDmgMul={wallDmgMul}");

        for (int i = 0; i < spawnCount; i++)
        {
            Transform p = spawnPoints[i % spawnPoints.Length];
            var go = Instantiate(enemyPrefab, p.position, p.rotation);
            ApplyMultipliers(go, hpMul, speedMul, wallDmgMul);

            if (autoAddWaveEnemyAgent && waveProgress != null)
            {
                var agent = go.GetComponent<WaveEnemyAgent>();
                if (agent == null) agent = go.AddComponent<WaveEnemyAgent>();
                agent.Initialize(waveProgress, waveId);
            }
        }
    }

    private void ApplyMultipliers(GameObject enemy, float hpMul, float speedMul, float wallDmgMul)
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
            ai.ApplyWallDamageMultiplier(wallDmgMul);
        }
    }
}
