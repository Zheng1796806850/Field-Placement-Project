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

    private readonly Dictionary<ResourceType, int> _amounts = new Dictionary<ResourceType, int>();
    private readonly Dictionary<ResourceType, int> _overflowBuffer = new Dictionary<ResourceType, int>();
    private readonly List<BackpackStackView> _stackCache = new List<BackpackStackView>();
    private readonly List<ResourceType> _displayOrder = new List<ResourceType>();

    private bool _flushingBuffer;

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
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SanitizeDefaultResources();
    }
#endif

    private void InitDefaultsIfNeeded()
    {
        EnsureAllResourceKeys();
        MigrateLegacyDefaultsToListIfNeeded();
        SanitizeDefaultResources();

        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            _amounts[t] = 0;
            _overflowBuffer[t] = Mathf.Max(0, GetOverflowBuffer(t));
        }

        if (defaultResources != null)
        {
            for (int i = 0; i < defaultResources.Count; i++)
            {
                var entry = defaultResources[i];
                if (entry == null) continue;
                if (entry.amount <= 0) continue;

                int current = Get(entry.type);
                int next = current + Mathf.Max(0, entry.amount);

                int maxCarry = GetMaxCarry(entry.type);
                if (maxCarry >= 0)
                    next = Mathf.Min(next, maxCarry);

                _amounts[entry.type] = next;
            }
        }

        EnsureDisplayOrder();
        EnsureWithinCapacityToBuffer(false);
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

    private void EnsureAllResourceKeys()
    {
        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            if (!_amounts.ContainsKey(t))
                _amounts[t] = 0;

            if (!_overflowBuffer.ContainsKey(t))
                _overflowBuffer[t] = 0;
        }
    }

    private void EnsureDisplayOrder()
    {
        var seen = new HashSet<ResourceType>();
        for (int i = _displayOrder.Count - 1; i >= 0; i--)
        {
            var t = _displayOrder[i];
            if (seen.Contains(t))
            {
                _displayOrder.RemoveAt(i);
                continue;
            }
            seen.Add(t);
        }

        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            if (!seen.Contains(t))
                _displayOrder.Add(t);
        }
    }

    public int Get(ResourceType type)
    {
        if (_amounts.TryGetValue(type, out int v)) return v;
        return 0;
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
        int total = 0;
        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            total += StacksFor(Get(t), GetStackSize(t));
        return total;
    }

    public int GetFreeSlots() => Mathf.Max(0, MaxSlots - GetUsedSlots());

    public int PreviewMaxAddable(ResourceType type, int request) => ComputeMaxAddable(type, request);
    public bool CanAcceptAny(ResourceType type, int request) => PreviewMaxAddable(type, request) > 0;

    public IReadOnlyList<BackpackStackView> GetStackViewsSnapshot()
    {
        EnsureDisplayOrder();
        _stackCache.Clear();

        for (int i = 0; i < _displayOrder.Count; i++)
        {
            ResourceType t = _displayOrder[i];
            int count = Get(t);
            if (count <= 0) continue;

            int s = GetStackSize(t);
            while (count > 0)
            {
                int take = Mathf.Min(s, count);
                _stackCache.Add(new BackpackStackView { type = t, amountInStack = take, stackSize = s });
                count -= take;
            }
        }

        return _stackCache;
    }

    public bool TryGetStackViewAtDisplayIndex(int displayIndex, out BackpackStackView view)
    {
        var snapshot = GetStackViewsSnapshot();
        if (displayIndex >= 0 && displayIndex < snapshot.Count)
        {
            view = snapshot[displayIndex];
            return true;
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

    public bool ReorderDisplaySlot(int fromDisplayIndex, int toDisplayIndex)
    {
        if (!TryGetResourceTypeAtDisplayIndex(fromDisplayIndex, out var fromType))
            return false;

        if (TryGetResourceTypeAtDisplayIndex(toDisplayIndex, out var toType))
        {
            if (fromType == toType)
                return false;

            return SwapDisplayOrder(fromType, toType);
        }

        return InsertDisplayOrderAtEmptyTarget(fromType, toDisplayIndex);
    }

    private bool InsertDisplayOrderAtEmptyTarget(ResourceType fromType, int toDisplayIndex)
    {
        EnsureDisplayOrder();

        int beforeTotalRows = GetStackViewsSnapshot().Count;
        int fromStacks = StacksFor(Get(fromType), GetStackSize(fromType));
        if (fromStacks <= 0)
            return false;

        int otherRows = beforeTotalRows - fromStacks;
        int targetRow = Mathf.Min(toDisplayIndex, Mathf.Max(0, otherRows));

        int sourceIndex = _displayOrder.IndexOf(fromType);
        if (sourceIndex < 0)
            return false;

        var orderBefore = new List<ResourceType>(_displayOrder);

        _displayOrder.RemoveAt(sourceIndex);

        int row = 0;
        int insertIndex = _displayOrder.Count;
        for (int i = 0; i < _displayOrder.Count; i++)
        {
            ResourceType t = _displayOrder[i];
            if (Get(t) <= 0)
                continue;

            int stacks = StacksFor(Get(t), GetStackSize(t));
            if (targetRow <= row)
            {
                insertIndex = i;
                break;
            }

            row += stacks;
        }

        _displayOrder.Insert(insertIndex, fromType);

        if (DisplayOrderSequencesEqual(orderBefore, _displayOrder))
            return false;

        RaiseLayoutChanged();
        return true;
    }

    private static bool DisplayOrderSequencesEqual(List<ResourceType> a, List<ResourceType> b)
    {
        if (a.Count != b.Count)
            return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }

    public bool BindQuickSlotCandidateFromDisplayIndex(int displayIndex, out ResourceType type)
    {
        return TryGetResourceTypeAtDisplayIndex(displayIndex, out type);
    }

    private bool SwapDisplayOrder(ResourceType a, ResourceType b)
    {
        EnsureDisplayOrder();

        int aIndex = _displayOrder.IndexOf(a);
        int bIndex = _displayOrder.IndexOf(b);
        if (aIndex < 0 || bIndex < 0 || aIndex == bIndex)
            return false;

        (_displayOrder[aIndex], _displayOrder[bIndex]) = (_displayOrder[bIndex], _displayOrder[aIndex]);
        RaiseLayoutChanged();
        return true;
    }

    private bool MoveTypeToEndOfOccupied(ResourceType type)
    {
        EnsureDisplayOrder();

        int sourceIndex = _displayOrder.IndexOf(type);
        if (sourceIndex < 0)
            return false;

        int insertIndex = 0;
        for (int i = 0; i < _displayOrder.Count; i++)
        {
            var current = _displayOrder[i];
            if (current == type) continue;
            if (Get(current) > 0)
                insertIndex = i + 1;
        }

        if (insertIndex > sourceIndex)
            insertIndex--;

        if (insertIndex == sourceIndex)
            return false;

        _displayOrder.RemoveAt(sourceIndex);
        _displayOrder.Insert(Mathf.Clamp(insertIndex, 0, _displayOrder.Count), type);
        RaiseLayoutChanged();
        return true;
    }

    public void Set(ResourceType type, int amount)
    {
        amount = Mathf.Max(0, amount);

        int maxCarry = GetMaxCarry(type);
        if (maxCarry >= 0) amount = Mathf.Min(amount, maxCarry);

        _amounts[type] = amount;
        EnsureAllResourceKeys();
        EnsureDisplayOrder();
        EnsureWithinCapacityToBuffer(false);
        TryFlushOverflowBuffer(false);
        Broadcast(type);
    }

    public void Add(ResourceType type, int delta)
    {
        if (delta == 0) return;

        if (delta < 0)
        {
            int next = Mathf.Max(0, Get(type) + delta);
            _amounts[type] = next;
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

        Set(type, Get(type) - cost);
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

        EnsureAllResourceKeys();
        EnsureDisplayOrder();

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
            _amounts[type] = Get(type) + accepted;

        if (rejected > 0)
            HandleOverflow(type, rejected, worldPos, mode, showMessages);

        EnsureWithinCapacityToBuffer(showMessages);
        TryFlushOverflowBuffer(false);

        if (accepted > 0 || rejected > 0)
            Broadcast(type);

        return rejected == 0;
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
            _amounts[t] = Get(t) + can;
            movedAny = true;
        }

        if (movedAny)
        {
            EnsureWithinCapacityToBuffer(false);
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

    private void EnsureWithinCapacityToBuffer(bool showMessage)
    {
        int maxSlots = MaxSlots;
        int used = GetUsedSlots();
        if (used <= maxSlots) return;

        int safety = 0;
        while (used > maxSlots && safety++ < 10000)
        {
            bool reduced = false;

            var values = (ResourceType[])Enum.GetValues(typeof(ResourceType));
            for (int i = values.Length - 1; i >= 0; i--)
            {
                var t = values[i];
                int c = Get(t);
                if (c <= 0) continue;

                int s = GetStackSize(t);
                int stacks = StacksFor(c, s);
                if (stacks <= 0) continue;

                int target = Mathf.Max(0, (stacks - 1) * s);
                int move = c - target;
                if (move <= 0) continue;

                _amounts[t] = target;
                _overflowBuffer[t] = GetOverflowBuffer(t) + move;

                used -= 1;
                reduced = true;
                break;
            }

            if (!reduced) break;
        }

        if (showMessage) RaiseMessage("Backpack capacity exceeded: moved to overflow buffer");
    }

    private int ComputeMaxAddable(ResourceType type, int request)
    {
        if (request <= 0) return 0;

        int s = GetStackSize(type);
        int current = Get(type);

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
            foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
                total += Get(t);

            int byTotal = Mathf.Max(0, maxTotalItems - total);
            request = Mathf.Min(request, byTotal);
            if (request <= 0) return 0;
        }

        int maxSlots = MaxSlots;
        int usedSlots = GetUsedSlots();
        int freeSlots = Mathf.Max(0, maxSlots - usedSlots);

        int stacksBefore = StacksFor(current, s);
        int capBySlots;

        if (stacksBefore <= 0)
        {
            capBySlots = freeSlots * s;
        }
        else
        {
            int partialCap = (stacksBefore * s) - current;
            capBySlots = partialCap + (freeSlots * s);
        }

        if (capBySlots <= 0) return 0;
        return Mathf.Min(request, capBySlots);
    }

    private static int StacksFor(int count, int stackSize)
    {
        if (count <= 0) return 0;
        stackSize = Mathf.Max(1, stackSize);
        return (count + stackSize - 1) / stackSize;
    }

    private void Broadcast(ResourceType type)
    {
        OnResourceChanged?.Invoke(type, Get(type));
        OnAnyResourceChanged?.Invoke();
    }

    private void BroadcastAll()
    {
        EnsureAllResourceKeys();
        EnsureDisplayOrder();

        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            OnResourceChanged?.Invoke(t, Get(t));
        OnAnyResourceChanged?.Invoke();
    }

    private void RaiseLayoutChanged()
    {
        OnAnyResourceChanged?.Invoke();
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
        SaveDataV3 data = new SaveDataV3();

        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            data.entries.Add(new Entry { type = t, amount = Get(t) });
            data.overflow.Add(new Entry { type = t, amount = GetOverflowBuffer(t) });
        }

        EnsureDisplayOrder();
        for (int i = 0; i < _displayOrder.Count; i++)
            data.displayOrder.Add(new OrderEntry { type = _displayOrder[i] });

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
            _amounts.Clear();
            _overflowBuffer.Clear();
            _displayOrder.Clear();

            if (json.Contains("\"displayOrder\""))
            {
                var data = JsonUtility.FromJson<SaveDataV3>(json);
                if (data != null && data.entries != null)
                {
                    foreach (var e in data.entries)
                        _amounts[e.type] = Mathf.Max(0, e.amount);
                }

                if (data != null && data.overflow != null)
                {
                    foreach (var e in data.overflow)
                        _overflowBuffer[e.type] = Mathf.Max(0, e.amount);
                }

                if (data != null && data.displayOrder != null)
                {
                    foreach (var e in data.displayOrder)
                        _displayOrder.Add(e.type);
                }
            }
            else if (json.Contains("\"overflow\""))
            {
                var data = JsonUtility.FromJson<SaveDataV2>(json);
                if (data != null && data.entries != null)
                {
                    foreach (var e in data.entries)
                        _amounts[e.type] = Mathf.Max(0, e.amount);
                }

                if (data != null && data.overflow != null)
                {
                    foreach (var e in data.overflow)
                        _overflowBuffer[e.type] = Mathf.Max(0, e.amount);
                }
            }
            else
            {
                var data = JsonUtility.FromJson<SaveDataV1>(json);
                if (data != null && data.entries != null)
                {
                    foreach (var e in data.entries)
                        _amounts[e.type] = Mathf.Max(0, e.amount);
                }
            }
        }
        catch
        {
            _amounts.Clear();
            _overflowBuffer.Clear();
            _displayOrder.Clear();
            InitDefaultsIfNeeded();
        }

        EnsureAllResourceKeys();
        EnsureDisplayOrder();
        EnsureWithinCapacityToBuffer(false);
        TryFlushOverflowBuffer(false);
        BroadcastAll();
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

        _amounts.Clear();
        _overflowBuffer.Clear();
        _displayOrder.Clear();
        InitDefaultsIfNeeded();
        BroadcastAll();
    }
}
