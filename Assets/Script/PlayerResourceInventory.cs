using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerResourceInventory : MonoBehaviour
{
    public static PlayerResourceInventory Instance { get; private set; }

    [Header("Defaults (used if no save exists yet)")]
    public int defaultPlanks = 0;
    public int defaultSeeds = 0;
    public int defaultWater = 0;
    public int defaultFood = 0;

    [Header("Persistence")]
    [Tooltip("PlayerPrefs key used to store inventory JSON.")]
    public string saveKey = "PLAYER_RESOURCE_INVENTORY_V1";
    public bool autoLoadOnAwake = true;
    public bool dontDestroyOnLoad = true;

    public event Action<ResourceType, int> OnResourceChanged;
    public event Action OnAnyResourceChanged;

    private readonly Dictionary<ResourceType, int> _amounts = new Dictionary<ResourceType, int>();

    [Serializable]
    private class SaveData
    {
        public List<Entry> entries = new List<Entry>();
    }

    [Serializable]
    private class Entry
    {
        public ResourceType type;
        public int amount;
    }

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

    private void InitDefaultsIfNeeded()
    {
        if (_amounts.Count > 0) return;

        _amounts[ResourceType.Planks] = Mathf.Max(0, defaultPlanks);
        _amounts[ResourceType.Seeds] = Mathf.Max(0, defaultSeeds);
        _amounts[ResourceType.Water] = Mathf.Max(0, defaultWater);
        _amounts[ResourceType.Food] = Mathf.Max(0, defaultFood);
    }

    public int Get(ResourceType type)
    {
        if (_amounts.TryGetValue(type, out int v)) return v;
        return 0;
    }

    public void Set(ResourceType type, int amount)
    {
        amount = Mathf.Max(0, amount);
        _amounts[type] = amount;
        OnResourceChanged?.Invoke(type, amount);
        OnAnyResourceChanged?.Invoke();
    }

    public void Add(ResourceType type, int delta)
    {
        if (delta == 0) return;
        int next = Mathf.Max(0, Get(type) + delta);
        Set(type, next);
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
        {
            Spend(kv.Key, kv.Value);
        }
        return true;
    }

    public void SaveInMemory()
    {
        SaveData data = new SaveData();
        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            data.entries.Add(new Entry
            {
                type = t,
                amount = Get(t)
            });
        }

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

        SaveData data = null;
        try
        {
            data = JsonUtility.FromJson<SaveData>(json);
        }
        catch
        {
            data = null;
        }

        if (data == null || data.entries == null)
        {
            InitDefaultsIfNeeded();
            BroadcastAll();
            return;
        }

        _amounts.Clear();
        foreach (var e in data.entries)
        {
            if (e == null) continue;
            _amounts[e.type] = Mathf.Max(0, e.amount);
        }

        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            if (!_amounts.ContainsKey(t))
                _amounts[t] = 0;
        }

        BroadcastAll();
    }

    public void ClearSave()
    {
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();
    }

    public bool HasSave()
    {
        return PlayerPrefs.HasKey(saveKey);
    }

    public void ResetToDefaults(bool alsoClearSave = false)
    {
        if (alsoClearSave)
            ClearSave();

        _amounts.Clear();
        InitDefaultsIfNeeded();
        BroadcastAll();
    }

    private void BroadcastAll()
    {
        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            OnResourceChanged?.Invoke(t, Get(t));
        }
        OnAnyResourceChanged?.Invoke();
    }
}
