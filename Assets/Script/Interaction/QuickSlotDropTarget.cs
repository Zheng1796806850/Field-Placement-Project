using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

public class QuickSlotDropTarget : MonoBehaviour, IDropHandler
{
    public static event Action<int, ResourceType> OnAnyQuickSlotBound;
    private static readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);

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
        TryBindFromPayload();
    }

    public static bool TryConsumeDragAtPointer(PointerEventData eventData)
    {
        if (eventData == null || EventSystem.current == null || !BackpackSlotUI.DragPayload.active)
            return false;

        var pointer = new PointerEventData(EventSystem.current) { position = eventData.position };
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(pointer, _raycastResults);

        for (int i = 0; i < _raycastResults.Count; i++)
        {
            var go = _raycastResults[i].gameObject;
            if (go == null)
                continue;

            var target = go.GetComponentInParent<QuickSlotDropTarget>();
            if (target == null)
                continue;

            if (target.TryBindFromPayload())
                return true;
        }

        return false;
    }

    private bool TryBindFromPayload()
    {
        ResolveController();
        if (controller == null) return false;
        if (!BackpackSlotUI.DragPayload.active) return false;
        if (slotIndex < 0 || slotIndex >= controller.QuickSlotCount) return false;

        BackpackSlotUI.DragPayload.DropConsumedByValidTarget = true;
        ResourceType type = BackpackSlotUI.DragPayload.resourceType;
        controller.BindQuickSlotResource(slotIndex, type, BackpackSlotUI.DragPayload.sourceSlotIndex);
        OnAnyQuickSlotBound?.Invoke(slotIndex, type);
        return true;
    }
}
