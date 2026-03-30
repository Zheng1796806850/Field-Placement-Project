using UnityEngine;

public class BackpackReorderObjective : TutorialObjective
{
    public BackpackPanelHUD backpackPanel;
    [Min(1)] public int requiredReorderCount = 1;

    private int _count;

    protected override void OnBegin()
    {
        if (backpackPanel == null)
            backpackPanel = FindFirstObjectByType<BackpackPanelHUD>(FindObjectsInactive.Include);

        _count = 0;

        if (backpackPanel != null)
        {
            backpackPanel.OnDisplayOrderReordered -= HandleReordered;
            backpackPanel.OnDisplayOrderReordered += HandleReordered;
        }
    }

    protected override void OnEnd()
    {
        if (backpackPanel != null)
            backpackPanel.OnDisplayOrderReordered -= HandleReordered;
    }

    private void HandleReordered(int from, int to)
    {
        _count++;
        if (_count >= requiredReorderCount)
            Complete();
    }

    public override string GetProgressText()
    {
        return $"Reorder backpack items {_count}/{requiredReorderCount}";
    }
}
