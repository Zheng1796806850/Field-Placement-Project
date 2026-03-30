using System.Collections.Generic;
using UnityEngine;

public class QuickSlotBindByDragObjective : TutorialObjective
{
    [Min(1)] public int requiredBindCount = 1;
    public List<int> requiredSlotIndices = new List<int>();

    private readonly HashSet<int> _boundSlots = new HashSet<int>();
    private int _bindCount;

    protected override void OnBegin()
    {
        _boundSlots.Clear();
        _bindCount = 0;
        QuickSlotDropTargetUI.OnAnyQuickSlotBound -= HandleBound;
        QuickSlotDropTargetUI.OnAnyQuickSlotBound += HandleBound;
    }

    protected override void OnEnd()
    {
        QuickSlotDropTargetUI.OnAnyQuickSlotBound -= HandleBound;
    }

    private void HandleBound(int slotIndex, ResourceType type)
    {
        _bindCount++;
        _boundSlots.Add(slotIndex);

        if (requiredSlotIndices != null && requiredSlotIndices.Count > 0)
        {
            for (int i = 0; i < requiredSlotIndices.Count; i++)
            {
                if (!_boundSlots.Contains(requiredSlotIndices[i]))
                    return;
            }

            Complete();
            return;
        }

        if (_bindCount >= requiredBindCount)
            Complete();
    }

    public override string GetProgressText()
    {
        if (requiredSlotIndices != null && requiredSlotIndices.Count > 0)
            return $"Bind drag item to slots ({_boundSlots.Count}/{requiredSlotIndices.Count})";

        return $"Bind drag item to quick slot {_bindCount}/{requiredBindCount}";
    }
}
