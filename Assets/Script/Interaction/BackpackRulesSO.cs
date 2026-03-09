using System;
using System.Collections.Generic;
using UnityEngine;

public enum BackpackOverflowMode
{
    DenyPickup,
    DropOverflow,
    TempBuffer
}

[CreateAssetMenu(menuName = "BackyardSiege/Backpack Rules", fileName = "BackpackRules")]
public class BackpackRulesSO : ScriptableObject
{
    [Serializable]
    public class ResourceRule
    {
        public ResourceType type;
        public string displayName;
        public Sprite icon;
        [Min(1)] public int stackSize = 20;
        [Min(-1)] public int maxCarry = -1;
        public bool showInUI = true;
        public QuickUseItemSO quickUseItem;
    }

    [Header("Capacity")]
    [Min(1)] public int maxSlots = 16;
    [Min(-1)] public int maxTotalItems = -1;

    [Header("Overflow Default")]
    public BackpackOverflowMode overflowMode = BackpackOverflowMode.DenyPickup;

    [Header("Per Resource Rules")]
    public List<ResourceRule> rules = new List<ResourceRule>();

    private readonly Dictionary<ResourceType, ResourceRule> _cache = new Dictionary<ResourceType, ResourceRule>();
    private int _cacheToken;
    private int _builtToken = -1;

    private void OnEnable()
    {
        _cacheToken++;
    }

    private void OnValidate()
    {
        _cacheToken++;
    }

    private void EnsureCache()
    {
        if (_builtToken == _cacheToken) return;

        _cache.Clear();
        if (rules != null)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (r == null) continue;
                _cache[r.type] = r;
            }
        }

        _builtToken = _cacheToken;
    }

    private ResourceRule GetRule(ResourceType type)
    {
        EnsureCache();
        _cache.TryGetValue(type, out var r);
        return r;
    }

    public int GetStackSize(ResourceType type) => Mathf.Max(1, GetRule(type)?.stackSize ?? 20);
    public int GetMaxCarry(ResourceType type) => GetRule(type)?.maxCarry ?? -1;
    public Sprite GetIcon(ResourceType type) => GetRule(type)?.icon;
    public QuickUseItemSO GetQuickUseItem(ResourceType type) => GetRule(type)?.quickUseItem;

    public string GetDisplayName(ResourceType type)
    {
        var r = GetRule(type);
        if (r == null) return type.ToString();
        return string.IsNullOrWhiteSpace(r.displayName) ? type.ToString() : r.displayName;
    }

    public bool GetShowInUI(ResourceType type) => GetRule(type)?.showInUI ?? true;
}
