using System.Collections.Generic;
using UnityEngine;

public class BuildWaterCollectorObjective : TutorialObjective
{
    public List<WaterCollectorBuildSpot> collectors = new List<WaterCollectorBuildSpot>();
    [Min(1)] public int requiredBuiltCount = 1;
    [Min(0)] public int requiredWaterInInventory = 3;
    [Tooltip("How much Water must be gained during this objective vs inventory when it starts (not interaction count).")]
    public ResourceType waterResourceType = ResourceType.Water;

    private int _builtCount;
    private PlayerResourceInventory _inventory;
    private int _waterAmountAtBegin;

    protected override void OnBegin()
    {
        if (collectors == null || collectors.Count == 0)
        {
            var all = FindObjectsByType<WaterCollectorBuildSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            collectors = new List<WaterCollectorBuildSpot>(all);
        }

        for (int i = 0; i < collectors.Count; i++)
        {
            if (collectors[i] != null)
            {
                collectors[i].OnBuiltChanged -= HandleBuiltChanged;
                collectors[i].OnBuiltChanged += HandleBuiltChanged;
            }
        }

        _inventory = ResolveInventory();
        _waterAmountAtBegin = _inventory != null ? _inventory.Get(waterResourceType) : 0;
        if (_inventory != null)
        {
            _inventory.OnResourceChanged -= HandleResourceChanged;
            _inventory.OnResourceChanged += HandleResourceChanged;
        }

        Recount();
        TryComplete();
    }

    protected override void OnEnd()
    {
        if (_inventory != null)
        {
            _inventory.OnResourceChanged -= HandleResourceChanged;
            _inventory = null;
        }

        if (collectors == null) return;
        for (int i = 0; i < collectors.Count; i++)
        {
            if (collectors[i] != null)
                collectors[i].OnBuiltChanged -= HandleBuiltChanged;
        }
    }

    private void HandleBuiltChanged(bool built)
    {
        Recount();
        TryComplete();
    }

    private void HandleResourceChanged(ResourceType type, int amount)
    {
        if (type != waterResourceType) return;
        TryComplete();
    }

    private PlayerResourceInventory ResolveInventory()
    {
        if (manager != null && manager.player != null)
        {
            var inv = manager.player.GetComponent<PlayerResourceInventory>();
            if (inv != null) return inv;
        }

        return PlayerResourceInventory.Instance;
    }

    private void TryComplete()
    {
        if (IsCompleted) return;
        if (_builtCount < requiredBuiltCount) return;

        if (requiredWaterInInventory > 0)
        {
            if (_inventory == null) return;
            int gained = _inventory.Get(waterResourceType) - _waterAmountAtBegin;
            if (gained < requiredWaterInInventory) return;
        }

        Complete();
    }

    private void Recount()
    {
        int c = 0;
        if (collectors != null)
        {
            for (int i = 0; i < collectors.Count; i++)
            {
                if (collectors[i] != null && collectors[i].IsBuilt)
                    c++;
            }
        }

        _builtCount = c;
    }

    public override string GetProgressText()
    {
        if (requiredWaterInInventory > 0)
        {
            int gained = _inventory != null ? _inventory.Get(waterResourceType) - _waterAmountAtBegin : 0;
            if (gained < 0) gained = 0;
            return $"Build {_builtCount}/{requiredBuiltCount}, Water +{gained}/{requiredWaterInInventory}";
        }

        return $"Build water collector {_builtCount}/{requiredBuiltCount}";
    }
}
