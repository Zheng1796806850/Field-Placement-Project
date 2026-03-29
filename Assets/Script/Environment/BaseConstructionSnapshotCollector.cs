using UnityEngine;
using UnityEngine.SceneManagement;

public static class BaseConstructionSnapshotCollector
{
    public static void CaptureFromSceneIfBase()
    {
        var applier = Object.FindFirstObjectByType<BaseConstructionSnapshotApplier>();
        string name = applier != null && !string.IsNullOrWhiteSpace(applier.BaseSceneName)
            ? applier.BaseSceneName
            : "BaseScene";
        CaptureFromSceneIfBase(name);
    }

    public static void CaptureFromSceneIfBase(string baseSceneName)
    {
        if (string.IsNullOrWhiteSpace(baseSceneName))
            return;

        var active = SceneManager.GetActiveScene().name;
        if (!string.Equals(active, baseSceneName, System.StringComparison.OrdinalIgnoreCase))
            return;

        ForcePersistAllWaterCollectors();

        var snap = new BaseConstructionSnapshot();

        var plots = Object.FindObjectsByType<FarmlandPlot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < plots.Length; i++)
        {
            var p = plots[i];
            if (p == null) continue;
            snap.farmlands.Add(p.BuildSnapshotEntry());
        }

        var placed = Object.FindObjectsByType<PlayerPlacedWall>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < placed.Length; i++)
        {
            var m = placed[i];
            if (m == null) continue;

            var wall = m.GetComponentInChildren<WoodenWallDurability>(true);
            if (wall == null) continue;
            if (wall.CurrentState == WallBuildState.Removed) continue;

            snap.walls.Add(BuildWallEntry(m, wall));
        }

        BaseConstructionSnapshotStore.SetPending(snap);
    }

    private static void ForcePersistAllWaterCollectors()
    {
        var collectors = Object.FindObjectsByType<WaterCollectorBuildSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < collectors.Length; i++)
        {
            if (collectors[i] != null)
                collectors[i].ForcePersistRuntimeState();
        }
    }

    private static PlacedWallEntry BuildWallEntry(PlayerPlacedWall marker, WoodenWallDurability wall)
    {
        var t = wall.transform;
        var health = wall.health != null ? wall.health : wall.GetComponent<Health>();

        float rubbleTime = -1f;
        if (wall.CurrentState == WallBuildState.Rubble)
            rubbleTime = wall.GetRubbleTimeRemainingForSnapshot();

        return new PlacedWallEntry
        {
            wallPlacementId = marker.WallPlacementId,
            worldPosition = t.position,
            rotation = t.rotation,
            gridCell = marker.GridCell,
            wallState = (int)wall.CurrentState,
            currentHP = health != null ? health.currentHP : 0,
            maxHP = health != null ? health.maxHP : wall.wallMaxHP,
            rubbleTimeRemaining = rubbleTime
        };
    }
}
