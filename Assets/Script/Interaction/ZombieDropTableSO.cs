using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ECO/Zombie Drop Table", fileName = "ZombieDropTableSO")]
public class ZombieDropTableSO : ScriptableObject
{
    [Serializable]
    public class DropEntry
    {
        public ResourceType type = ResourceType.Planks;

        [Min(1)] public int minAmount = 1;
        [Min(1)] public int maxAmount = 1;

        [Range(0f, 1f)]
        public float chance = 0.5f;

        [Min(0)] public int maxSpawnsPerKill = 1;

        public void Clamp()
        {
            if (minAmount < 1) minAmount = 1;
            if (maxAmount < 1) maxAmount = 1;
            if (maxAmount < minAmount) maxAmount = minAmount;
            chance = Mathf.Clamp01(chance);
            if (maxSpawnsPerKill < 0) maxSpawnsPerKill = 0;
        }
    }

    [Header("Entries")]
    public List<DropEntry> entries = new List<DropEntry>();

    [Header("Rolls")]
    [Tooltip("How many times we roll the whole table per enemy death. More rolls = more drops on average.")]
    [Min(1)] public int rollsPerKill = 1;

    [Header("Global Limits")]
    [Tooltip("0 = no limit.")]
    [Min(0)] public int maxTotalDropsPerKill = 3;

    [Tooltip("If true, when no entry succeeds, one entry will be forced (weighted by chance).")]
    public bool forceAtLeastOneDrop = false;

    public void Validate()
    {
        if (entries == null) entries = new List<DropEntry>();
        foreach (var e in entries)
            e?.Clamp();

        if (rollsPerKill < 1) rollsPerKill = 1;
        if (maxTotalDropsPerKill < 0) maxTotalDropsPerKill = 0;
    }

    public List<(ResourceType type, int amount)> RollDrops(System.Random rng = null)
    {
        Validate();

        List<(ResourceType type, int amount)> results = new List<(ResourceType type, int amount)>();
        if (entries == null || entries.Count == 0) return results;

        int totalCap = maxTotalDropsPerKill;

        Dictionary<DropEntry, int> spawnedPerEntry = new Dictionary<DropEntry, int>(entries.Count);
        foreach (var e in entries)
        {
            if (e == null) continue;
            spawnedPerEntry[e] = 0;
        }

        for (int rollIndex = 0; rollIndex < rollsPerKill; rollIndex++)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;

                if (totalCap > 0 && results.Count >= totalCap) break;

                if (e.maxSpawnsPerKill > 0 && spawnedPerEntry[e] >= e.maxSpawnsPerKill)
                    continue;

                float roll = (rng != null) ? (float)rng.NextDouble() : UnityEngine.Random.value;
                if (roll > e.chance)
                    continue;

                int amt = (rng != null)
                    ? rng.Next(e.minAmount, e.maxAmount + 1)
                    : UnityEngine.Random.Range(e.minAmount, e.maxAmount + 1);

                results.Add((e.type, amt));
                spawnedPerEntry[e]++;

                if (totalCap > 0 && results.Count >= totalCap) break;
            }

            if (totalCap > 0 && results.Count >= totalCap) break;
        }

        if (forceAtLeastOneDrop && results.Count == 0)
        {
            float sum = 0f;
            foreach (var e in entries)
            {
                if (e == null) continue;
                sum += Mathf.Max(0f, e.chance);
            }

            DropEntry chosen = null;
            if (sum <= 0f)
            {
                chosen = entries[0];
            }
            else
            {
                float r = (rng != null) ? (float)rng.NextDouble() * sum : UnityEngine.Random.value * sum;
                foreach (var e in entries)
                {
                    if (e == null) continue;
                    float w = Mathf.Max(0f, e.chance);
                    r -= w;
                    if (r <= 0f)
                    {
                        chosen = e;
                        break;
                    }
                }
                if (chosen == null) chosen = entries[entries.Count - 1];
            }

            if (chosen != null)
            {
                int amt = (rng != null)
                    ? rng.Next(chosen.minAmount, chosen.maxAmount + 1)
                    : UnityEngine.Random.Range(chosen.minAmount, chosen.maxAmount + 1);

                results.Add((chosen.type, amt));
            }
        }

        return results;
    }
}
