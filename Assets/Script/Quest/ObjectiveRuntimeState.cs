using System;

[Serializable]
public class ObjectiveRuntimeState
{
    public string objectiveId;
    public int currentProgress;
    public bool completed;

    public ObjectiveRuntimeState(string id)
    {
        objectiveId = id;
    }
}
