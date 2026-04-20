using UnityEngine;

[DisallowMultipleComponent]
public class GameplayModeQuickSlotHighlightBridge : MonoBehaviour
{
    private enum ActiveMode
    {
        None = 0,
        Planting = 1,
        WallPlacement = 2
    }

    [Header("Refs")]
    public QuickSlotsHUD quickSlotsHUD;
    public PlayerHungerThirst quickSlotController;
    public PlayerSeedPlantingController seedPlantingController;
    public PlayerWallPlacementController wallPlacementController;

    [Header("Debug")]
    public bool debugLogs = false;

    private ActiveMode _activeMode = ActiveMode.None;
    private int _activeSlot = -1;

    private void Awake()
    {
        ResolveRefs();
    }

    private void OnEnable()
    {
        ResolveRefs();
        Subscribe();
        SyncFromCurrentState();
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (quickSlotsHUD != null)
            quickSlotsHUD.ClearModeHighlights();
        _activeMode = ActiveMode.None;
        _activeSlot = -1;
    }

    private void ResolveRefs()
    {
        if (quickSlotsHUD == null)
            quickSlotsHUD = FindFirstObjectByType<QuickSlotsHUD>(FindObjectsInactive.Include);
        if (quickSlotController == null)
            quickSlotController = FindFirstObjectByType<PlayerHungerThirst>(FindObjectsInactive.Include);
        if (seedPlantingController == null)
            seedPlantingController = FindFirstObjectByType<PlayerSeedPlantingController>(FindObjectsInactive.Include);
        if (wallPlacementController == null)
            wallPlacementController = FindFirstObjectByType<PlayerWallPlacementController>(FindObjectsInactive.Include);
    }

    private void Subscribe()
    {
        if (seedPlantingController != null)
        {
            seedPlantingController.OnModeEntered -= HandleSeedEntered;
            seedPlantingController.OnModeEntered += HandleSeedEntered;
            seedPlantingController.OnModeExited -= HandleSeedExited;
            seedPlantingController.OnModeExited += HandleSeedExited;
        }

        if (wallPlacementController != null)
        {
            wallPlacementController.OnModeEntered -= HandleWallEntered;
            wallPlacementController.OnModeEntered += HandleWallEntered;
            wallPlacementController.OnModeExited -= HandleWallExited;
            wallPlacementController.OnModeExited += HandleWallExited;
        }

        if (quickSlotController != null)
        {
            quickSlotController.OnQuickSlotsLayoutChanged -= HandleQuickSlotsLayoutChanged;
            quickSlotController.OnQuickSlotsLayoutChanged += HandleQuickSlotsLayoutChanged;
        }
    }

    private void Unsubscribe()
    {
        if (seedPlantingController != null)
        {
            seedPlantingController.OnModeEntered -= HandleSeedEntered;
            seedPlantingController.OnModeExited -= HandleSeedExited;
        }

        if (wallPlacementController != null)
        {
            wallPlacementController.OnModeEntered -= HandleWallEntered;
            wallPlacementController.OnModeExited -= HandleWallExited;
        }

        if (quickSlotController != null)
            quickSlotController.OnQuickSlotsLayoutChanged -= HandleQuickSlotsLayoutChanged;
    }

    private void SyncFromCurrentState()
    {
        if (wallPlacementController != null && wallPlacementController.IsPlacementModeActive)
        {
            Activate(ActiveMode.WallPlacement, wallPlacementController.ActiveSourceSlotIndex);
            ValidateActiveHighlight();
            return;
        }

        if (seedPlantingController != null && seedPlantingController.IsPlantingModeActive)
        {
            Activate(ActiveMode.Planting, seedPlantingController.ActiveSourceSlotIndex);
            ValidateActiveHighlight();
            return;
        }

        Clear();
    }

    private void HandleSeedEntered(int slotIndex, SeedPlantingQuickUseSO _)
    {
        Activate(ActiveMode.Planting, slotIndex);
        ValidateActiveHighlight();
    }

    private void HandleSeedExited(int slotIndex, string reason)
    {
        if (_activeMode != ActiveMode.Planting || _activeSlot != slotIndex)
            return;

        if (debugLogs)
            Debug.Log($"[ModeHighlightBridge] Seed mode exited. slot={slotIndex}, reason={reason}");

        if (wallPlacementController != null && wallPlacementController.IsPlacementModeActive)
        {
            Activate(ActiveMode.WallPlacement, wallPlacementController.ActiveSourceSlotIndex);
            ValidateActiveHighlight();
            return;
        }

        Clear();
    }

    private void HandleWallEntered(int slotIndex, WallPlacementQuickUseSO _)
    {
        Activate(ActiveMode.WallPlacement, slotIndex);
        ValidateActiveHighlight();
    }

    private void HandleWallExited(int slotIndex, string reason)
    {
        if (_activeMode != ActiveMode.WallPlacement || _activeSlot != slotIndex)
            return;

        if (debugLogs)
            Debug.Log($"[ModeHighlightBridge] Wall mode exited. slot={slotIndex}, reason={reason}");

        if (seedPlantingController != null && seedPlantingController.IsPlantingModeActive)
        {
            Activate(ActiveMode.Planting, seedPlantingController.ActiveSourceSlotIndex);
            ValidateActiveHighlight();
            return;
        }

        Clear();
    }

    private void HandleQuickSlotsLayoutChanged()
    {
        ValidateActiveHighlight();
    }

    private void Activate(ActiveMode mode, int slotIndex)
    {
        if (quickSlotsHUD == null)
            return;

        quickSlotsHUD.ClearModeHighlights();

        _activeMode = mode;
        _activeSlot = slotIndex;

        if (_activeSlot >= 0)
            quickSlotsHUD.SetModeHighlight(_activeSlot, true);

        if (debugLogs)
            Debug.Log($"[ModeHighlightBridge] Activate mode={_activeMode}, slot={_activeSlot}");
    }

    private void Clear()
    {
        if (quickSlotsHUD != null)
            quickSlotsHUD.ClearModeHighlights();

        _activeMode = ActiveMode.None;
        _activeSlot = -1;
    }

    private void ValidateActiveHighlight()
    {
        if (_activeMode == ActiveMode.None)
            return;

        if (quickSlotsHUD == null || quickSlotController == null || _activeSlot < 0 || _activeSlot >= quickSlotController.QuickSlotCount)
        {
            Clear();
            return;
        }

        switch (_activeMode)
        {
            case ActiveMode.Planting:
                if (!IsSeedHighlightStillValid())
                    Clear();
                break;

            case ActiveMode.WallPlacement:
                if (!IsWallHighlightStillValid())
                    Clear();
                break;
        }
    }

    private bool IsSeedHighlightStillValid()
    {
        if (seedPlantingController == null || !seedPlantingController.IsPlantingModeActive)
            return false;
        if (seedPlantingController.ActiveSourceSlotIndex != _activeSlot)
            return false;

        var expected = seedPlantingController.ActiveConfig;
        if (expected == null)
            return false;

        var slotItem = quickSlotController.GetQuickSlotItem(_activeSlot);
        return slotItem == expected;
    }

    private bool IsWallHighlightStillValid()
    {
        if (wallPlacementController == null || !wallPlacementController.IsPlacementModeActive)
            return false;
        if (wallPlacementController.ActiveSourceSlotIndex != _activeSlot)
            return false;

        var expected = wallPlacementController.ActiveConfig;
        if (expected == null)
            return false;

        var slotItem = quickSlotController.GetQuickSlotItem(_activeSlot);
        return slotItem == expected;
    }
}
