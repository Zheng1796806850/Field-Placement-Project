using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BackpackPanelHUD : MonoBehaviour
{
    [Header("Refs")]
    public PlayerResourceInventory inventory;
    public BackpackRulesSO rules;

    [Header("Auto Wiring")]
    public bool autoWireRulesIntoInventory = true;

    [Header("Panel")]
    public GameObject panelRoot;

    [Header("Grid")]
    public Transform gridRoot;
    public BackpackSlotUI slotPrefab;

    [Header("Summary (Optional)")]
    public TextMeshProUGUI capacityLabel;
    public TextMeshProUGUI overflowLabel;

    [Header("Display")]
    public bool showEmptySlots = true;

    [Header("Input")]
    public bool enableToggleKey = true;
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Refresh")]
    public bool refreshWhileVisible = true;
    [Min(0.02f)] public float refreshInterval = 0.1f;

    [Header("Behavior")]
    public bool openOnStart = false;
    public bool saveInventoryAfterReorder = true;

    public event Action<bool> OnPanelVisibilityChanged;
    public event Action<int, int> OnDisplayOrderReordered;

    private readonly List<BackpackSlotUI> _slots = new List<BackpackSlotUI>();
    private float _nextRefreshTime;
    private bool _lastVisibleState;
    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    /// <summary>Resolved inventory for drag/drop helpers (e.g. BackpackSlotUI).</summary>
    public PlayerResourceInventory Inventory => inventory;

    private void Awake()
    {
        ResolveRefs();

        if (panelRoot != null)
            panelRoot.SetActive(openOnStart);

        _lastVisibleState = IsOpen;

        Refresh();
    }

    private void OnEnable()
    {
        ResolveRefs();

        if (inventory != null)
            inventory.OnAnyResourceChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.OnAnyResourceChanged -= Refresh;
    }

    private void Update()
    {
        if (enableToggleKey && Input.GetKeyDown(toggleKey))
            Toggle();

        EmitPanelVisibilityIfChanged();

        if (!refreshWhileVisible) return;
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;

        if (Time.unscaledTime >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.unscaledTime + refreshInterval;
            Refresh();
        }
    }

    private void ResolveRefs()
    {
        if (inventory == null)
            inventory = PlayerResourceInventory.Instance != null ? PlayerResourceInventory.Instance : FindFirstObjectByType<PlayerResourceInventory>(FindObjectsInactive.Include);

        if (rules == null && inventory != null)
            rules = inventory.rules;

        if (inventory != null && autoWireRulesIntoInventory)
        {
            if (inventory.rules == null && rules != null)
                inventory.rules = rules;

            if (rules == null)
                rules = inventory.rules;
        }
    }

    public void Toggle()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(!panelRoot.activeSelf);
        EmitPanelVisibilityIfChanged(force: true);
        if (panelRoot.activeSelf) Refresh();
    }

    public void SetPanelVisible(bool visible)
    {
        if (panelRoot == null) return;
        if (panelRoot.activeSelf == visible) return;
        panelRoot.SetActive(visible);
        EmitPanelVisibilityIfChanged(force: true);
        if (visible) Refresh();
    }

    public void Refresh()
    {
        ResolveRefs();
        if (inventory == null) return;

        int maxSlots = inventory.MaxSlots;
        int used = inventory.GetUsedSlots();

        if (capacityLabel != null) capacityLabel.text = $"Slots {used}/{maxSlots}";

        int overflowTotal = 0;
        foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
            overflowTotal += inventory.GetOverflowBuffer(t);

        if (overflowLabel != null)
            overflowLabel.text = overflowTotal > 0 ? $"Overflow {overflowTotal}" : "";

        int visibleSlots = showEmptySlots ? maxSlots : Mathf.Clamp(used, 0, maxSlots);

        EnsureSlotObjects(maxSlots);

        for (int i = 0; i < maxSlots; i++)
        {
            _slots[i].Configure(this, i);
            var cell = inventory.GetSlot(i);
            if (cell.IsEmpty)
            {
                _slots[i].SetEmpty();
            }
            else
            {
                int stackSize = inventory.GetStackSize(cell.type);
                Sprite icon = rules != null ? rules.GetIcon(cell.type) : null;
                string name = rules != null ? rules.GetDisplayName(cell.type) : cell.type.ToString();
                _slots[i].Set(cell.type, cell.amount, stackSize, icon, name);
            }
        }

        for (int i = 0; i < maxSlots; i++)
            _slots[i].gameObject.SetActive(i < visibleSlots);
    }

    /// <summary>True if the point lies inside the backpack panel rect (screen space).</summary>
    public bool IsScreenPointOverBackpackPanel(Vector2 screenPosition, Camera eventCamera)
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy)
            return false;

        var rt = panelRoot.transform as RectTransform;
        if (rt == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPosition, eventCamera);
    }

    public bool TryGetResourceTypeAtDisplaySlot(int displayIndex, out ResourceType type)
    {
        ResolveRefs();
        if (inventory == null)
        {
            type = default;
            return false;
        }

        return inventory.BindQuickSlotCandidateFromDisplayIndex(displayIndex, out type);
    }

    public bool TryGetQuickSlotBindData(int displayIndex, out ResourceType type, out int backpackSlotIndex)
    {
        ResolveRefs();
        type = default;
        backpackSlotIndex = -1;
        if (inventory == null)
            return false;

        return inventory.TryGetBackpackSlotForQuickBind(displayIndex, out type, out backpackSlotIndex);
    }

    public void HandleSlotDrop(int fromSlotIndex, int toSlotIndex)
    {
        ResolveRefs();
        if (inventory == null) return;
        if (fromSlotIndex < 0 || toSlotIndex < 0) return;
        if (fromSlotIndex == toSlotIndex) return;

        bool changed = inventory.TryApplyBackpackSlotDrag(fromSlotIndex, toSlotIndex);
        if (!changed) return;

        if (saveInventoryAfterReorder)
            inventory.SaveInMemory();

        OnDisplayOrderReordered?.Invoke(fromSlotIndex, toSlotIndex);

        Refresh();
    }

    private void EmitPanelVisibilityIfChanged(bool force = false)
    {
        bool now = IsOpen;
        if (!force && now == _lastVisibleState) return;
        _lastVisibleState = now;
        OnPanelVisibilityChanged?.Invoke(now);
    }

    private void EnsureSlotObjects(int targetCount)
    {
        if (gridRoot == null || slotPrefab == null) return;

        while (_slots.Count < targetCount)
        {
            var slot = Instantiate(slotPrefab, gridRoot);
            slot.Configure(this, _slots.Count);
            _slots.Add(slot);
        }
    }
}
