using UnityEngine;

[CreateAssetMenu(menuName = "FGCP/Farming/Crop Config", fileName = "CropConfig")]
public class CropConfigSO : ScriptableObject
{
    [Header("Identity")]
    public string cropId = "spring_crop_demo";
    public string displayName = "Spring Crop (Demo)";

    [Header("Planting")]
    public ResourceType seedResource = ResourceType.Seeds;
    [Min(0)] public int seedCost = 1;

    [Header("Growth Rules")]
    [Min(0)] public int daysToMature = 2;
    public bool requiresDailyWater = true;

    [Header("Costs (per day)")]
    [Min(0)] public int waterCostPerDay = 0;

    [Header("Stage Settings")]
    [Min(1)] public int growthStageCount = 2;

    [Header("Harvest Reward")]
    public ResourceType harvestResource = ResourceType.Food;
    [Min(1)] public int harvestMinAmount = 1;
    [Min(1)] public int harvestMaxAmount = 1;

    [Header("Economy")]
    [Min(0)] public int seedEconomyValue = 1;
    [Min(0)] public int cropEconomyValue = 3;
    public string balanceNote;

    [Header("SFX")]
    public SfxId plantSfxId = SfxId.Farming_Plant;
    public SfxId waterSfxId = SfxId.Farming_Water;
    public SfxId harvestSfxId = SfxId.Farming_Harvest;

    public int GetResolvedSeedCost(int fallback)
    {
        if (seedCost > 0) return seedCost;
        return Mathf.Max(0, fallback);
    }

    public int GetResolvedHarvestAmount()
    {
        int min = Mathf.Max(1, harvestMinAmount);
        int max = Mathf.Max(min, harvestMaxAmount);
        return Random.Range(min, max + 1);
    }

    public string GetHarvestAmountLabel()
    {
        int min = Mathf.Max(1, harvestMinAmount);
        int max = Mathf.Max(min, harvestMaxAmount);
        return min == max ? min.ToString() : $"{min}-{max}";
    }

    public int GetGrowthStageIndex(int growthDaysCompleted)
    {
        int stageCount = Mathf.Max(1, growthStageCount);
        int targetDays = Mathf.Max(1, daysToMature);
        int clampedDays = Mathf.Clamp(growthDaysCompleted, 0, targetDays - 1);
        int index = (clampedDays * stageCount) / targetDays;
        return Mathf.Clamp(index, 0, stageCount - 1);
    }
}
