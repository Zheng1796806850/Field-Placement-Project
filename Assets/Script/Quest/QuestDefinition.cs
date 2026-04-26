using System.Collections.Generic;

/// <summary>Runtime quest template (from <see cref="QuestDefinitionSO"/> or legacy adapter).</summary>
public class QuestDefinition
{
    public string questId;
    public string displayTitle;
    public string victoryReason;
    public bool parallelObjectives;
    public List<ObjectiveDefinition> objectives = new List<ObjectiveDefinition>();

    public bool triggerGameFlowVictoryOnComplete = true;
}
