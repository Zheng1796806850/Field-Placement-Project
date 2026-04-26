using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FGCP/Quest Definition", fileName = "QuestDefinition")]
public class QuestDefinitionSO : ScriptableObject
{
    [Tooltip("Unique id for PlayerPrefs / resume.")]
    public string questId = "quest_default";

    public string displayTitle = "Quest";

    [TextArea] public string victoryReason = "Quest complete!";

    [Tooltip("若为 false，任务完成不会弹出胜利结算（线性剧情用）。")]
    public bool triggerGameFlowVictoryOnComplete = true;

    [Tooltip("If true, all objectives track together; quest completes when all are done. If false, objectives unlock in list order (serial).")]
    public bool parallelObjectives;

    public List<ObjectiveDefinition> objectives = new List<ObjectiveDefinition>();

    public QuestDefinition ToRuntimeCopy()
    {
        var q = new QuestDefinition
        {
            questId = questId,
            displayTitle = displayTitle,
            victoryReason = victoryReason,
            parallelObjectives = parallelObjectives,
            triggerGameFlowVictoryOnComplete = triggerGameFlowVictoryOnComplete,
            objectives = new List<ObjectiveDefinition>()
        };

        if (objectives != null)
        {
            for (int i = 0; i < objectives.Count; i++)
            {
                if (objectives[i] == null) continue;
                q.objectives.Add(CloneObjective(objectives[i]));
            }
        }

        return q;
    }

    private static ObjectiveDefinition CloneObjective(ObjectiveDefinition src)
    {
        return new ObjectiveDefinition
        {
            objectiveId = src.objectiveId,
            type = src.type,
            targetId = src.targetId,
            resourceType = src.resourceType,
            requiredAmount = Mathf.Max(1, src.requiredAmount),
            displayText = src.displayText,
            optional = src.optional,
            filterCropId = src.filterCropId
        };
    }
}
