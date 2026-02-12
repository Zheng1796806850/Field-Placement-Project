using UnityEngine;

[CreateAssetMenu(menuName = "FGCP/Farming/Crop Config", fileName = "CropConfig")]
public class CropConfigSO : ScriptableObject
{
    [Header("Identity")]
    public string cropId = "spring_crop_demo";
    public string displayName = "Spring Crop (Demo)";

    [Header("Growth Rules")]
    [Min(0)] public int daysToMature = 2;
    public bool requiresDailyWater = true;

    [Header("Costs (per day)")]
    [Min(0)] public int waterCostPerDay = 0;

    [Header("Harvest Reward")]
    public ResourceType harvestResource = ResourceType.Food;
    [Min(1)] public int harvestAmount = 1;
}
