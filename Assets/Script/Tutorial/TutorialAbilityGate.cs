using UnityEngine;

public class TutorialAbilityGate : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMovementController movement;
    public PlayerCombat2D combat;
    public PlayerInteractor2D interactor;
    public PlayerHungerThirst hungerThirst;
    public PlayerSeedPlantingController plantingController;
    public PlayerWallPlacementController wallPlacementController;

    private bool _movementEnabled = true;
    private bool _combatEnabled = true;
    private bool _interactEnabled = true;
    private bool _quickSlotEnabled = true;

    private void Awake()
    {
        ResolveRefs();
    }

    public void ApplyStepAbilities(TutorialStep step)
    {
        if (step == null) return;

        ResolveRefs();

        SetMovementEnabled(step.allowMovement);
        SetCombatEnabled(step.allowCombat);
        SetInteractEnabled(step.allowInteraction);
        SetQuickSlotEnabled(step.allowQuickSlotUse);

        if (!step.allowPlantingMode && plantingController != null && plantingController.IsPlantingModeActive)
            plantingController.CancelPlanting(false, null);

        if (!step.allowWallPlacementMode && wallPlacementController != null && wallPlacementController.IsPlacementModeActive)
            wallPlacementController.CancelPlacement(false, null);
    }

    public void SetMovementEnabled(bool enabled)
    {
        _movementEnabled = enabled;
        movement?.SetCanMove(enabled);
    }

    public void SetCombatEnabled(bool enabled)
    {
        _combatEnabled = enabled;
        combat?.SetInputEnabled(enabled);
    }

    public void SetInteractEnabled(bool enabled)
    {
        _interactEnabled = enabled;
        interactor?.SetInputEnabled(enabled);
    }

    public void SetQuickSlotEnabled(bool enabled)
    {
        _quickSlotEnabled = enabled;
        if (hungerThirst != null)
            hungerThirst.enableQuickSlotsInput = enabled;
    }

    public void RestoreDefaults()
    {
        SetMovementEnabled(true);
        SetCombatEnabled(true);
        SetInteractEnabled(true);
        SetQuickSlotEnabled(true);
    }

    /// <summary>
    /// Re-applies movement/combat/interact/quick-slot flags from a step without cancelling planting or wall mode.
    /// Use after wall placement exits so interactor state matches the tutorial step (fixes stuck input).
    /// </summary>
    public void RefreshFromStep(TutorialStep step)
    {
        if (step == null) return;
        ResolveRefs();
        SetMovementEnabled(step.allowMovement);
        SetCombatEnabled(step.allowCombat);
        SetInteractEnabled(step.allowInteraction);
        SetQuickSlotEnabled(step.allowQuickSlotUse);
    }

    private void ResolveRefs()
    {
        if (movement == null) movement = FindFirstObjectByType<PlayerMovementController>(FindObjectsInactive.Include);
        if (combat == null) combat = FindFirstObjectByType<PlayerCombat2D>(FindObjectsInactive.Include);
        if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor2D>(FindObjectsInactive.Include);
        if (hungerThirst == null) hungerThirst = FindFirstObjectByType<PlayerHungerThirst>(FindObjectsInactive.Include);
        if (plantingController == null) plantingController = FindFirstObjectByType<PlayerSeedPlantingController>(FindObjectsInactive.Include);
        if (wallPlacementController == null) wallPlacementController = FindFirstObjectByType<PlayerWallPlacementController>(FindObjectsInactive.Include);
    }
}
