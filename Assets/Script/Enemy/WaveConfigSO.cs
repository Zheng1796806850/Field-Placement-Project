using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Waves/Wave Config", fileName = "WaveConfig")]
public class WaveConfigSO : ScriptableObject
{
    [Serializable]
    public class WaveDefinition
    {
        [Min(1)] public int waveId = 1;
        [Min(0)] public int spawnCount = 5;

        [Min(0f)] public float hpMultiplier = 1f;
        [Min(0f)] public float speedMultiplier = 1f;

        [Min(0f)] public float wallDamageMultiplier = 1f;
    }

    [Header("Wave Table")]
    public List<WaveDefinition> waves = new List<WaveDefinition>();

    [Header("Win Condition (Optional)")]
    [Tooltip("=0 表示自动使用 waves 中最大的 waveId 作为胜利波次。")]
    [Min(0)] public int winWaveIdOverride = 0;

    private Dictionary<int, WaveDefinition> _dict;

    public bool TryGetWave(int waveId, out WaveDefinition def)
    {
        BuildCacheIfNeeded();
        return _dict.TryGetValue(waveId, out def);
    }

    public int GetMaxWaveId()
    {
        int max = 0;
        for (int i = 0; i < waves.Count; i++)
        {
            if (waves[i] == null) continue;
            if (waves[i].waveId > max) max = waves[i].waveId;
        }
        return max;
    }

    public int GetWinWaveId()
    {
        if (winWaveIdOverride > 0) return winWaveIdOverride;
        return Mathf.Max(1, GetMaxWaveId());
    }

    private void BuildCacheIfNeeded()
    {
        if (_dict != null) return;

        _dict = new Dictionary<int, WaveDefinition>();
        for (int i = 0; i < waves.Count; i++)
        {
            var w = waves[i];
            if (w == null) continue;
            _dict[w.waveId] = w;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _dict = null;
    }
#endif
}
