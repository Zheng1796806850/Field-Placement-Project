using System;
using UnityEngine;

public class WaveProgressTracker : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("用于决定胜利波次（以及可选的 wave 数据一致性）。")]
    public WaveConfigSO waveConfig;

    [Header("Runtime")]
    [Tooltip("当前夜晚的波次。白天管理阶段也保持这个数值（代表上一晚/即将到来晚上的波次进度）。")]
    public int currentWave = 0;

    [Tooltip("从 WaveConfigSO 推导出的胜利波次（只读概念）。")]
    public int winWaveNumber = 1;

    public event Action<int> OnWaveChanged;

    public event Action<int> OnWaveStarted;

    [Header("Debug")]
    public bool enableDebugHotkey = true;

    [Tooltip("手动触发 StartNextWave（等同“强制进入下一晚并刷下一波”的逻辑触发）。")]
    public KeyCode debugStartNextWaveKey = KeyCode.F3;

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
        OnWaveChanged?.Invoke(currentWave);
        OnWaveStarted?.Invoke(currentWave);
    }

    public void HandleDayStarted()
    {
        RefreshWinWaveFromConfig();

        if (currentWave >= winWaveNumber)
        {
            GameFlowManager.Instance?.TriggerVictory($"Survived to Wave {winWaveNumber}!");
        }
    }

    public void SetCurrentWave(int waveNumber)
    {
        RefreshWinWaveFromConfig();

        currentWave = Mathf.Max(0, waveNumber);
        OnWaveChanged?.Invoke(currentWave);
    }
}
