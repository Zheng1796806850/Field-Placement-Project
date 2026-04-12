using System;
using System.Collections.Generic;

[Serializable]
public class QuestRuntimeState
{
    public string questId;
    public bool active;
    public bool completed;
    public bool failed;
    public string failReason;

    public bool parallelObjectives;
    public int serialObjectiveIndex;

    public List<ObjectiveRuntimeState> objectives = new List<ObjectiveRuntimeState>();

    public static QuestRuntimeState CreateNew(QuestDefinition def)
    {
        var s = new QuestRuntimeState
        {
            questId = def.questId,
            active = true,
            completed = false,
            failed = false,
            parallelObjectives = def.parallelObjectives,
            serialObjectiveIndex = 0,
            objectives = new List<ObjectiveRuntimeState>()
        };

        if (def.objectives != null)
        {
            for (int i = 0; i < def.objectives.Count; i++)
            {
                var od = def.objectives[i];
                if (od == null) continue;
                string oid = string.IsNullOrEmpty(od.objectiveId) ? $"obj_{i}" : od.objectiveId;
                s.objectives.Add(new ObjectiveRuntimeState(oid) { currentProgress = 0, completed = false });
            }
        }

        return s;
    }
}
