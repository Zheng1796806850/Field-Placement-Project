using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WaveSpawnController2D : MonoBehaviour
{
    [Header("Refs")]
    public WaveConfigSO waveConfig;
    public WaveProgressTracker waveProgress;
    public GameStateManager gameStateManager;

    [Header("Base Night Continuous Spawn")]
    [Tooltip("When enabled, this component runs continuous spawning during night in allowed scenes.")]
    public bool enableNightContinuousSpawn = true;
    [Tooltip("Only scenes in this list use the Base night continuous spawn flow.")]
    public string[] allowedSceneNames = { "BaseScene" };
    [Min(0.1f)] public float randomSpawnDelayMin = 1f;
    [Min(0.1f)] public float randomSpawnDelayMax = 5f;
    [Min(0f)] public float perDayHpBonus = 0.2f;
    [Min(0f)] public float perDayDamageBonus = 0.2f;

    [Header("Enemy Prefabs")]
    public GameObject normalEnemyPrefab;
    public GameObject runnerEnemyPrefab;
    [Min(0f)] public float normalWeight = 1f;
    [Min(0f)] public float runnerWeight = 1f;
    public Transform[] spawnPoints;

    [Header("Enemy Tracking")]
    public bool autoAddWaveEnemyAgent = true;
    [Tooltip("Generated enemies are tracked with this wave id. For continuous mode this maps to CurrentDay.")]
    public bool trackAsCurrentDayWave = true;

    [Header("Legacy Wave Spawn (Deprecated)")]
    [Tooltip("Deprecated for BaseScene. Keep off to avoid fixed wave-based spawning.")]
    public bool enableLegacyWaveSpawn = false;
    [Min(0)] public int fallbackSpawnCount = 5;
    [Min(0f)] public float fallbackHpMultiplier = 1f;
    [Min(0f)] public float fallbackSpeedMultiplier = 1f;
    [Min(0f)] public float fallbackWallDamageMultiplier = 1f;
    public bool applyWaveSpeedToEnemyMoveSpeed = true;
    public GameObject enemyPrefab;

    [Header("Debug")]
    public bool logSpawn = false;

    bool _subscribedLegacyWave;
    float _nextRetryTime;
    Coroutine _nightSpawnRoutine;
    int _activeNightWaveId;

    public bool ShouldRunContinuousInCurrentScene => enableNightContinuousSpawn && IsSceneAllowedForContinuous();

    void Awake()
    {
        ResolveRefs();
    }

    void OnEnable()
    {
        ResolveRefs();
        TrySubscribeLegacyWave();
    }

    void Start()
    {
        ResolveRefs();
        TrySubscribeLegacyWave();
    }

    void Update()
    {
        if (_subscribedLegacyWave || !enableLegacyWaveSpawn) return;
        if (Time.unscaledTime < _nextRetryTime) return;
        _nextRetryTime = Time.unscaledTime + 0.5f;
        ResolveRefs();
        TrySubscribeLegacyWave();
    }

    void OnDisable()
    {
        StopNightContinuousSpawn();
        TryUnsubscribeLegacyWave();
    }

    void ResolveRefs()
    {
        if (waveProgress == null)
            waveProgress = FindFirstObjectByType<WaveProgressTracker>();
        if (waveConfig == null && waveProgress != null)
            waveConfig = waveProgress.waveConfig;
        if (gameStateManager == null)
            gameStateManager = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
    }

    bool IsSceneAllowedForContinuous()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;
        if (allowedSceneNames == null || allowedSceneNames.Length == 0)
            return false;
        for (int i = 0; i < allowedSceneNames.Length; i++)
        {
            if (string.Equals(sceneName, allowedSceneNames[i], System.StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public void HandleNightStarted()
    {
        if (!ShouldRunContinuousInCurrentScene) return;
        StartNightContinuousSpawn();
    }

    public void HandleDayStarted()
    {
        if (!ShouldRunContinuousInCurrentScene) return;
        StopNightContinuousSpawn();
        KillRemainingNightEnemiesThroughHealth();
        if (waveProgress != null)
            waveProgress.EndNightSession();
    }

    void StartNightContinuousSpawn()
    {
        ResolveRefs();
        if (_nightSpawnRoutine != null)
            return;
        if (!HasValidContinuousSetup())
            return;

        _activeNightWaveId = trackAsCurrentDayWave
            ? Mathf.Max(1, gameStateManager != null ? gameStateManager.CurrentDay : 1)
            : Mathf.Max(1, waveProgress != null ? waveProgress.currentWave : 1);

        if (waveProgress != null)
        {
            waveProgress.useFixedWaveCompletion = false;
            waveProgress.StartNightSession(_activeNightWaveId);
        }

        _nightSpawnRoutine = StartCoroutine(NightSpawnLoop());
    }

    void StopNightContinuousSpawn()
    {
        if (_nightSpawnRoutine == null)
            return;
        StopCoroutine(_nightSpawnRoutine);
        _nightSpawnRoutine = null;
    }

    IEnumerator NightSpawnLoop()
    {
        while (gameStateManager != null && gameStateManager.CurrentPhase == DayNightPhase.Night)
        {
            SpawnSingleContinuousEnemy();

            float minDelay = Mathf.Max(0.05f, randomSpawnDelayMin);
            float maxDelay = Mathf.Max(minDelay, randomSpawnDelayMax);
            float delay = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(delay);
        }

        _nightSpawnRoutine = null;
    }

    void SpawnSingleContinuousEnemy()
    {
        var prefab = PickEnemyPrefab();
        if (prefab == null)
        {
            if (logSpawn) Debug.LogWarning($"[WaveSpawn] {name}: no enemy prefab available for continuous spawn.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError($"{name}: spawnPoints is empty.");
            return;
        }

        var point = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (point == null)
        {
            Debug.LogWarning($"[WaveSpawn] {name}: one spawn point is null.");
            return;
        }

        var go = Instantiate(prefab, point.position, point.rotation);
        ApplyNightScaling(go);

        if (waveProgress != null)
            waveProgress.NotifyEnemySpawned(_activeNightWaveId);

        if (autoAddWaveEnemyAgent && waveProgress != null)
        {
            var agent = go.GetComponent<WaveEnemyAgent>();
            if (agent == null)
                agent = go.AddComponent<WaveEnemyAgent>();
            agent.Initialize(waveProgress, _activeNightWaveId);
        }
    }

    void ApplyNightScaling(GameObject enemy)
    {
        if (enemy == null) return;

        int day = Mathf.Max(1, gameStateManager != null ? gameStateManager.CurrentDay : 1);
        float hpMul = 1f + perDayHpBonus * (day - 1);
        float dmgMul = 1f + perDayDamageBonus * (day - 1);

        var hp = enemy.GetComponentInChildren<Health>();
        if (hp != null)
            hp.ApplyMaxHPMultiplier(hpMul, fillToMax: true);

        var ai = enemy.GetComponentInChildren<EnemyAI2D>();
        if (ai != null)
            ai.ApplyAttackMultiplier(dmgMul);
    }

    GameObject PickEnemyPrefab()
    {
        bool hasNormal = normalEnemyPrefab != null;
        bool hasRunner = runnerEnemyPrefab != null;
        if (!hasNormal && !hasRunner)
            return enemyPrefab;
        if (hasNormal && !hasRunner)
            return normalEnemyPrefab;
        if (!hasNormal && hasRunner)
            return runnerEnemyPrefab;

        float n = Mathf.Max(0f, normalWeight);
        float r = Mathf.Max(0f, runnerWeight);
        float total = n + r;
        if (total <= 0.0001f)
            return Random.value < 0.5f ? normalEnemyPrefab : runnerEnemyPrefab;

        float roll = Random.Range(0f, total);
        return roll < n ? normalEnemyPrefab : runnerEnemyPrefab;
    }

    void KillRemainingNightEnemiesThroughHealth()
    {
        var enemies = FindObjectsByType<EnemyAI2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            var ai = enemies[i];
            if (ai == null) continue;
            var hp = ai.GetComponent<Health>();
            if (hp == null || hp.dead || hp.currentHP <= 0) continue;
            hp.TakeDamage(hp.currentHP);
        }
    }

    bool HasValidContinuousSetup()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError($"{name}: spawnPoints is empty.");
            return false;
        }

        if (normalEnemyPrefab == null && runnerEnemyPrefab == null && enemyPrefab == null)
        {
            Debug.LogError($"{name}: no enemy prefab assigned (normal/runner/legacy).");
            return false;
        }

        return true;
    }

    void TrySubscribeLegacyWave()
    {
        if (!enableLegacyWaveSpawn) return;
        if (_subscribedLegacyWave) return;
        if (waveProgress == null) return;
        waveProgress.OnWaveStarted += HandleWaveStartedLegacy;
        _subscribedLegacyWave = true;
    }

    void TryUnsubscribeLegacyWave()
    {
        if (!_subscribedLegacyWave) return;
        if (waveProgress != null)
            waveProgress.OnWaveStarted -= HandleWaveStartedLegacy;
        _subscribedLegacyWave = false;
    }

    void HandleWaveStartedLegacy(int waveId)
    {
        if (!enableLegacyWaveSpawn) return;
        if (ShouldRunContinuousInCurrentScene) return;
        SpawnWaveLegacy(waveId);
    }

    void SpawnWaveLegacy(int waveId)
    {
        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            return;

        int spawnCount = Mathf.Max(0, fallbackSpawnCount);
        float hpMul = fallbackHpMultiplier;
        float speedMul = fallbackSpeedMultiplier;
        float wallDmgMul = fallbackWallDamageMultiplier;

        if (waveConfig != null && waveConfig.TryGetWave(waveId, out var def) && def != null)
        {
            spawnCount = Mathf.Max(0, def.spawnCount);
            hpMul = def.hpMultiplier;
            speedMul = def.speedMultiplier;
            wallDmgMul = def.wallDamageMultiplier;
        }

        if (waveProgress != null)
            waveProgress.SetExpectedEnemiesForWave(waveId, spawnCount);

        for (int i = 0; i < spawnCount; i++)
        {
            Transform p = spawnPoints[i % spawnPoints.Length];
            if (p == null) continue;
            var go = Instantiate(enemyPrefab, p.position, p.rotation);
            ApplyLegacyMultipliers(go, hpMul, speedMul, wallDmgMul);

            if (autoAddWaveEnemyAgent && waveProgress != null)
            {
                var agent = go.GetComponent<WaveEnemyAgent>();
                if (agent == null) agent = go.AddComponent<WaveEnemyAgent>();
                agent.Initialize(waveProgress, waveId);
            }
        }
    }

    void ApplyLegacyMultipliers(GameObject enemy, float hpMul, float speedMul, float wallDmgMul)
    {
        if (enemy == null) return;

        var hp = enemy.GetComponentInChildren<Health>();
        if (hp != null)
            hp.ApplyMaxHPMultiplier(hpMul, fillToMax: true);

        var ai = enemy.GetComponentInChildren<EnemyAI2D>();
        if (ai != null)
        {
            if (applyWaveSpeedToEnemyMoveSpeed)
                ai.ApplySpeedMultiplier(speedMul);
            ai.ApplyWallDamageMultiplier(wallDmgMul);
        }
    }
}
