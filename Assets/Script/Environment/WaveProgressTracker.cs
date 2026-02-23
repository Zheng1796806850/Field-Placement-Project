using System;
using System.Collections.Generic;
using UnityEngine;

public class WaveProgressTracker : MonoBehaviour
{
    [Header("Config")]
    public WaveConfigSO waveConfig;

    [Header("Wave Victory")]
    public bool enableAutoVictoryOnDayStart = true;
    public int winWaveNumber = 1;
    public string victoryReasonFormat = "Survived to Wave {0}!";

    [Header("Wave Runtime")]
    public int currentWave = 0;
    public int enemiesAlive = 0;
    public int enemiesTotalThisWave = 0;
    public bool waveInProgress = false;

    public event Action<int> OnWaveChanged;
    public event Action<int> OnWaveStarted;
    public event Action<int, int> OnEnemyCountChanged;
    public event Action<int> OnWaveCompleted;

    [Header("Debug")]
    public bool enableDebugHotkey = true;
    public KeyCode debugStartNextWaveKey = KeyCode.F3;

    private readonly HashSet<int> _enemyIds = new HashSet<int>(256);

    private void Awake()
    {
        RefreshWinWaveFromConfig();
    }

    private void Start()
    {
        RefreshWinWaveFromConfig();
    }

    private void Update()
    {
        if (enableDebugHotkey && Input.GetKeyDown(debugStartNextWaveKey))
        {
            StartNextWave();
        }
    }

    public void RefreshWinWaveFromConfig()
    {
        if (waveConfig != null)
            winWaveNumber = waveConfig.GetWinWaveId();
        else
            winWaveNumber = Mathf.Max(1, winWaveNumber);
    }

    public void StartNextWave()
    {
        RefreshWinWaveFromConfig();

        currentWave = Mathf.Max(0, currentWave) + 1;
        waveInProgress = true;

        enemiesAlive = 0;
        enemiesTotalThisWave = GetPlannedSpawnCountFromConfig(currentWave);
        _enemyIds.Clear();

        OnWaveChanged?.Invoke(currentWave);
        OnWaveStarted?.Invoke(currentWave);
        OnEnemyCountChanged?.Invoke(enemiesAlive, enemiesTotalThisWave);
    }

    public void HandleDayStarted()
    {
        if (!enableAutoVictoryOnDayStart) return;

        RefreshWinWaveFromConfig();

        if (currentWave >= winWaveNumber)
        {
            string reason = string.IsNullOrWhiteSpace(victoryReasonFormat)
                ? $"Survived to Wave {winWaveNumber}!"
                : string.Format(victoryReasonFormat, winWaveNumber);

            GameFlowManager.Instance?.TriggerVictory(reason);
        }
    }

    public void SetCurrentWave(int waveNumber)
    {
        RefreshWinWaveFromConfig();

        currentWave = Mathf.Max(0, waveNumber);
        OnWaveChanged?.Invoke(currentWave);
    }

    public void SetExpectedEnemiesForWave(int waveId, int total)
    {
        if (waveId != currentWave) return;
        enemiesTotalThisWave = Mathf.Max(0, total);
        OnEnemyCountChanged?.Invoke(enemiesAlive, enemiesTotalThisWave);
    }

    public void RegisterEnemy(GameObject enemy, int waveId)
    {
        if (enemy == null) return;
        if (!waveInProgress) return;
        if (waveId != currentWave) return;

        int id = enemy.GetInstanceID();
        if (_enemyIds.Add(id))
        {
            enemiesAlive = Mathf.Max(0, enemiesAlive + 1);
            OnEnemyCountChanged?.Invoke(enemiesAlive, enemiesTotalThisWave);
        }
    }

    public void UnregisterEnemy(GameObject enemy, int waveId)
    {
        if (enemy == null) return;
        if (waveId != currentWave) return;

        int id = enemy.GetInstanceID();
        if (_enemyIds.Remove(id))
        {
            enemiesAlive = Mathf.Max(0, enemiesAlive - 1);
            OnEnemyCountChanged?.Invoke(enemiesAlive, enemiesTotalThisWave);

            if (waveInProgress && enemiesAlive <= 0 && enemiesTotalThisWave > 0)
            {
                waveInProgress = false;
                OnWaveCompleted?.Invoke(currentWave);
            }
        }
    }

    private int GetPlannedSpawnCountFromConfig(int waveId)
    {
        if (waveConfig != null && waveConfig.TryGetWave(waveId, out var def) && def != null)
            return Mathf.Max(0, def.spawnCount);
        return 0;
    }
}