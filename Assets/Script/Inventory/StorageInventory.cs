using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class StorageInventory : MonoBehaviour
{
    [Serializable]
    public class DefaultSlotEntry
    {
        [Min(0)] public int slotIndex;
        public ResourceType type;
        [Min(1)] public int amount = 1;
    }

    [Serializable]
    private class StorageSlotDto
    {
        public ResourceType type;
        public int amount;
    }

    [Serializable]
    private class StorageSaveData
    {
        public StorageSlotDto[] slots;
    }

    [Header("Storage Rules")]
    [Min(1)] public int slotCount = 12;
    public BackpackRulesSO rules;
    public bool autoSaveOnSlotChange = true;

    [Header("Defaults (used when no save exists)")]
    public List<DefaultSlotEntry> defaultSlots = new List<DefaultSlotEntry>();

    [SerializeField] private List<InventorySlot> _slots = new List<InventorySlot>();
    [SerializeField, HideInInspector] private string _boundStorageId = "";

    public event Action OnSlotsChanged;

    public int SlotCount => Mathf.Max(1, slotCount);
    public string BoundStorageId => _boundStorageId;
    public IReadOnlyList<InventorySlot> Slots => _slots;

    public void BindAndLoad(string storageId, BackpackRulesSO fallbackRules = null)
    {
        _boundStorageId = storageId ?? string.Empty;
        if (rules == null && fallbackRules != null)
            rules = fallbackRules;

        EnsureSlotCount();

        if (!TryLoadFromSave())
            ApplyDefaults();

        OnSlotsChanged?.Invoke();
    }

    public void EnsureSlotCount()
    {
        int n = SlotCount;
        while (_slots.Count < n) _slots.Add(InventorySlot.Empty);
        while (_slots.Count > n) _slots.RemoveAt(_slots.Count - 1);
    }

    public InventorySlot GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Count)
            return InventorySlot.Empty;
        return _slots[index];
    }

    public bool SetSlot(int index, InventorySlot slot, bool save = true)
    {
        if (index < 0 || index >= _slots.Count)
            return false;

        slot = SanitizeSlot(slot);
        if (_slots[index].type == slot.type && _slots[index].amount == slot.amount)
            return false;

        _slots[index] = slot;
        HandleSlotMutation(save);
        return true;
    }

    public bool SetSlot(int index, ResourceType type, int amount, bool save = true)
    {
        return SetSlot(index, amount <= 0 ? InventorySlot.Empty : new InventorySlot { type = type, amount = amount }, save);
    }

    public bool ClearSlot(int index, bool save = true)
    {
        return SetSlot(index, InventorySlot.Empty, save);
    }

    public bool TryApplyInternalSlotDrag(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || toIndex < 0 || fromIndex >= _slots.Count || toIndex >= _slots.Count)
            return false;
        if (fromIndex == toIndex)
            return false;

        InventorySlot source = _slots[fromIndex];
        InventorySlot target = _slots[toIndex];
        bool changed = InventorySlotTransfer.TryTransfer(ref source, ref target, GetStackSize);
        if (!changed)
            return false;

        _slots[fromIndex] = source;
        _slots[toIndex] = target;
        HandleSlotMutation(true);
        return true;
    }

    public int GetStackSize(ResourceType type)
    {
        return rules != null ? rules.GetStackSize(type) : 20;
    }

    public bool SaveToMemory()
    {
        string key = GetScopedSaveKey();
        if (string.IsNullOrWhiteSpace(key))
            return false;

        EnsureSlotCount();
        var save = new StorageSaveData { slots = new StorageSlotDto[_slots.Count] };
        for (int i = 0; i < _slots.Count; i++)
        {
            InventorySlot s = _slots[i];
            save.slots[i] = new StorageSlotDto
            {
                type = s.IsEmpty ? default : s.type,
                amount = s.IsEmpty ? 0 : s.amount
            };
        }

        PlayerPrefs.SetString(key, JsonUtility.ToJson(save));
        PlayerPrefs.Save();
        return true;
    }

    public bool TryLoadFromSave()
    {
        string key = GetScopedSaveKey();
        if (string.IsNullOrWhiteSpace(key) || !PlayerPrefs.HasKey(key))
            return false;

        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var save = JsonUtility.FromJson<StorageSaveData>(json);
            if (save == null || save.slots == null)
                return false;

            EnsureSlotCount();
            for (int i = 0; i < _slots.Count; i++)
                _slots[i] = InventorySlot.Empty;

            int n = Mathf.Min(_slots.Count, save.slots.Length);
            for (int i = 0; i < n; i++)
            {
                StorageSlotDto dto = save.slots[i];
                if (dto == null || dto.amount <= 0)
                {
                    _slots[i] = InventorySlot.Empty;
                    continue;
                }

                _slots[i] = SanitizeSlot(new InventorySlot { type = dto.type, amount = dto.amount });
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ApplyDefaults()
    {
        EnsureSlotCount();
        for (int i = 0; i < _slots.Count; i++)
            _slots[i] = InventorySlot.Empty;

        if (defaultSlots == null)
            return;

        for (int i = 0; i < defaultSlots.Count; i++)
        {
            DefaultSlotEntry e = defaultSlots[i];
            if (e == null) continue;
            if (e.amount <= 0) continue;
            if (e.slotIndex < 0 || e.slotIndex >= _slots.Count) continue;
            _slots[e.slotIndex] = SanitizeSlot(new InventorySlot { type = e.type, amount = e.amount });
        }
    }

    private InventorySlot SanitizeSlot(InventorySlot slot)
    {
        if (slot.IsEmpty || slot.amount <= 0)
            return InventorySlot.Empty;

        int stack = Mathf.Max(1, GetStackSize(slot.type));
        int amount = Mathf.Clamp(slot.amount, 1, stack);
        return new InventorySlot { type = slot.type, amount = amount };
    }

    private string GetScopedSaveKey()
    {
        if (string.IsNullOrWhiteSpace(_boundStorageId))
            return string.Empty;

        return BaseWorldSession.ScopePlayerPrefsKey("town_storage_" + _boundStorageId);
    }

    private void HandleSlotMutation(bool save)
    {
        if (save && autoSaveOnSlotChange)
            SaveToMemory();
        OnSlotsChanged?.Invoke();
    }
}

