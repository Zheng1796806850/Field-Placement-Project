using System;
using UnityEngine;

public abstract class TutorialObjective : MonoBehaviour
{
    public event Action<TutorialObjective> OnCompleted;

    protected TutorialManager manager;
    protected TutorialStep step;

    public bool IsCompleted { get; private set; }

    public virtual string GetProgressText()
    {
        return IsCompleted ? "Done" : "In Progress";
    }

    public void Begin(TutorialManager m, TutorialStep s)
    {
        manager = m;
        step = s;
        IsCompleted = false;
        enabled = true;
        OnBegin();
    }

    public void End()
    {
        OnEnd();
        enabled = false;
    }

    protected void Complete()
    {
        if (IsCompleted) return;
        IsCompleted = true;
        OnObjectiveCompleted();
        OnCompleted?.Invoke(this);
    }

    protected virtual void OnBegin() { }
    protected virtual void OnEnd() { }
    protected virtual void OnObjectiveCompleted() { }
}
