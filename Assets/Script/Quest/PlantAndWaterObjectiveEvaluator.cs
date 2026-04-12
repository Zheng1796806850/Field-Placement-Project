public sealed class PlantAndWaterObjectiveEvaluator : IObjectiveEvaluator
{
    public void Evaluate(
        QuestManager manager,
        QuestDefinition questDef,
        QuestRuntimeState questState,
        int objectiveIndex,
        ObjectiveDefinition objectiveDef,
        GameplayEvent ev)
    {
        if (ev.Kind != GameplayEventKind.CropPlantedAndWatered) return;

        if (string.IsNullOrEmpty(objectiveDef.targetId) ||
            !string.Equals(objectiveDef.targetId, ev.StringId, System.StringComparison.Ordinal))
            return;

        if (!string.IsNullOrEmpty(objectiveDef.filterCropId) &&
            !string.Equals(objectiveDef.filterCropId, ev.StringId2, System.StringComparison.Ordinal))
            return;

        manager.AddObjectiveProgress(questState, objectiveIndex, 1, objectiveDef.requiredAmount);
    }
}
