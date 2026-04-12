public sealed class CollectObjectiveEvaluator : IObjectiveEvaluator
{
    public void Evaluate(
        QuestManager manager,
        QuestDefinition questDef,
        QuestRuntimeState questState,
        int objectiveIndex,
        ObjectiveDefinition objectiveDef,
        GameplayEvent ev)
    {
        if (ev.Kind != GameplayEventKind.ResourceCollected) return;
        if (objectiveDef.resourceType != ev.ResourceType) return;

        manager.AddObjectiveProgress(questState, objectiveIndex, ev.IntValue, objectiveDef.requiredAmount);
    }
}
