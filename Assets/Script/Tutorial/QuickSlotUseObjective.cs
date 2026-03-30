using System.Collections.Generic;
using UnityEngine;

public class QuickSlotUseObjective : TutorialObjective
{
    public PlayerHungerThirst hungerThirst;
    public PlayerSeedPlantingController plantingController;
    public PlayerWallPlacementController wallPlacementController;

    [Header("Quick Slot Use")]
    [Min(1)] public int requiredUseCount = 1;
    public List<int> requiredSlots = new List<int>();

    [Header("Mode Toggle Requirements")]
    public bool requirePlantingModeEnterAndExit = false;
    public bool requireWallPlacementEnterAndExit = false;

    private int _usedCount;
    private bool _plantEntered;
    private bool _plantExited;
    private bool _wallEntered;
    private bool _wallExited;
    private bool _lastPlantActive;
    private bool _lastWallActive;

    protected override void OnBegin()
    {
        if (hungerThirst == null)
            hungerThirst = FindFirstObjectByType<PlayerHungerThirst>(FindObjectsInactive.Include);

        if (plantingController == null)
            plantingController = FindFirstObjectByType<PlayerSeedPlantingController>(FindObjectsInactive.Include);

        if (wallPlacementController == null)
            wallPlacementController = FindFirstObjectByType<PlayerWallPlacementController>(FindObjectsInactive.Include);

        _usedCount = 0;
        _plantEntered = _plantExited = false;
        _wallEntered = _wallExited = false;
        _lastPlantActive = plantingController != null && plantingController.IsPlantingModeActive;
        _lastWallActive = wallPlacementController != null && wallPlacementController.IsPlacementModeActive;

        if (hungerThirst != null)
        {
            hungerThirst.OnQuickSlotUsed -= HandleQuickSlotUsed;
            hungerThirst.OnQuickSlotUsed += HandleQuickSlotUsed;
        }
    }

    protected override void OnEnd()
    {
        if (hungerThirst != null)
            hungerThirst.OnQuickSlotUsed -= HandleQuickSlotUsed;
    }

    private void Update()
    {
        bool plantNow = plantingController != null && plantingController.IsPlantingModeActive;
        if (!_lastPlantActive && plantNow) _plantEntered = true;
        if (_lastPlantActive && !plantNow && _plantEntered) _plantExited = true;
        _lastPlantActive = plantNow;

        bool wallNow = wallPlacementController != null && wallPlacementController.IsPlacementModeActive;
        if (!_lastWallActive && wallNow) _wallEntered = true;
        if (_lastWallActive && !wallNow && _wallEntered) _wallExited = true;
        _lastWallActive = wallNow;

        TryComplete();
    }

    private void HandleQuickSlotUsed(int slotIndex)
    {
        if (requiredSlots != null && requiredSlots.Count > 0)
        {
            if (requiredSlots.Contains(slotIndex))
                _usedCount++;
        }
        else
        {
            _usedCount++;
        }

        TryComplete();
    }

    private void TryComplete()
    {
        if (_usedCount < requiredUseCount)
            return;

        if (requirePlantingModeEnterAndExit && !(_plantEntered && _plantExited))
            return;

        if (requireWallPlacementEnterAndExit && !(_wallEntered && _wallExited))
            return;

        Complete();
    }

    public override string GetProgressText()
    {
        string text = $"Use quick slots {_usedCount}/{requiredUseCount}";

        if (requirePlantingModeEnterAndExit)
            text += $" | Plant mode in/out: {(_plantEntered && _plantExited ? "Done" : "Pending")}";

        if (requireWallPlacementEnterAndExit)
            text += $" | Wall mode in/out: {(_wallEntered && _wallExited ? "Done" : "Pending")}";

        return text;
    }
}
