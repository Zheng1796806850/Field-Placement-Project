using UnityEngine;
using UnityEngine.Tilemaps;

public class PlaceWallObjective : TutorialObjective
{
    [Min(1)] public int requiredPlacedCount = 1;

    [Header("Count filter")]
    [Tooltip("If true, only walls inside the sphere around Area Center are counted (legacy).")]
    public bool useAreaFilter = false;
    public Transform areaCenter;
    [Min(0.1f)] public float areaRadius = 8f;
    [Tooltip("If true, only walls in this grid's region are counted (recommended with per-area Grid + Tilemap).")]
    public bool useGridFilter = false;
    public Grid wallCountGrid;
    [Tooltip("Optional. When set, wall must map to a cell inside this tilemap's cellBounds (your area's floor/wall tilemap).")]
    public Tilemap wallCountRegionTilemap;

    private int _count;

    private void Update()
    {
        if (IsCompleted) return;

        var all = FindObjectsByType<PlayerPlacedWall>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int c = 0;

        for (int i = 0; i < all.Length; i++)
        {
            var w = all[i];
            if (w == null) continue;
            if (!ShouldCountWall(w)) continue;
            c++;
        }

        _count = c;

        if (_count >= requiredPlacedCount)
            Complete();
    }

    private bool ShouldCountWall(PlayerPlacedWall w)
    {
        if (useGridFilter && wallCountGrid != null)
        {
            if (!IsWallInGridRegion(w))
                return false;
        }

        if (useAreaFilter && areaCenter != null)
        {
            float d = Vector2.Distance(areaCenter.position, w.transform.position);
            if (d > areaRadius) return false;
        }

        return true;
    }

    private bool IsWallInGridRegion(PlayerPlacedWall w)
    {
        if (wallCountGrid == null) return true;

        if (wallCountRegionTilemap != null)
        {
            Vector3Int cell = wallCountRegionTilemap.WorldToCell(w.transform.position);
            if (!wallCountRegionTilemap.cellBounds.Contains(cell))
                return false;
        }

        Vector3Int gc = wallCountGrid.WorldToCell(w.transform.position);
        Vector3 center = wallCountGrid.GetCellCenterWorld(gc);
        float cellMax = Mathf.Max(Mathf.Abs(wallCountGrid.cellSize.x), Mathf.Abs(wallCountGrid.cellSize.y));
        if (Vector2.Distance(center, w.transform.position) > cellMax * 0.55f)
            return false;

        return true;
    }

    public override string GetProgressText()
    {
        return $"Place wooden wall {_count}/{requiredPlacedCount}";
    }
}
