using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BaseConstructionSnapshotApplier : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Must match the Base scene asset name (Build Settings).")]
    public string baseSceneName = "BaseScene";

    [Header("Crop resolution")]
    [Tooltip("Assign every CropConfigSO that can appear on farmland in Base.")]
    public CropConfigSO[] cropCatalog = new CropConfigSO[0];

    [Header("Wall resolution")]
    [Tooltip("Assign every WallPlacementQuickUseSO used to build player walls (ids must match wallPlacementId).")]
    public WallPlacementQuickUseSO[] wallCatalog = new WallPlacementQuickUseSO[0];

    [Header("Timing")]
    [Min(0)]
    [Tooltip("Extra frames after load before applying (avoids Awake/Start order issues).")]
    public int applyDelayFrames = 1;

    public string BaseSceneName => baseSceneName;

    private IEnumerator Start()
    {
        for (int i = 0; i < applyDelayFrames; i++)
            yield return null;

        if (string.IsNullOrWhiteSpace(baseSceneName))
            yield break;

        var active = SceneManager.GetActiveScene().name;
        if (!string.Equals(active, baseSceneName, System.StringComparison.OrdinalIgnoreCase))
            yield break;

        var snapshot = BaseConstructionSnapshotStore.GetPendingSnapshotOrNull();
        if (snapshot == null)
            yield break;

        ApplyFarmlands(snapshot);

        DestroyExistingPlayerWalls();
        yield return null;

        ApplyWalls(snapshot);

        BaseConstructionSnapshotStore.ClearPending();
    }

    private static void DestroyExistingPlayerWalls()
    {
        var placed = Object.FindObjectsByType<PlayerPlacedWall>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < placed.Length; i++)
        {
            if (placed[i] != null)
                Destroy(placed[i].gameObject);
        }
    }

    private void ApplyFarmlands(BaseConstructionSnapshot snapshot)
    {
        if (snapshot.farmlands == null || snapshot.farmlands.Count == 0)
            return;

        var plots = Object.FindObjectsByType<FarmlandPlot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var map = new Dictionary<string, FarmlandPlot>(System.StringComparer.Ordinal);
        for (int i = 0; i < plots.Length; i++)
        {
            var p = plots[i];
            if (p == null || string.IsNullOrWhiteSpace(p.plotId))
                continue;
            if (!map.ContainsKey(p.plotId))
                map.Add(p.plotId, p);
        }

        for (int i = 0; i < snapshot.farmlands.Count; i++)
        {
            var e = snapshot.farmlands[i];
            if (e == null || string.IsNullOrWhiteSpace(e.plotId))
                continue;
            if (!map.TryGetValue(e.plotId, out var plot))
                continue;

            plot.TryApplySnapshotFromTravel(e, cropCatalog);
        }
    }

    private void ApplyWalls(BaseConstructionSnapshot snapshot)
    {
        if (snapshot.walls == null || snapshot.walls.Count == 0)
            return;

        for (int i = 0; i < snapshot.walls.Count; i++)
        {
            var entry = snapshot.walls[i];
            if (entry == null) continue;

            if (!System.Enum.IsDefined(typeof(WallBuildState), entry.wallState))
                continue;

            var ws = (WallBuildState)entry.wallState;
            if (ws == WallBuildState.Removed)
                continue;

            var cfg = ResolveWallConfig(entry.wallPlacementId);
            if (cfg == null || cfg.wallPrefab == null)
            {
                Debug.LogWarning($"[BaseConstructionSnapshotApplier] Unknown wallPlacementId '{entry.wallPlacementId}'. Skipping wall restore.");
                continue;
            }

            var go = Object.Instantiate(cfg.wallPrefab, entry.worldPosition, entry.rotation);

            var marker = go.GetComponent<PlayerPlacedWall>() ?? go.AddComponent<PlayerPlacedWall>();
            marker.SetPlacement(entry.wallPlacementId, entry.gridCell);

            var wall = go.GetComponentInChildren<WoodenWallDurability>(true);
            if (wall == null)
            {
                Debug.LogWarning("[BaseConstructionSnapshotApplier] Wall prefab missing WoodenWallDurability.");
                Destroy(go);
                continue;
            }

            wall.SetPlacementCostEquivalent(cfg.placementCost);
            wall.ApplyRestoredPlacementState(ws, entry.currentHP, entry.maxHP, entry.rubbleTimeRemaining);
        }
    }

    private WallPlacementQuickUseSO ResolveWallConfig(string wallPlacementId)
    {
        if (wallCatalog == null || wallCatalog.Length == 0)
            return null;

        string id = string.IsNullOrEmpty(wallPlacementId) ? "wooden_wall_default" : wallPlacementId;

        for (int i = 0; i < wallCatalog.Length; i++)
        {
            var w = wallCatalog[i];
            if (w == null) continue;
            if (w.wallPlacementId == id)
                return w;
        }

        return null;
    }
}
