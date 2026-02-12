using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class WoodenWallDurability : MonoBehaviour, IInteractable
{
    [Header("Refs")]
    public Health health;
    public WallRepairProgressHUD repairProgressHUD;

    [Header("Durability")]
    [Min(1)] public int wallMaxHP = 50;
    public bool overrideHealthMaxOnAwake = true;
    public bool fillToMaxOnAwake = true;

    [Header("Damage Pipeline (optional)")]
    [Min(0.1f)] public float incomingDamageMultiplier = 1f;

    [Header("Repair")]
    public bool restrictRepairToDay = false;
    public bool holdToRepair = true;

    public int planksPerRepairStep = 1;
    public int hpRestoredPerStep = 10;
    public float holdRepairInterval = 5f;
    public float maxRepairDistance = 2.5f;

    public bool lockPlayerMovementWhileRepairing = false;

    [Header("Low Durability Warning")]
    [Range(0f, 1f)] public float lowDurabilityThreshold01 = 0.25f;

    [Header("Interact")]
    public int priority = 8;
    public bool debugLogs = false;

    [Header("Inventory")]
    public bool autoSaveInventoryOnRepair = true;

    public event Action<int, int> OnDurabilityChanged;
    public event Action<bool> OnLowDurabilityChanged;
    public event Action OnWallDestroyed;

    public int Priority => priority;

    public int CurrentHP => health != null ? health.currentHP : 0;
    public int MaxHP => health != null ? health.maxHP : wallMaxHP;

    public bool IsLowDurability
    {
        get
        {
            if (health == null || health.maxHP <= 0) return false;
            return (health.currentHP / (float)health.maxHP) <= lowDurabilityThreshold01;
        }
    }

    private bool _lastLow;
    private Coroutine _repairCo;

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();

        if (health != null)
        {
            if (overrideHealthMaxOnAwake)
                health.SetMaxHP(wallMaxHP, fillToMaxOnAwake);

            health.OnHealthChanged += HandleHealthChanged;
            health.OnDied += HandleDied;

            HandleHealthChanged(health.currentHP, health.maxHP);
        }

        if (repairProgressHUD == null)
            repairProgressHUD = GetComponentInChildren<WallRepairProgressHUD>(true);

        if (repairProgressHUD != null)
        {
            repairProgressHUD.SetVisible(false);
            repairProgressHUD.SetProgress(0f);
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDied -= HandleDied;
        }
    }

    public void ApplyWallDamage(int amount)
    {
        if (health == null || health.dead) return;
        if (amount <= 0) return;

        int final = Mathf.Max(1, Mathf.RoundToInt(amount * incomingDamageMultiplier));
        health.TakeDamage(final);

        if (debugLogs)
            Debug.Log($"[WoodenWall] ApplyWallDamage {amount} -> {final} ({name})");
    }

    public string GetPrompt()
    {
        if (health == null) return "Repair";
        if (health.dead) return "Wall Destroyed";
        if (health.currentHP >= health.maxHP) return "Wall (Full)";

        string mode = holdToRepair ? "Hold Repair" : "Repair";
        return $"{mode} (-{planksPerRepairStep} Planks)";
    }

    public bool CanInteract(GameObject interactor)
    {
        if (health == null) return false;
        if (health.dead) return false;
        if (health.currentHP >= health.maxHP) return false;

        if (restrictRepairToDay)
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null && gsm.CurrentPhase != DayNightPhase.Day)
                return false;
        }

        var inv = ResolveInventory(interactor);
        if (inv == null) return false;

        return inv.CanSpend(ResourceType.Planks, planksPerRepairStep);
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        var runner = interactor != null ? interactor.GetComponentInParent<TimedActionController>() : null;
        if (runner == null)
        {
            TryRepairImmediate(inv);
            return;
        }

        if (runner.IsBusy) return;

        if (!inv.CanSpend(ResourceType.Planks, planksPerRepairStep))
            return;

        bool spent = false;
        var pi = interactor != null ? interactor.GetComponentInParent<PlayerInteractor2D>() : null;
        KeyCode holdKey = pi != null ? pi.interactKey : KeyCode.E;

        var req = new TimedActionRequest();
        req.label = "Repairing...";
        req.duration = Mathf.Max(0.05f, holdRepairInterval);
        req.requireHold = holdToRepair;
        req.holdKey = holdKey;
        req.lockPlayerMovement = lockPlayerMovementWhileRepairing;
        req.target = transform;
        req.maxDistance = maxRepairDistance;
        req.cancelIfPhaseNotDay = restrictRepairToDay;

        req.onBegin = () =>
        {
            if (repairProgressHUD != null)
            {
                repairProgressHUD.SetVisible(true);
                repairProgressHUD.SetProgress(0f);
            }

            spent = inv.Spend(ResourceType.Planks, planksPerRepairStep);
            if (!spent)
                runner.CancelActive();
        };

        req.onProgress = (p) =>
        {
            if (repairProgressHUD != null)
            {
                repairProgressHUD.SetVisible(true);
                repairProgressHUD.SetProgress(p);
            }
        };

        req.onCancel = () =>
        {
            if (repairProgressHUD != null)
            {
                repairProgressHUD.SetProgress(0f);
                repairProgressHUD.SetVisible(false);
            }

            if (spent)
            {
                inv.Add(ResourceType.Planks, planksPerRepairStep);
                if (autoSaveInventoryOnRepair) inv.SaveInMemory();
            }
        };

        req.onComplete = () =>
        {
            if (repairProgressHUD != null)
            {
                repairProgressHUD.SetProgress(0f);
                repairProgressHUD.SetVisible(false);
            }

            if (health == null || health.dead) return;
            if (health.currentHP >= health.maxHP)
            {
                if (spent)
                {
                    inv.Add(ResourceType.Planks, planksPerRepairStep);
                    if (autoSaveInventoryOnRepair) inv.SaveInMemory();
                }
                return;
            }

            health.Heal(hpRestoredPerStep);
            if (autoSaveInventoryOnRepair) inv.SaveInMemory();

            if (debugLogs)
                Debug.Log($"[WoodenWall] Timed Repair +{hpRestoredPerStep} => {health.currentHP}/{health.maxHP} ({name})");
        };

        runner.TryBegin(req);
    }

    private void TryRepairImmediate(PlayerResourceInventory inv)
    {
        if (inv == null) return;
        if (!inv.Spend(ResourceType.Planks, planksPerRepairStep)) return;

        health.Heal(hpRestoredPerStep);
        if (autoSaveInventoryOnRepair) inv.SaveInMemory();

        if (debugLogs)
            Debug.Log($"[WoodenWall] Immediate Repair +{hpRestoredPerStep} => {health.currentHP}/{health.maxHP} ({name})");
    }

    private PlayerResourceInventory ResolveInventory(GameObject interactor)
    {
        var inv = interactor != null ? interactor.GetComponentInParent<PlayerResourceInventory>() : null;
        if (inv != null) return inv;
        return PlayerResourceInventory.Instance;
    }

    private void HandleHealthChanged(int current, int max)
    {
        OnDurabilityChanged?.Invoke(current, max);

        bool low = (max > 0) && ((current / (float)max) <= lowDurabilityThreshold01);
        if (low != _lastLow)
        {
            _lastLow = low;
            OnLowDurabilityChanged?.Invoke(low);
        }
    }

    private void HandleDied()
    {
        OnWallDestroyed?.Invoke();
    }
}
