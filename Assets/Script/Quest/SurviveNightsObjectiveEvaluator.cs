public sealed class SurviveNightsObjectiveEvaluator : IObjectiveEvaluator
{
    public void Evaluate(
        QuestManager manager,
        QuestDefinition questDef,
        QuestRuntimeState questState,
        int objectiveIndex,
        ObjectiveDefinition objectiveDef,
        GameplayEvent ev)
    {
        if (ev.Kind != GameplayEventKind.NightSurvived) return;

        manager.AddObjectiveProgress(questState, objectiveIndex, 1, objectiveDef.requiredAmount);
    }
}
