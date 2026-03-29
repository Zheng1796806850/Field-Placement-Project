using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FarmlandPlotEntry
{
    public string plotId;
    public string cropId;
    public int growthDaysCompleted;
    public bool wateredSinceLastDayStart;
    public int plotState;
}

[Serializable]
public class PlacedWallEntry
{
    public string wallPlacementId;
    public Vector3 worldPosition;
    public Quaternion rotation;
    public Vector3Int gridCell;
    public int wallState;
    public int currentHP;
    public int maxHP;
    public float rubbleTimeRemaining;
}

[Serializable]
public class BaseConstructionSnapshot
{
    public List<FarmlandPlotEntry> farmlands = new List<FarmlandPlotEntry>();
    public List<PlacedWallEntry> walls = new List<PlacedWallEntry>();
}

public static class BaseConstructionSnapshotStore
{
    public static bool HasPending { get; private set; }

    private static BaseConstructionSnapshot _pending;

    public static void SetPending(BaseConstructionSnapshot snapshot)
    {
        _pending = snapshot ?? new BaseConstructionSnapshot();
        HasPending = true;
    }

    public static BaseConstructionSnapshot GetPendingSnapshotOrNull()
    {
        return HasPending ? _pending : null;
    }

    public static void ClearPending()
    {
        HasPending = false;
        _pending = null;
    }
}

public static class CropConfigCatalogUtil
{
    public static CropConfigSO ResolveByCropId(string cropId, CropConfigSO[] catalog)
    {
        if (string.IsNullOrEmpty(cropId) || catalog == null) return null;

        for (int i = 0; i < catalog.Length; i++)
        {
            var c = catalog[i];
            if (c == null) continue;
            if (c.cropId == cropId) return c;
        }

        return null;
    }
}
