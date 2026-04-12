public sealed class KillObjectiveEvaluator : IObjectiveEvaluator
{
    public void Evaluate(
        QuestManager manager,
        QuestDefinition questDef,
        QuestRuntimeState questState,
        int objectiveIndex,
        ObjectiveDefinition objectiveDef,
        GameplayEvent ev)
    {
        if (ev.Kind != GameplayEventKind.EnemyKilled) return;

        if (!string.IsNullOrEmpty(objectiveDef.targetId) &&
            !string.Equals(objectiveDef.targetId, ev.StringId, System.StringComparison.OrdinalIgnoreCase))
            return;

        manager.AddObjectiveProgress(questState, objectiveIndex, 1, objectiveDef.requiredAmount);
    }
}
