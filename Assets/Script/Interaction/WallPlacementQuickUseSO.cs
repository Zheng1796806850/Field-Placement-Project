using UnityEngine;

[CreateAssetMenu(menuName = "Game/Wall Placement Quick Use Item", fileName = "WallPlacementQuickUse")]
public class WallPlacementQuickUseSO : QuickUseItemSO, IUsableItem
{
    [Header("Wall Prefabs")]
    public GameObject wallPrefab;
    public GameObject previewPrefab;

    [Header("Persistence")]
    [Tooltip("Stable id for Base <-> Town wall snapshot restore (must match an entry in BaseConstructionSnapshotApplier wall catalog).")]
    public string wallPlacementId = "wooden_wall_default";

    [Header("Build Cost")]
    [Min(0)] public int placementCost = 4;

    [Header("Build Action")]
    [Min(0.01f)] public float buildDuration = 2f;
    public bool holdToBuild = true;
    public bool restrictBuildToDay = false;
    [Min(0f)] public float maxBuildDistance = 2.5f;
    public bool lockPlayerMovementWhileBuilding = false;

    [Header("Placement Validation")]
    public LayerMask placementBlockerLayers = ~0;
    public Vector2 previewCheckSize = new Vector2(0.9f, 0.9f);
    public bool ignoreTriggerCollidersWhenValidating = true;

    [Header("Placement World Offsets")]
    public Vector3 previewWorldOffset;
    public Vector3 builtWorldOffset;

    [Header("Preview Colors")]
    public Color validPreviewColor = new Color(1f, 1f, 1f, 0.4f);
    public Color invalidPreviewColor = new Color(1f, 0.3f, 0.3f, 0.45f);

    [Header("Build Loop SFX")]
    public SfxId buildLoopSfxId = SfxId.Action_BuildLoop;

    [Header("Messages")]
    public string activateMessage = "Wall placement ready";
    public string deactivateMessage = "Wall placement cancelled";
    public string invalidPlacementMessage = "Cannot place wall here";

    bool IUsableItem.Use(UseContext context)
    {
        return Use(context);
    }

    public new bool Use(UseContext context)
    {
        var controller = ResolveController(context);
        if (controller == null)
        {
            context.pushMessage?.Invoke("No wall placement controller");
            return false;
        }

        if (!controller.IsActiveWith(this))
        {
            if (context.inventory == null)
            {
                context.pushMessage?.Invoke("No inventory");
                return false;
            }

            // Do not require full placementCost to open placement mode — only to start a build.
            // Otherwise after placing one wall the player cannot re-enter with leftover materials
            // (e.g. tutorial gives 5 planks, cost 4 → 1 left and TogglePlacement would always fail).
        }

        return controller.TogglePlacement(this, context);
    }

    private PlayerWallPlacementController ResolveController(UseContext context)
    {
        if (context.user != null)
        {
            var c = context.user.GetComponentInParent<PlayerWallPlacementController>();
            if (c != null) return c;
        }

        return Object.FindFirstObjectByType<PlayerWallPlacementController>(FindObjectsInactive.Include);
    }
}