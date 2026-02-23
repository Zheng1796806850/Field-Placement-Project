using UnityEngine;

[CreateAssetMenu(menuName = "FGCP/Playtest Milestone", fileName = "PlaytestMilestoneSO")]
public class PlaytestMilestoneSO : ScriptableObject
{
    public enum MilestoneType
    {
        SurviveNights = 0,
        GatherFoodAndPlanks = 1,
        BuildWaterCollectorAndSurvive = 2
    }

    [Header("Definition")]
    public MilestoneType type = MilestoneType.BuildWaterCollectorAndSurvive;

    [Header("UI Text")]
    public string objectiveTitle = "Objective";
    public string victoryReason = "Milestone achieved!";

    [Header("Survive Nights")]
    [Min(1)] public int requiredNights = 2;

    [Header("Gather Resources")]
    [Min(0)] public int requiredFood = 15;
    [Min(0)] public int requiredPlanks = 15;

    [Header("Build + Survive")]
    [Min(1)] public int requiredBuiltWells = 1;
    [Min(1)] public int requiredNightsAfterBuild = 1;
}