using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class QuickSlotDropTargetUI : MonoBehaviour, IDropHandler
{
    [Header("Binding")]
    [Min(0)] public int quickSlotIndex = 0;
    public PlayerHungerThirst quickSlotController;
    public BackpackPanelHUD backpackPanel;
    public bool saveInventoryAfterBind = true;

    public static event Action<int, ResourceType> OnAnyQuickSlotBound;
    public event Action<int, ResourceType> OnBound;

    private void Awake()
    {
        if (quickSlotController == null)
            quickSlotController = FindFirstObjectByType<PlayerHungerThirst>(FindObjectsInactive.Include);

        if (backpackPanel == null)
            backpackPanel = FindFirstObjectByType<BackpackPanelHUD>(FindObjectsInactive.Include);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!BackpackSlotUI.DragPayload.active) return;
        if (quickSlotController == null) return;
        if (backpackPanel == null) return;

        if (!backpackPanel.TryGetQuickSlotBindData(BackpackSlotUI.DragPayload.sourceSlotIndex, out var type, out var backpackSlotIndex))
            return;

        BackpackSlotUI.DragPayload.DropConsumedByValidTarget = true;
        quickSlotController.BindQuickSlotResource(quickSlotIndex, type, backpackSlotIndex);

        if (saveInventoryAfterBind)
            PlayerResourceInventory.Instance?.SaveInMemory();

        OnBound?.Invoke(quickSlotIndex, type);
        OnAnyQuickSlotBound?.Invoke(quickSlotIndex, type);
    }
}
