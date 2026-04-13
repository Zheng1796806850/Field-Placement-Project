using System.Collections.Generic;
using UnityEngine;

public class QuickSlotBindByDragObjective : TutorialObjective
{
    [Min(1)] public int requiredBindCount = 1;
    public List<int> requiredSlotIndices = new List<int>();

    private readonly HashSet<int> _boundSlots = new HashSet<int>();
    private int _bindCount;
    private int _lastEventFrame = -1;
    private int _lastEventSlot = -1;
    private ResourceType _lastEventType;

    protected override void OnBegin()
    {
        _boundSlots.Clear();
        _bindCount = 0;
        _lastEventFrame = -1;
        _lastEventSlot = -1;
        _lastEventType = default;
        QuickSlotDropTargetUI.OnAnyQuickSlotBound -= HandleBound;
        QuickSlotDropTargetUI.OnAnyQuickSlotBound += HandleBound;
        QuickSlotDropTarget.OnAnyQuickSlotBound -= HandleBound;
        QuickSlotDropTarget.OnAnyQuickSlotBound += HandleBound;
    }

    protected override void OnEnd()
    {
        QuickSlotDropTargetUI.OnAnyQuickSlotBound -= HandleBound;
        QuickSlotDropTarget.OnAnyQuickSlotBound -= HandleBound;
    }

    private void HandleBound(int slotIndex, ResourceType type)
    {
        if (Time.frameCount == _lastEventFrame &&
            slotIndex == _lastEventSlot &&
            type == _lastEventType)
            return;

        _lastEventFrame = Time.frameCount;
        _lastEventSlot = slotIndex;
        _lastEventType = type;

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
