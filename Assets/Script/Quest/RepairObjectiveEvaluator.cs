public sealed class RepairObjectiveEvaluator : IObjectiveEvaluator
{
    public void Evaluate(
        QuestManager manager,
        QuestDefinition questDef,
        QuestRuntimeState questState,
        int objectiveIndex,
        ObjectiveDefinition objectiveDef,
        GameplayEvent ev)
    {
        if (ev.Kind != GameplayEventKind.StructureRepaired) return;

        if (!string.IsNullOrEmpty(objectiveDef.targetId) &&
            !string.Equals(objectiveDef.targetId, ev.StringId, System.StringComparison.Ordinal))
            return;

        manager.AddObjectiveProgress(questState, objectiveIndex, ev.IntValue, objectiveDef.requiredAmount);
    }
}
