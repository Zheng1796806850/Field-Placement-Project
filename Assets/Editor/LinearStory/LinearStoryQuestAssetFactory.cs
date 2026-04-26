#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>线性剧情 QuestDefinitionSO：菜单创建 + 向导 Ensure（存在则只校正关键字段）。</summary>
public static class LinearStoryQuestAssetFactory
{
    public const string TargetDir = "Assets/_Game/LinearStory";

    [MenuItem("Tools/Linear Story/Create Default QuestDefinition Assets")]
    public static void CreateAllMenu()
    {
        var w = new List<string>();
        EnsureQuestAssets(w, refreshQuestContent: true);
        foreach (var line in w)
            Debug.Log(line);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LinearStoryQuestAssetFactory] Quest assets ensured under {TargetDir}");
    }

    /// <param name="warnings">接收提示（例如 Kill 数量需人工确认）。</param>
    /// <param name="refreshQuestContent">为 true 时用模板覆盖目标与目标列表（慎用）。</param>
    public static void EnsureQuestAssets(List<string> warnings, bool refreshQuestContent = false)
    {
        if (warnings == null) warnings = new List<string>();
        if (!Directory.Exists(TargetDir))
            Directory.CreateDirectory(TargetDir);

        EnsureOne(warnings, "Quest_Linear_BuildWell.asset", refreshQuestContent, ApplyWell);
        EnsureOne(warnings, "Quest_Linear_PlantCrop.asset", refreshQuestContent, ApplyPlant);
        EnsureOne(warnings, "Quest_Linear_WaterOnce.asset", refreshQuestContent, ApplyWater);
        EnsureOne(warnings, "Quest_Linear_FirstNight.asset", refreshQuestContent, ApplyNight);
        EnsureOne(warnings, "Quest_Linear_VisitBackyard.asset", refreshQuestContent, ApplyBackyard);
        EnsureOne(warnings, "Quest_Linear_TownSupplies.asset", refreshQuestContent, ApplyTown);

        warnings.Add("[LinearStory] Quest_Linear_FirstNight: default Kill count is 10 — verify against your first wave in the asset.");
    }

    static void EnsureOne(List<string> warnings, string fileName, bool refresh, System.Action<QuestDefinitionSO> applyTemplate)
    {
        string path = Path.Combine(TargetDir, fileName).Replace('\\', '/');
        var existing = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(path);
        if (existing == null)
        {
            var q = ScriptableObject.CreateInstance<QuestDefinitionSO>();
            applyTemplate(q);
            AssetDatabase.CreateAsset(q, path);
            return;
        }

        existing.triggerGameFlowVictoryOnComplete = false;
        if (refresh)
            applyTemplate(existing);

        EditorUtility.SetDirty(existing);
    }

    static void ApplyWell(QuestDefinitionSO q)
    {
        q.questId = "linear_story_build_well";
        q.displayTitle = "Build a well";
        q.victoryReason = "Well completed";
        q.triggerGameFlowVictoryOnComplete = false;
        q.parallelObjectives = false;
        q.objectives = new List<ObjectiveDefinition>
        {
            new ObjectiveDefinition
            {
                objectiveId = "build_well",
                type = ObjectiveType.Build,
                targetId = WaterCollectorQuestIds.StructureId,
                requiredAmount = 1,
                displayText = "Build the well"
            }
        };
    }

    static void ApplyPlant(QuestDefinitionSO q)
    {
        q.questId = "linear_story_plant_crop";
        q.displayTitle = "Till a field and plant a crop";
        q.victoryReason = "Seeds planted";
        q.triggerGameFlowVictoryOnComplete = false;
        q.parallelObjectives = false;
        q.objectives = new List<ObjectiveDefinition>
        {
            new ObjectiveDefinition
            {
                objectiveId = "plant_any",
                type = ObjectiveType.CropPlanted,
                targetId = "",
                requiredAmount = 1,
                displayText = "Plant seeds on a field"
            }
        };
    }

    static void ApplyWater(QuestDefinitionSO q)
    {
        q.questId = "linear_story_water_plot";
        q.displayTitle = "Water the crops once";
        q.victoryReason = "Crops watered";
        q.triggerGameFlowVictoryOnComplete = false;
        q.parallelObjectives = false;
        q.objectives = new List<ObjectiveDefinition>
        {
            new ObjectiveDefinition
            {
                objectiveId = "water_any",
                type = ObjectiveType.PlotWatered,
                targetId = "",
                requiredAmount = 1,
                displayText = "Water your crops"
            }
        };
    }

    static void ApplyNight(QuestDefinitionSO q)
    {
        q.questId = "linear_story_first_night";
        q.displayTitle = "Clear the horde and survive the night";
        q.victoryReason = "You survived the first night";
        q.triggerGameFlowVictoryOnComplete = false;
        q.parallelObjectives = true;
        q.objectives = new List<ObjectiveDefinition>
        {
            new ObjectiveDefinition
            {
                objectiveId = "kill_wave",
                type = ObjectiveType.Kill,
                targetId = "",
                requiredAmount = 10,
                displayText = "Defeat the attacking enemies"
            },
            new ObjectiveDefinition
            {
                objectiveId = "survive_night",
                type = ObjectiveType.SurviveNights,
                targetId = "",
                requiredAmount = 1,
                displayText = "Survive until morning"
            }
        };
    }

    static void ApplyBackyard(QuestDefinitionSO q)
    {
        q.questId = "linear_story_visit_backyard";
        q.displayTitle = "Check the backyard";
        q.victoryReason = "Reached the backyard";
        q.triggerGameFlowVictoryOnComplete = false;
        q.parallelObjectives = false;
        q.objectives = new List<ObjectiveDefinition>
        {
            new ObjectiveDefinition
            {
                objectiveId = "reach_backyard",
                type = ObjectiveType.ReachArea,
                targetId = "story_backyard",
                requiredAmount = 1,
                displayText = "Go to the backyard"
            }
        };
    }

    static void ApplyTown(QuestDefinitionSO q)
    {
        q.questId = "linear_story_town_supplies";
        q.displayTitle = "Explore the town — get medicine and a flashlight";
        q.victoryReason = "Supplies obtained";
        q.triggerGameFlowVictoryOnComplete = false;
        q.parallelObjectives = true;
        q.objectives = new List<ObjectiveDefinition>
        {
            new ObjectiveDefinition
            {
                objectiveId = "collect_medicine",
                type = ObjectiveType.Collect,
                targetId = "",
                resourceType = ResourceType.AntiInflammatory,
                requiredAmount = 1,
                displayText = "Obtain anti-inflammatory medicine"
            },
            new ObjectiveDefinition
            {
                objectiveId = "collect_light",
                type = ObjectiveType.Collect,
                targetId = "",
                resourceType = ResourceType.Flashlight,
                requiredAmount = 1,
                displayText = "Obtain a flashlight"
            }
        };
    }

    public static QuestDefinitionSO LoadQuest(string fileName)
    {
        string path = Path.Combine(TargetDir, fileName).Replace('\\', '/');
        return AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(path);
    }
}
#endif
