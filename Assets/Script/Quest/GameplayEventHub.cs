using System;
using UnityEngine;

/// <summary>Central gameplay signals for quest objectives. Emit from game systems; subscribe only from <see cref="QuestManager"/>.</summary>
public static class GameplayEventHub
{
    public static event Action<ResourceType, int> OnResourceCollected;
    public static event Action<string, int> OnStructureBuilt;
    public static event Action<string, int> OnStructureRepaired;
    public static event Action<string, int> OnEnemyKilled;
    public static event Action<string> OnPlayerEnteredArea;
    public static event Action OnNightSurvived;
    /// <summary>Args: plotQuestId (matches quest objective targetId), cropId from <see cref="CropConfigSO.cropId"/>.</summary>
    public static event Action<string, string> OnCropPlantedAndWatered;

    public static void RaiseResourceCollected(ResourceType type, int delta)
    {
        if (delta <= 0) return;
        OnResourceCollected?.Invoke(type, delta);
    }

    public static void RaiseStructureBuilt(string structureId, int instanceId)
    {
        OnStructureBuilt?.Invoke(structureId ?? "", instanceId);
    }

    public static void RaiseStructureRepaired(string targetId, int repairAmount)
    {
        if (repairAmount <= 0) return;
        OnStructureRepaired?.Invoke(targetId ?? "", repairAmount);
    }

    public static void RaiseEnemyKilled(string enemyTag, int instanceId)
    {
        OnEnemyKilled?.Invoke(enemyTag ?? "", instanceId);
    }

    public static void RaisePlayerEnteredArea(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) return;
        OnPlayerEnteredArea?.Invoke(areaId);
    }

    public static void RaiseNightSurvived()
    {
        OnNightSurvived?.Invoke();
    }

    public static void RaiseCropPlantedAndWatered(string plotQuestId, string cropId)
    {
        if (string.IsNullOrEmpty(plotQuestId)) return;
        OnCropPlantedAndWatered?.Invoke(plotQuestId, cropId ?? "");
    }
}
