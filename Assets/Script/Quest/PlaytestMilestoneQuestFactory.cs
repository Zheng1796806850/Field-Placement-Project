using UnityEngine;

/// <summary>Builds a runtime <see cref="QuestDefinition"/> from legacy <see cref="PlaytestMilestoneSO"/> (no per-milestone code in <see cref="QuestManager"/>).</summary>
public static class PlaytestMilestoneQuestFactory
{
    public static QuestDefinition FromMilestone(PlaytestMilestoneSO m)
    {
        var q = new QuestDefinition
        {
            questId = "legacy_playtest_milestone",
            displayTitle = m != null && !string.IsNullOrWhiteSpace(m.objectiveTitle) ? m.objectiveTitle : "Objective",
            victoryReason = m != null && !string.IsNullOrWhiteSpace(m.victoryReason) ? m.victoryReason : "Milestone achieved!",
            parallelObjectives = false,
            objectives = new System.Collections.Generic.List<ObjectiveDefinition>()
        };

        if (m == null)
            return q;

        switch (m.type)
        {
            case PlaytestMilestoneSO.MilestoneType.SurviveNights:
                q.objectives.Add(new ObjectiveDefinition
                {
                    objectiveId = "survive_nights",
                    type = ObjectiveType.SurviveNights,
                    requiredAmount = Mathf.Max(1, m.requiredNights),
                    displayText = $"Survive {m.requiredNights} night(s)"
                });
                break;

            case PlaytestMilestoneSO.MilestoneType.GatherFoodAndPlanks:
                q.parallelObjectives = true;
                if (m.requiredFood > 0)
                {
                    q.objectives.Add(new ObjectiveDefinition
                    {
                        objectiveId = "gather_food",
                        type = ObjectiveType.Collect,
                        resourceType = ResourceType.Food,
                        requiredAmount = m.requiredFood,
                        displayText = $"Gather Food {m.requiredFood}"
                    });
                }

                if (m.requiredPlanks > 0)
                {
                    q.objectives.Add(new ObjectiveDefinition
                    {
                        objectiveId = "gather_planks",
                        type = ObjectiveType.Collect,
                        resourceType = ResourceType.Planks,
                        requiredAmount = m.requiredPlanks,
                        displayText = $"Gather Planks {m.requiredPlanks}"
                    });
                }

                break;

            case PlaytestMilestoneSO.MilestoneType.BuildWaterCollectorAndSurvive:
                q.parallelObjectives = false;
                q.objectives.Add(new ObjectiveDefinition
                {
                    objectiveId = "build_wells",
                    type = ObjectiveType.Build,
                    targetId = WaterCollectorQuestIds.StructureId,
                    requiredAmount = Mathf.Max(1, m.requiredBuiltWells),
                    displayText = $"Build water collector(s) {m.requiredBuiltWells}"
                });
                q.objectives.Add(new ObjectiveDefinition
                {
                    objectiveId = "survive_after_build",
                    type = ObjectiveType.SurviveNights,
                    requiredAmount = Mathf.Max(1, m.requiredNightsAfterBuild),
                    displayText = $"Survive {m.requiredNightsAfterBuild} night(s) after building"
                });
                break;
        }

        return q;
    }
}

/// <summary>Shared id for <see cref="WaterCollectorBuildSpot"/> quest events.</summary>
public static class WaterCollectorQuestIds
{
    public const string StructureId = "water_collector";
}
