using UnityEngine;
using UnityEngine.EventSystems;

public class QuickSlotDropTarget : MonoBehaviour, IDropHandler
{
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
        controller.BindQuickSlotResource(slotIndex, BackpackSlotUI.DragPayload.resourceType, BackpackSlotUI.DragPayload.sourceSlotIndex);
    }
}
