using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StoragePanelHUD : MonoBehaviour
{
    [Header("Refs")]
    public BackpackPanelHUD backpackPanel;
    public BackpackRulesSO rules;

    [Header("Panel")]
    public GameObject panelRoot;
    public Transform gridRoot;
    public StorageSlotUI slotPrefab;
    public TextMeshProUGUI titleLabel;
    public string defaultTitle = "Storage";

    [Header("Behavior")]
    public bool closeOnEscape = true;
    public KeyCode closeKey = KeyCode.Escape;
    public bool closeWhenGamePaused = true;

    private readonly List<StorageSlotUI> _slots = new List<StorageSlotUI>();
    private StorageInventory _boundInventory;
    private TownStoragePoint _boundPoint;

    private bool _backpackWasOpenBeforeStorage;
    private bool _storageOpenedBackpack;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;
    public StorageInventory BoundInventory => _boundInventory;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (backpackPanel == null)
            backpackPanel = FindFirstObjectByType<BackpackPanelHUD>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (backpackPanel != null && backpackPanel.enableToggleKey && Input.GetKeyDown(backpackPanel.toggleKey))
        {
            // While storage is open, Tab should close storage + backpack together.
            backpackPanel.SetPanelVisible(false);
            Close();
            return;
        }

        if (closeWhenGamePaused)
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null && gsm.IsPaused)
            {
                Close();
                return;
            }
        }

        if (closeOnEscape && closeKey != KeyCode.None && Input.GetKeyDown(closeKey))
            Close();
    }

    public void OpenFor(TownStoragePoint point, StorageInventory inventory)
    {
        if (inventory == null || panelRoot == null)
            return;

        if (_boundInventory != null)
            _boundInventory.OnSlotsChanged -= Refresh;

        _boundPoint = point;
        _boundInventory = inventory;
        if (rules == null && _boundInventory.rules != null)
            rules = _boundInventory.rules;

        _boundInventory.OnSlotsChanged += Refresh;
        panelRoot.SetActive(true);

        CacheAndOpenBackpack();
        Refresh();
    }

    public void Close()
    {
        if (_boundInventory != null)
            _boundInventory.OnSlotsChanged -= Refresh;

        _boundInventory = null;
        _boundPoint = null;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        RestoreBackpackState();
    }

    public void Refresh()
    {
        if (!IsOpen || _boundInventory == null)
            return;

        int slotCount = _boundInventory.SlotCount;
        EnsureSlotObjects(slotCount);

        string title = _boundPoint != null && !string.IsNullOrWhiteSpace(_boundPoint.storageDisplayName)
            ? _boundPoint.storageDisplayName
            : defaultTitle;
        if (titleLabel != null)
            titleLabel.text = title;

        for (int i = 0; i < slotCount; i++)
        {
            StorageSlotUI ui = _slots[i];
            ui.Configure(this, i);

            InventorySlot s = _boundInventory.GetSlot(i);
            if (s.IsEmpty)
            {
                ui.SetEmpty();
                continue;
            }

            int stackSize = _boundInventory.GetStackSize(s.type);
            Sprite icon = rules != null ? rules.GetIcon(s.type) : null;
            string displayName = rules != null ? rules.GetDisplayName(s.type) : s.type.ToString();
            ui.Set(s.type, s.amount, stackSize, icon, displayName);
        }
    }

    public bool HandleStorageSlotDrop(int fromStorageSlotIndex, int toStorageSlotIndex)
    {
        if (_boundInventory == null)
            return false;

        bool changed = _boundInventory.TryApplyInternalSlotDrag(fromStorageSlotIndex, toStorageSlotIndex);
        if (changed)
            Refresh();
        return changed;
    }

    public bool HandleDropFromBackpack(BackpackPanelHUD sourceBackpackPanel, int fromBackpackSlotIndex, int toStorageSlotIndex)
    {
        if (_boundInventory == null || sourceBackpackPanel == null || sourceBackpackPanel.Inventory == null)
            return false;
        if (sourceBackpackPanel != backpackPanel)
            return false;

        PlayerResourceInventory playerInv = sourceBackpackPanel.Inventory;
        if (fromBackpackSlotIndex < 0 || fromBackpackSlotIndex >= playerInv.MaxSlots)
            return false;
        if (toStorageSlotIndex < 0 || toStorageSlotIndex >= _boundInventory.SlotCount)
            return false;

        InventorySlot source = playerInv.GetSlot(fromBackpackSlotIndex);
        InventorySlot target = _boundInventory.GetSlot(toStorageSlotIndex);

        bool changed = InventorySlotTransfer.TryTransfer(ref source, ref target, _boundInventory.GetStackSize);
        if (!changed)
            return false;

        playerInv.SetSlot(fromBackpackSlotIndex, source.type, source.amount);
        _boundInventory.SetSlot(toStorageSlotIndex, target);
        sourceBackpackPanel.Refresh();
        Refresh();
        return true;
    }

    private void EnsureSlotObjects(int targetCount)
    {
        if (gridRoot == null || slotPrefab == null)
            return;

        while (_slots.Count < targetCount)
        {
            StorageSlotUI slot = Instantiate(slotPrefab, gridRoot);
            slot.Configure(this, _slots.Count);
            _slots.Add(slot);
        }

        for (int i = 0; i < _slots.Count; i++)
            _slots[i].gameObject.SetActive(i < targetCount);
    }

    private void CacheAndOpenBackpack()
    {
        if (backpackPanel == null)
            return;

        _backpackWasOpenBeforeStorage = backpackPanel.IsOpen;
        _storageOpenedBackpack = !_backpackWasOpenBeforeStorage;

        if (_storageOpenedBackpack)
            backpackPanel.SetPanelVisible(true);
    }

    private void RestoreBackpackState()
    {
        if (backpackPanel == null)
            return;

        if (_storageOpenedBackpack && !_backpackWasOpenBeforeStorage)
            backpackPanel.SetPanelVisible(false);

        _storageOpenedBackpack = false;
    }
}

