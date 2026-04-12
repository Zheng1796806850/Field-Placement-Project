public interface IObjectiveEvaluator
{
    void Evaluate(
        QuestManager manager,
        QuestDefinition questDef,
        QuestRuntimeState questState,
        int objectiveIndex,
        ObjectiveDefinition objectiveDef,
        GameplayEvent ev);
}
