using UnityEngine;

public class HouseCoreRepairInteractable : MonoBehaviour, IInteractable
{
    [Header("Refs")]
    public HouseObjective house;
    public Health coreHealth;

    [Header("Inventory")]
    public PlayerResourceInventory inventoryOverride;
    public bool autoSaveInventoryOnRepair = true;

    [Header("Repair")]
    public bool restrictRepairToDay = false;
    public bool holdToRepair = true;
    [Min(0)] public int planksPerRepairStep = 1;
    [Min(1)] public int hpRestoredPerStep = 10;
    [Min(0.05f)] public float holdRepairDuration = 2f;
    [Min(0f)] public float maxRepairDistance = 2.5f;
    public bool lockPlayerMovementWhileRepairing = false;

    [Header("Quest")]
    [Tooltip("Passed to GameplayEventHub for Repair objectives (HP restored per successful step).")]
    public string questRepairTargetId = "house_core";

    [Header("Progress HUD (Optional)")]
    public WallRepairProgressHUD repairProgressHUD;

    [Header("TimedAction Runner (Optional)")]
    public TimedActionController timedActionOverride;

    [Header("Interaction")]
    [TextArea] public string promptText = "Hold Repair House (-1 Planks)";
    public int priority = 100;
    public int Priority => priority;

    public string GetPrompt()
    {
        if (!CanOfferRepair()) return "";
        int cost = Mathf.Max(0, planksPerRepairStep);
        string mode = holdToRepair ? "Hold Repair" : "Repair";
        return string.IsNullOrWhiteSpace(promptText)
            ? $"{mode} House (-{cost} Planks)"
            : promptText.Replace("{mode}", mode).Replace("{cost}", cost.ToString());
    }

    public bool CanInteract(GameObject interactor)
    {
        if (!CanOfferRepair()) return false;

        var inv = ResolveInventory(interactor);
        if (inv == null) return false;

        if (restrictRepairToDay)
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null && gsm.CurrentPhase != DayNightPhase.Day)
                return false;
        }

        int cost = Mathf.Max(0, planksPerRepairStep);
        return inv.CanSpend(ResourceType.Planks, cost);
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        var runner = ResolveTimedActionRunner(interactor);
        if (runner == null)
        {
            TryRepairImmediate(inv);
            return;
        }

        if (runner.IsBusy) return;

        StartTimedRepair(interactor, inv, runner);
    }

    private bool CanOfferRepair()
    {
        ResolveHouseAndHealth();

        if (coreHealth == null) return false;
        if (coreHealth.dead) return false;
        if (coreHealth.currentHP >= coreHealth.maxHP) return false; 
        if (planksPerRepairStep <= 0) return false;
        if (hpRestoredPerStep <= 0) return false;
        return true;
    }

    private void ResolveHouseAndHealth()
    {
        if (house == null)
            house = HouseObjective.Instance != null ? HouseObjective.Instance : FindFirstObjectByType<HouseObjective>(FindObjectsInactive.Include);

        if (coreHealth == null && house != null)
            coreHealth = house.coreHealth;
    }

    private PlayerResourceInventory ResolveInventory(GameObject interactor)
    {
        if (inventoryOverride != null) return inventoryOverride;

        var inv = interactor != null ? interactor.GetComponentInParent<PlayerResourceInventory>() : null;
        if (inv != null) return inv;

        return PlayerResourceInventory.Instance;
    }

    private TimedActionController ResolveTimedActionRunner(GameObject interactor)
    {
        if (timedActionOverride != null) return timedActionOverride;
        return interactor != null ? interactor.GetComponentInParent<TimedActionController>() : null;
    }

    private void StartTimedRepair(GameObject interactor, PlayerResourceInventory inv, TimedActionController runner)
    {
        if (coreHealth == null || coreHealth.dead) return;
        if (coreHealth.currentHP >= coreHealth.maxHP) return;

        int cost = Mathf.Max(0, planksPerRepairStep);
        if (!inv.CanSpend(ResourceType.Planks, cost)) return;

        bool spent = false;

        var pi = interactor != null ? interactor.GetComponentInParent<PlayerInteractor2D>() : null;
        KeyCode holdKey = pi != null ? pi.interactKey : KeyCode.E;

        var req = new TimedActionRequest();
        req.label = "Repairing Core...";
        req.duration = Mathf.Max(0.05f, holdRepairDuration);
        req.requireHold = holdToRepair;
        req.holdKey = holdKey;
        req.lockPlayerMovement = lockPlayerMovementWhileRepairing;
        req.target = transform;
        req.maxDistance = maxRepairDistance;
        req.cancelIfPhaseNotDay = restrictRepairToDay;

        req.onBegin = () =>
        {
            ShowRepairProgress(0f);
            spent = inv.Spend(ResourceType.Planks, cost);
            if (!spent)
            {
                runner.CancelActive();
                return;
            }
        };

        req.onProgress = (p) =>
        {
            ShowRepairProgress(p);
            if (p <= 0f) HideRepairProgress();
        };

        req.onCancel = () =>
        {
            HideRepairProgress();

            if (spent)
            {
                inv.Add(ResourceType.Planks, cost);
                if (autoSaveInventoryOnRepair) inv.SaveInMemory();
            }
        };

        req.onComplete = () =>
        {
            HideRepairProgress();
            if (!spent) return;

            ResolveHouseAndHealth();

            if (coreHealth == null || coreHealth.dead)
            {
                inv.Add(ResourceType.Planks, cost);
                if (autoSaveInventoryOnRepair) inv.SaveInMemory();
                return;
            }

            if (coreHealth.currentHP >= coreHealth.maxHP)
            {
                inv.Add(ResourceType.Planks, cost);
                if (autoSaveInventoryOnRepair) inv.SaveInMemory();
                return;
            }

            coreHealth.Heal(hpRestoredPerStep);
            EmitQuestRepair(hpRestoredPerStep);

            if (autoSaveInventoryOnRepair) inv.SaveInMemory();
        };

        runner.TryBegin(req);
    }

    private void TryRepairImmediate(PlayerResourceInventory inv)
    {
        ResolveHouseAndHealth();
        if (coreHealth == null || coreHealth.dead) return;
        if (coreHealth.currentHP >= coreHealth.maxHP) return;

        if (restrictRepairToDay)
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null && gsm.CurrentPhase != DayNightPhase.Day)
                return;
        }

        int cost = Mathf.Max(0, planksPerRepairStep);
        if (!inv.Spend(ResourceType.Planks, cost)) return;

        coreHealth.Heal(hpRestoredPerStep);
        EmitQuestRepair(hpRestoredPerStep);

        if (autoSaveInventoryOnRepair) inv.SaveInMemory();
    }

    private void EmitQuestRepair(int hpRestored)
    {
        if (hpRestored <= 0) return;
        if (string.IsNullOrEmpty(questRepairTargetId)) return;

        GameplayEventHub.RaiseStructureRepaired(questRepairTargetId, hpRestored);
    }

    private void ShowRepairProgress(float p)
    {
        if (repairProgressHUD == null) return;
        repairProgressHUD.SetVisible(true);
        repairProgressHUD.SetProgress(p);
    }

    private void HideRepairProgress()
    {
        if (repairProgressHUD == null) return;
        repairProgressHUD.SetProgress(0f);
        repairProgressHUD.SetVisible(false);
    }
}

