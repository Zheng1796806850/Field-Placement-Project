using System;
using UnityEngine;

public class WaveProgressTracker : MonoBehaviour
{
    [Header("Win Condition")]
    [Min(1)] public int winWaveNumber = 3;

    [Header("Runtime")]
    public int currentWave = 0;

    public event Action<int> OnWaveChanged;

    [Header("Debug")]
    public bool enableDebugHotkey = true;
    public KeyCode debugAdvanceWaveKey = KeyCode.F3;
    public KeyCode debugKillPlayer = KeyCode.F4;

    public bool debugPlayerCanKillByF4 = false;

    public Health playerHealth;

    private void Update()
    {
        if (enableDebugHotkey && Input.GetKeyDown(debugAdvanceWaveKey))
        {
            NotifyWaveCleared();
        }

        if (enableDebugHotkey && Input.GetKeyDown(debugKillPlayer) && debugPlayerCanKillByF4)
        {
            playerHealth.TakeDamage(10);
        }
    }

    public void NotifyWaveCleared()
    {
        currentWave++;
        OnWaveChanged?.Invoke(currentWave);

        if (currentWave >= winWaveNumber)
        {
            GameFlowManager.Instance?.TriggerVictory($"Survived to Wave {winWaveNumber}!");
        }
    }

    public void SetCurrentWave(int waveNumber)
    {
        currentWave = Mathf.Max(0, waveNumber);
        OnWaveChanged?.Invoke(currentWave);

        if (currentWave >= winWaveNumber)
        {
            GameFlowManager.Instance?.TriggerVictory($"Survived to Wave {winWaveNumber}!");
        }
    }
}
