using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class QuickSlotDropTarget : MonoBehaviour, IDropHandler
{
    public static event Action<int, ResourceType> OnAnyQuickSlotBound;

    [Header("Refs")]
    public PlayerHungerThirst controller;

    [Header("Slot")]
    public int slotIndex;

    private void Awake()
    {
        ResolveController();
    }

    private void OnEnable()
    {
        ResolveController();
    }

    private void ResolveController()
    {
        if (controller == null)
            controller = FindFirstObjectByType<PlayerHungerThirst>(FindObjectsInactive.Include);
    }

    public void OnDrop(PointerEventData eventData)
    {
        ResolveController();
        if (controller == null) return;
        if (!BackpackSlotUI.DragPayload.active) return;

        BackpackSlotUI.DragPayload.DropConsumedByValidTarget = true;
        ResourceType type = BackpackSlotUI.DragPayload.resourceType;
        controller.BindQuickSlotResource(slotIndex, type, BackpackSlotUI.DragPayload.sourceSlotIndex);
        OnAnyQuickSlotBound?.Invoke(slotIndex, type);
    }
}
