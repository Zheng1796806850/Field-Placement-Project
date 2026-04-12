using System;
using System.Collections.Generic;
using UnityEngine;

public struct BackpackStackView
{
    public ResourceType type;
    public int amountInStack;
    public int stackSize;
}

public class PlayerResourceInventory : MonoBehaviour
{
    public static PlayerResourceInventory Instance { get; private set; }

    [Serializable]
    public class DefaultResourceEntry
    {
        public ResourceType type;
        public int amount;
    }

    [Header("Backpack Rules")]
    public BackpackRulesSO rules;
    public BackpackOverflowMode overflowModeOverride = BackpackOverflowMode.DenyPickup;
    public bool useRulesOverflowMode = true;

    [Header("Overflow Drop")]
    public ResourceDrop2D overflowDropPrefab;
    public float overflowDropScatterRadius = 0.25f;

    [Header("Drop from backpack (drag outside UI)")]
    [Tooltip("If null, overflowDropPrefab is used when dropping a slot to the world.")]
    public ResourceDrop2D dropResourcePrefab;

    [Header("Default Resources (used if no save exists yet)")]
    public List<DefaultResourceEntry> defaultResources = new List<DefaultResourceEntry>();

    [Header("Persistence")]
    public string saveKey = "PLAYER_RESOURCE_INVENTORY_V1";
    public bool autoLoadOnAwake = true;
    public bool dontDestroyOnLoad = true;

    [Header("Editor Debug")]
    public bool clearSaveOnAwakeInEditor = false;

    [Header("Legacy Defaults")]
    [SerializeField, HideInInspector] private int defaultPlanks = 0;
    [SerializeField, HideInInspector] private int defaultSeeds = 0;
    [SerializeField, HideInInspector] private int defaultWater = 0;
    [SerializeField, HideInInspector] private int defaultFood = 0;

    public event Action<ResourceType, int> OnResourceChanged;
    public event Action OnAnyResourceChanged;
    public event Action<string> OnInventoryMessage;

    [SerializeField] private List<InventorySlot> _slots = new List<InventorySlot>();

    private readonly Dictionary<ResourceType, int> _overflowBuffer = new Dictionary<ResourceType, int>();
    private readonly List<BackpackStackView> _stackCache = new List<BackpackStackView>();

    private bool _flushingBuffer;
    private int _quickUseScopedBackpackSlotIndex = -1;

    private Dictionary<ResourceType, int> _questCollectBaseline;

    [Serializable]
    private class SaveDataV4
    {
        public InventorySlotDto[] backpackSlots;
        public List<Entry> overflow = new List<Entry>();
    }

    [Serializable]
    private class InventorySlotDto
    {
        public ResourceType type;
        public int amount;
    }

    [Serializable]
    private class SaveDataV3
    {
        public List<Entry> entries = new List<Entry>();
        public List<Entry> overflow = new List<Entry>();
        public List<OrderEntry> displayOrder = new List<OrderEntry>();
    }

    [Serializable]
    private class SaveDataV2
    {
        public List<Entry> entries = new List<Entry>();
        public List<Entry> overflow = new List<Entry>();
    }

    [Serializable]
    private class SaveDataV1
    {
        public List<Entry> entries = new List<Entry>();
    }

    [Serializable]
    private class Entry
    {
        public ResourceType type;
        public int amount;
    }

    [Serializable]
    private class OrderEntry
    {
        public ResourceType type;
    }

    public int MaxSlots => Mathf.Max(1, rules != null ? rules.maxSlots : 16);

    public IReadOnlyList<InventorySlot> Slots => _slots;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        if (clearSaveOnAwakeInEditor)
            ClearSave();
#endif

        InitDefaultsIfNeeded();

        if (autoLoadOnAwake)
        {
            if (HasSave())
                LoadFromMemory();
            else
                BroadcastAll();
        }
        else
        {
            BroadcastAll();
        }

        ResetQuestCollectBaseline();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SanitizeDefaultResources();
    }
#endif

    private void InitDefaultsIfNeeded()
    {
        EnsureOverflowKeys();
        MigrateLegacyDefaultsToListIfNeeded();
        SanitizeDefaultResources();

        EnsureSlotListSize();
        ClearAllSlots();

        if (defaultResources != null)
        {
            for (int i = 0; i < defaultResources.Count; i++)
            {
                var entry = defaultResources[i];
                if (entry == null) continue;
                if (entry.amount <= 0) continue;

                TryAdd(entry.type, entry.amount, transform.position, out _, out _, false);
            }
        }
    }

    private void MigrateLegacyDefaultsToListIfNeeded()
    {
        if (defaultResources != null && defaultResources.Count > 0)
            return;

        if (defaultResources == null)
            defaultResources = new List<DefaultResourceEntry>();

        AddLegacyDefaultIfPositive(ResourceType.Planks, defaultPlanks);
        AddLegacyDefaultIfPositive(ResourceType.Seeds, defaultSeeds);
        AddLegacyDefaultIfPositive(ResourceType.Water, defaultWater);
        AddLegacyDefaultIfPositive(ResourceType.Food, defaultFood);
    }

    private void AddLegacyDefaultIfPositive(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        defaultResources.Add(new DefaultResourceEntry { type = type, amount = amount });
    }

    private void SanitizeDefaultResources()
    {
        if (defaultResources == null)
        {
            defaultResources = new List<DefaultResourceEntry>();
            return;
        }

        for (int i = defaultResources.Count - 1; i >= 0; i--)
        {
            var entry = defaultResources[i];
            if (entry == null)
            {
                defaultResources.RemoveAt(i);
                continue;
            }

            if (entry.amount < 0)
                entry.amount = 0;
        }
    }

    private void EnsureOverflowKeys()
    {
        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            if (!_overflowBuffer.ContainsKey(t))
                _overflowBuffer[t] = 0;
        }
    }

    private void EnsureSlotListSize()
    {
        int n = MaxSlots;
        while (_slots.Count < n)
            _slots.Add(InventorySlot.Empty);
        while (_slots.Count > n)
            _slots.RemoveAt(_slots.Count - 1);
    }

    private void ClearAllSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i] = InventorySlot.Empty;
    }

    public InventorySlot GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Count)
            return InventorySlot.Empty;
        return _slots[index];
    }

    public void SetSlot(int index, ResourceType type, int amount)
    {
        if (index < 0 || index >= _slots.Count)
            return;

        amount = Mathf.Max(0, amount);
        if (amount <= 0)
        {
            _slots[index] = InventorySlot.Empty;
            RaiseAfterSlotMutation();
            return;
        }

        int cap = GetStackSize(type);
        amount = Mathf.Min(amount, cap);
        int maxCarry = GetMaxCarry(type);
        if (maxCarry >= 0)
        {
            int other = GetTotalOfTypeExcludingSlot(type, index);
            amount = Mathf.Min(amount, Mathf.Max(0, maxCarry - other));
        }

        _slots[index] = new InventorySlot { type = type, amount = amount };
        RaiseAfterSlotMutation();
    }

    public void ClearSlot(int index)
    {
        if (index < 0 || index >= _slots.Count)
            return;
        _slots[index] = InventorySlot.Empty;
        RaiseAfterSlotMutation();
    }

    private int GetTotalOfTypeExcludingSlot(ResourceType type, int excludeIndex)
    {
        int sum = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (i == excludeIndex) continue;
            var s = _slots[i];
            if (!s.IsEmpty && s.type == type)
                sum += s.amount;
        }
        return sum;
    }

    private void RaiseAfterSlotMutation()
    {
        BroadcastAll();
    }

    public void BeginQuickUseBackpackSlotScope(int backpackSlotIndex)
    {
        _quickUseScopedBackpackSlotIndex = backpackSlotIndex;
    }

    public void EndQuickUseBackpackSlotScope()
    {
        _quickUseScopedBackpackSlotIndex = -1;
    }

    public int SumTypeInSlots(ResourceType type)
    {
        int total = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (!s.IsEmpty && s.type == type)
                total += s.amount;
        }
        return total;
    }

    public int Get(ResourceType type)
    {
        if (_quickUseScopedBackpackSlotIndex >= 0 && _quickUseScopedBackpackSlotIndex < _slots.Count)
        {
            var s = _slots[_quickUseScopedBackpackSlotIndex];
            if (!s.IsEmpty && s.type == type)
                return s.amount;
            return 0;
        }

        return SumTypeInSlots(type);
    }

    public int GetOverflowBuffer(ResourceType type)
    {
        if (_overflowBuffer.TryGetValue(type, out int v)) return v;
        return 0;
    }

    public int GetStackSize(ResourceType type) => rules != null ? rules.GetStackSize(type) : 20;
    public int GetMaxCarry(ResourceType type) => rules != null ? rules.GetMaxCarry(type) : -1;

    public BackpackOverflowMode GetOverflowMode()
    {
        if (useRulesOverflowMode && rules != null) return rules.overflowMode;
        return overflowModeOverride;
    }

    public int GetUsedSlots()
    {
        int c = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].IsEmpty)
                c++;
        }
        return c;
    }

    public int GetFreeSlots() => Mathf.Max(0, MaxSlots - GetUsedSlots());

    public int PreviewMaxAddable(ResourceType type, int request) => ComputeMaxAddable(type, request);
    public bool CanAcceptAny(ResourceType type, int request) => PreviewMaxAddable(type, request) > 0;

    /// <summary>Compatibility: one view per occupied backpack slot (index aligns with UI).</summary>
    public IReadOnlyList<BackpackStackView> GetStackViewsSnapshot()
    {
        _stackCache.Clear();
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s.IsEmpty) continue;
            int ss = GetStackSize(s.type);
            _stackCache.Add(new BackpackStackView
            {
                type = s.type,
                amountInStack = s.amount,
                stackSize = ss
            });
        }
        return _stackCache;
    }

    public bool TryGetStackViewAtDisplayIndex(int displayIndex, out BackpackStackView view)
    {
        if (displayIndex >= 0 && displayIndex < _slots.Count)
        {
            var s = _slots[displayIndex];
            if (!s.IsEmpty)
            {
                view = new BackpackStackView
                {
                    type = s.type,
                    amountInStack = s.amount,
                    stackSize = GetStackSize(s.type)
                };
                return true;
            }
        }

        view = default;
        return false;
    }

    public bool TryGetResourceTypeAtDisplayIndex(int displayIndex, out ResourceType type)
    {
        if (TryGetStackViewAtDisplayIndex(displayIndex, out var view))
        {
            type = view.type;
            return true;
        }

        type = default;
        return false;
    }

    public bool TryGetBackpackSlotForQuickBind(int displayIndex, out ResourceType type, out int backpackSlotIndex)
    {
        backpackSlotIndex = displayIndex;
        if (TryGetResourceTypeAtDisplayIndex(displayIndex, out type))
            return true;
        type = default;
        backpackSlotIndex = -1;
        return false;
    }

    /// <summary>
    /// Drag-drop between UI slot indices: merge (same type, fits), else swap (same type over stack, or different types).
    /// </summary>
    public bool ReorderDisplaySlot(int fromDisplayIndex, int toDisplayIndex)
    {
        return TryApplyBackpackSlotDrag(fromDisplayIndex, toDisplayIndex);
    }

    public bool TryApplyBackpackSlotDrag(int fromIndex, int toIndex)
    {
        EnsureSlotListSize();
        if (fromIndex < 0 || toIndex < 0 || fromIndex >= _slots.Count || toIndex >= _slots.Count)
            return false;
        if (fromIndex == toIndex)
            return false;

        var a = _slots[fromIndex];
        var b = _slots[toIndex];
        if (a.IsEmpty)
            return false;

        if (b.IsEmpty)
        {
            _slots[toIndex] = a;
            _slots[fromIndex] = InventorySlot.Empty;
            RaiseLayoutChanged();
            return true;
        }

        if (a.type == b.type)
        {
            int cap = GetStackSize(a.type);
            int sum = a.amount + b.amount;
            if (sum <= cap)
            {
                _slots[toIndex] = new InventorySlot { type = a.type, amount = sum };
                _slots[fromIndex] = InventorySlot.Empty;
                RaiseLayoutChanged();
                return true;
            }

            (_slots[fromIndex], _slots[toIndex]) = (_slots[toIndex], _slots[fromIndex]);
            RaiseLayoutChanged();
            return true;
        }

        (_slots[fromIndex], _slots[toIndex]) = (_slots[toIndex], _slots[fromIndex]);
        RaiseLayoutChanged();
        return true;
    }

    public bool BindQuickSlotCandidateFromDisplayIndex(int displayIndex, out ResourceType type)
    {
        return TryGetResourceTypeAtDisplayIndex(displayIndex, out type);
    }

    public void DropSlotToWorld(int slotIndex, Vector3? worldDropOrigin = null)
    {
        EnsureSlotListSize();
        if (slotIndex < 0 || slotIndex >= _slots.Count)
            return;

        var s = _slots[slotIndex];
        if (s.IsEmpty)
            return;

        var prefab = dropResourcePrefab != null ? dropResourcePrefab : overflowDropPrefab;
        if (prefab == null)
        {
            RaiseMessage("No drop prefab configured");
            return;
        }

        Vector3 origin = worldDropOrigin ?? transform.position;
        Vector2 scatter = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, overflowDropScatterRadius);
        Vector3 pos = origin + new Vector3(scatter.x, scatter.y, 0f);

        var drop = Instantiate(prefab, pos, Quaternion.identity);
        drop.Configure(s.type, s.amount);

        _slots[slotIndex] = InventorySlot.Empty;
        RaiseLayoutChanged();
    }

    public void Set(ResourceType type, int amount)
    {
        amount = Mathf.Max(0, amount);
        int maxCarry = GetMaxCarry(type);
        if (maxCarry >= 0)
            amount = Mathf.Min(amount, maxCarry);

        int current = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (!s.IsEmpty && s.type == type)
                current += s.amount;
        }

        int diff = amount - current;
        if (diff == 0)
        {
            Broadcast(type);
            return;
        }

        if (diff > 0)
        {
            TryAdd(type, diff, transform.position, out _, out _, false);
            return;
        }

        int toRemove = -diff;
        for (int i = 0; i < _slots.Count && toRemove > 0; i++)
        {
            var s = _slots[i];
            if (s.IsEmpty || s.type != type) continue;

            int take = Mathf.Min(s.amount, toRemove);
            int left = s.amount - take;
            toRemove -= take;
            _slots[i] = left <= 0 ? InventorySlot.Empty : new InventorySlot { type = type, amount = left };
        }

        RaiseLayoutChanged();
    }

    public void Add(ResourceType type, int delta)
    {
        if (delta == 0) return;

        if (delta < 0)
        {
            int toRemove = -delta;
            for (int i = 0; i < _slots.Count && toRemove > 0; i++)
            {
                var s = _slots[i];
                if (s.IsEmpty || s.type != type) continue;
                int take = Mathf.Min(s.amount, toRemove);
                int left = s.amount - take;
                toRemove -= take;
                _slots[i] = left <= 0 ? InventorySlot.Empty : new InventorySlot { type = type, amount = left };
            }

            TryFlushOverflowBuffer(false);
            Broadcast(type);
            return;
        }

        TryAdd(type, delta, transform.position, out _, out _, true);
    }

    public bool CanSpend(ResourceType type, int cost)
    {
        if (cost <= 0) return true;
        return Get(type) >= cost;
    }

    public bool Spend(ResourceType type, int cost)
    {
        if (cost <= 0) return true;
        if (!CanSpend(type, cost)) return false;

        if (_quickUseScopedBackpackSlotIndex >= 0 && _quickUseScopedBackpackSlotIndex < _slots.Count)
        {
            var s = _slots[_quickUseScopedBackpackSlotIndex];
            if (s.IsEmpty || s.type != type || s.amount < cost)
                return false;

            int left = s.amount - cost;
            _slots[_quickUseScopedBackpackSlotIndex] = left <= 0
                ? InventorySlot.Empty
                : new InventorySlot { type = type, amount = left };
            RaiseLayoutChanged();
            return true;
        }

        int remaining = cost;
        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            var s = _slots[i];
            if (s.IsEmpty || s.type != type) continue;
            int take = Mathf.Min(s.amount, remaining);
            int left = s.amount - take;
            remaining -= take;
            _slots[i] = left <= 0 ? InventorySlot.Empty : new InventorySlot { type = type, amount = left };
        }

        if (remaining > 0)
            return false;

        RaiseLayoutChanged();
        return true;
    }

    public bool Spend(Dictionary<ResourceType, int> costs)
    {
        if (costs == null || costs.Count == 0) return true;

        foreach (var kv in costs)
        {
            if (!CanSpend(kv.Key, kv.Value))
                return false;
        }

        foreach (var kv in costs)
            Spend(kv.Key, kv.Value);

        return true;
    }

    public bool TryAdd(ResourceType type, int amount, out int accepted, out int rejected) =>
        TryAdd(type, amount, transform.position, out accepted, out rejected, true);

    public bool TryAdd(ResourceType type, int amount, Vector3 worldPos, out int accepted, out int rejected, bool showMessages)
    {
        accepted = 0;
        rejected = 0;

        if (amount <= 0) return true;

        EnsureSlotListSize();

        int maxAccepted = ComputeMaxAddable(type, amount);
        var mode = GetOverflowMode();

        if (mode == BackpackOverflowMode.DenyPickup && maxAccepted < amount)
        {
            accepted = 0;
            rejected = amount;
            if (showMessages) RaiseMessage("Backpack full");
            return false;
        }

        accepted = Mathf.Max(0, maxAccepted);
        rejected = Mathf.Max(0, amount - accepted);

        if (accepted > 0)
            AddToSlotsInternal(type, accepted);

        if (rejected > 0)
            HandleOverflow(type, rejected, worldPos, mode, showMessages);

        TryFlushOverflowBuffer(false);

        if (accepted > 0 || rejected > 0)
            Broadcast(type);

        return rejected == 0;
    }

    private void AddToSlotsInternal(ResourceType type, int add)
    {
        int cap = GetStackSize(type);
        int left = add;

        for (int i = 0; i < _slots.Count && left > 0; i++)
        {
            var s = _slots[i];
            if (s.IsEmpty || s.type != type) continue;
            if (s.amount >= cap) continue;
            int room = cap - s.amount;
            int take = Mathf.Min(room, left);
            _slots[i] = new InventorySlot { type = type, amount = s.amount + take };
            left -= take;
        }

        for (int i = 0; i < _slots.Count && left > 0; i++)
        {
            var s = _slots[i];
            if (!s.IsEmpty) continue;
            int take = Mathf.Min(cap, left);
            _slots[i] = new InventorySlot { type = type, amount = take };
            left -= take;
        }

        if (left > 0)
        {
            // Should not happen if ComputeMaxAddable is correct
            HandleOverflow(type, left, transform.position, GetOverflowMode(), false);
        }
    }

    public void TryFlushOverflowBuffer(bool showMessage)
    {
        if (_flushingBuffer) return;
        _flushingBuffer = true;

        bool movedAny = false;

        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            int buf = GetOverflowBuffer(t);
            if (buf <= 0) continue;

            int can = ComputeMaxAddable(t, buf);
            if (can <= 0) continue;

            _overflowBuffer[t] = buf - can;
            AddToSlotsInternal(t, can);
            movedAny = true;
        }

        if (movedAny)
        {
            if (showMessage) RaiseMessage("Moved items from overflow buffer");
            BroadcastAll();
        }

        _flushingBuffer = false;
    }

    private void HandleOverflow(ResourceType type, int rejected, Vector3 worldPos, BackpackOverflowMode mode, bool showMessages)
    {
        if (rejected <= 0) return;

        if (mode == BackpackOverflowMode.DropOverflow)
        {
            if (overflowDropPrefab != null)
            {
                Vector2 scatter = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, overflowDropScatterRadius);
                Vector3 pos = worldPos + new Vector3(scatter.x, scatter.y, 0f);
                var drop = Instantiate(overflowDropPrefab, pos, Quaternion.identity);
                drop.Configure(type, rejected);
            }
            else
            {
                _overflowBuffer[type] = GetOverflowBuffer(type) + rejected;
            }

            if (showMessages) RaiseMessage("Backpack full: overflow dropped");
            return;
        }

        if (mode == BackpackOverflowMode.TempBuffer)
        {
            _overflowBuffer[type] = GetOverflowBuffer(type) + rejected;
            if (showMessages) RaiseMessage("Backpack full: overflow buffered");
            return;
        }

        if (showMessages) RaiseMessage("Backpack full");
    }

    private int ComputeMaxAddable(ResourceType type, int request)
    {
        if (request <= 0) return 0;

        int s = GetStackSize(type);
        int current = SumTypeInSlots(type);

        int maxCarry = GetMaxCarry(type);
        if (maxCarry >= 0)
        {
            int byCarry = Mathf.Max(0, maxCarry - current);
            request = Mathf.Min(request, byCarry);
            if (request <= 0) return 0;
        }

        int maxTotalItems = rules != null ? rules.maxTotalItems : -1;
        if (maxTotalItems >= 0)
        {
            int total = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsEmpty)
                    total += _slots[i].amount;
            }

            int byTotal = Mathf.Max(0, maxTotalItems - total);
            request = Mathf.Min(request, byTotal);
            if (request <= 0) return 0;
        }

        int room = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot.IsEmpty)
                room += s;
            else if (slot.type == type && slot.amount < s)
                room += s - slot.amount;
        }

        return Mathf.Min(request, room);
    }

    public void ResetQuestCollectBaseline()
    {
        if (_questCollectBaseline == null)
            _questCollectBaseline = new Dictionary<ResourceType, int>();

        _questCollectBaseline.Clear();
        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            _questCollectBaseline[t] = SumTypeInSlots(t);
    }

    private void EmitQuestResourceCollected(ResourceType type)
    {
        if (_questCollectBaseline == null)
        {
            ResetQuestCollectBaseline();
            return;
        }

        int now = SumTypeInSlots(type);
        if (!_questCollectBaseline.TryGetValue(type, out int prev))
            prev = now;

        if (now > prev)
            GameplayEventHub.RaiseResourceCollected(type, now - prev);

        _questCollectBaseline[type] = now;
    }

    private void Broadcast(ResourceType type)
    {
        OnResourceChanged?.Invoke(type, SumTypeInSlots(type));
        OnAnyResourceChanged?.Invoke();
        EmitQuestResourceCollected(type);
    }

    private void BroadcastAll()
    {
        EnsureOverflowKeys();
        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            OnResourceChanged?.Invoke(t, SumTypeInSlots(t));
        OnAnyResourceChanged?.Invoke();
        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            EmitQuestResourceCollected(t);
    }

    private void RaiseLayoutChanged()
    {
        BroadcastAll();
    }

    public void PushMessage(string msg)
    {
        RaiseMessage(msg);
    }

    private void RaiseMessage(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return;
        OnInventoryMessage?.Invoke(msg);
    }

    public void SaveInMemory()
    {
        EnsureSlotListSize();

        var data = new SaveDataV4();
        data.backpackSlots = new InventorySlotDto[_slots.Count];

        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            data.backpackSlots[i] = new InventorySlotDto
            {
                type = s.IsEmpty ? default : s.type,
                amount = s.IsEmpty ? 0 : s.amount
            };
        }

        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            data.overflow.Add(new Entry { type = t, amount = GetOverflowBuffer(t) });

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();
    }

    public void LoadFromMemory()
    {
        if (!HasSave())
        {
            InitDefaultsIfNeeded();
            BroadcastAll();
            return;
        }

        string json = PlayerPrefs.GetString(saveKey, "");
        if (string.IsNullOrWhiteSpace(json))
        {
            InitDefaultsIfNeeded();
            BroadcastAll();
            return;
        }

        try
        {
            _overflowBuffer.Clear();
            foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
                _overflowBuffer[t] = 0;

            if (json.Contains("\"backpackSlots\""))
            {
                var data = JsonUtility.FromJson<SaveDataV4>(json);
                EnsureSlotListSize();
                ClearAllSlots();

                if (data != null && data.backpackSlots != null)
                {
                    int n = Mathf.Min(data.backpackSlots.Length, _slots.Count);
                    for (int i = 0; i < n; i++)
                    {
                        var dto = data.backpackSlots[i];
                        int amt = Mathf.Max(0, dto.amount);
                        if (amt <= 0)
                            _slots[i] = InventorySlot.Empty;
                        else
                        {
                            int cap = GetStackSize(dto.type);
                            amt = Mathf.Min(amt, cap);
                            _slots[i] = new InventorySlot { type = dto.type, amount = amt };
                        }
                    }
                }

                if (data != null && data.overflow != null)
                {
                    foreach (var e in data.overflow)
                        _overflowBuffer[e.type] = Mathf.Max(0, e.amount);
                }
            }
            else
            {
                LoadLegacyAggregateAndMigrate(json);
            }
        }
        catch
        {
            foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
                _overflowBuffer[t] = 0;
            InitDefaultsIfNeeded();
        }

        EnsureOverflowKeys();
        TryFlushOverflowBuffer(false);
        BroadcastAll();
    }

    private void LoadLegacyAggregateAndMigrate(string json)
    {
        var amounts = new Dictionary<ResourceType, int>();
        var displayOrder = new List<ResourceType>();

        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            amounts[t] = 0;

        if (json.Contains("\"displayOrder\""))
        {
            var data = JsonUtility.FromJson<SaveDataV3>(json);
            if (data?.entries != null)
            {
                foreach (var e in data.entries)
                    amounts[e.type] = Mathf.Max(0, e.amount);
            }

            if (data?.overflow != null)
            {
                foreach (var e in data.overflow)
                    _overflowBuffer[e.type] = Mathf.Max(0, e.amount);
            }

            if (data?.displayOrder != null)
            {
                var seen = new HashSet<ResourceType>();
                foreach (var e in data.displayOrder)
                {
                    if (seen.Add(e.type))
                        displayOrder.Add(e.type);
                }
            }
        }
        else if (json.Contains("\"overflow\""))
        {
            var data = JsonUtility.FromJson<SaveDataV2>(json);
            if (data?.entries != null)
            {
                foreach (var e in data.entries)
                    amounts[e.type] = Mathf.Max(0, e.amount);
            }

            if (data?.overflow != null)
            {
                foreach (var e in data.overflow)
                    _overflowBuffer[e.type] = Mathf.Max(0, e.amount);
            }
        }
        else
        {
            var data = JsonUtility.FromJson<SaveDataV1>(json);
            if (data?.entries != null)
            {
                foreach (var e in data.entries)
                    amounts[e.type] = Mathf.Max(0, e.amount);
            }
        }

        MigrateAggregateDictToSlots(amounts, displayOrder);
    }

    /// <summary>
    /// Fills backpackSlots from legacy per-type totals. Chunks by stackSize along displayOrder, then enum order.
    /// Slots beyond MaxSlots: remainder goes to overflow buffer, or dropped to world when DropOverflow and prefab set.
    /// </summary>
    private void MigrateAggregateDictToSlots(Dictionary<ResourceType, int> amounts, List<ResourceType> displayOrder)
    {
        EnsureSlotListSize();
        ClearAllSlots();

        var queued = new List<(ResourceType type, int amt)>();
        var seenTypes = new HashSet<ResourceType>();

        void EnqueueChunks(ResourceType t, int count)
        {
            if (count <= 0) return;
            int ss = GetStackSize(t);
            while (count > 0)
            {
                int take = Mathf.Min(ss, count);
                queued.Add((t, take));
                count -= take;
            }
        }

        if (displayOrder != null)
        {
            for (int i = 0; i < displayOrder.Count; i++)
            {
                var t = displayOrder[i];
                if (seenTypes.Contains(t)) continue;
                seenTypes.Add(t);
                int c = amounts.TryGetValue(t, out int v) ? v : 0;
                EnqueueChunks(t, c);
            }
        }

        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            if (seenTypes.Contains(t)) continue;
            int c = amounts.TryGetValue(t, out int v) ? v : 0;
            EnqueueChunks(t, c);
        }

        int slotWrite = 0;
        var mode = GetOverflowMode();
        var dropPrefab = overflowDropPrefab != null ? overflowDropPrefab : dropResourcePrefab;

        for (int q = 0; q < queued.Count; q++)
        {
            if (slotWrite < _slots.Count)
            {
                var chunk = queued[q];
                _slots[slotWrite++] = new InventorySlot { type = chunk.type, amount = chunk.amt };
            }
            else
            {
                var chunk = queued[q];
                if (mode == BackpackOverflowMode.DropOverflow && dropPrefab != null)
                {
                    Vector2 scatter = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, overflowDropScatterRadius);
                    Vector3 pos = transform.position + new Vector3(scatter.x, scatter.y, 0f);
                    var drop = Instantiate(dropPrefab, pos, Quaternion.identity);
                    drop.Configure(chunk.type, chunk.amt);
                }
                else
                {
                    _overflowBuffer[chunk.type] = GetOverflowBuffer(chunk.type) + chunk.amt;
                }
            }
        }

        RaiseLayoutChanged();
    }

    public bool HasSave()
    {
        return PlayerPrefs.HasKey(saveKey);
    }

    public void ClearSave()
    {
        if (PlayerPrefs.HasKey(saveKey))
        {
            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
        }
    }

    public void ResetToDefaults(bool alsoClearSave = false)
    {
        if (alsoClearSave)
            ClearSave();

        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            _overflowBuffer[t] = 0;

        InitDefaultsIfNeeded();
        BroadcastAll();
    }
}
