using System;
using UnityEngine;

public enum WallBuildState
{
    Built = 0,
    Rubble = 1,
    Removed = 2
}

[RequireComponent(typeof(Health))]
public class WoodenWallDurability : MonoBehaviour, IInteractable
{
    [Header("Refs")]
    public Health health;
    public WallRepairProgressHUD repairProgressHUD;
    public WallDeathHandler wallDeathHandler;
    public GameObject builtVisualRoot;
    public GameObject rubbleVisualRoot;

    [Header("Durability")]
    [Min(1)] public int wallMaxHP = 50;
    public bool overrideHealthMaxOnAwake = true;
    public bool fillToMaxOnAwake = true;
    public bool forceHealthDestroyOnDeathFalse = true;

    [Header("Damage Pipeline")]
    [Min(0.1f)] public float incomingDamageMultiplier = 1f;

    [Header("Repair")]
    public bool restrictRepairToDay = false;
    public bool holdToRepair = true;
    public int planksPerRepairStep = 1;
    public int hpRestoredPerStep = 10;
    public float holdRepairInterval = 5f;
    public float maxRepairDistance = 2.5f;
    public bool lockPlayerMovementWhileRepairing = false;

    [Header("Rebuild")]
    public bool enableRubbleRebuild = true;
    public bool restrictRebuildToDay = false;
    public bool holdToRebuild = true;
    public int planksPerRebuild = 3;
    public int rebuiltHP = 20;
    public float rebuildDuration = 3f;
    public float maxRebuildDistance = 2.5f;
    public bool lockPlayerMovementWhileRebuilding = false;
    public float rubbleLifetimeSeconds = 25f;
    public bool useUnscaledRubbleLifetime = false;

    [Header("Player Dismantle")]
    public bool allowPlayerAttackDismantle = true;
    [Min(0)] public int planksCostEquivalent = 4;
    [Range(0f, 1f)] public float playerDismantleRefundRatio = 0.5f;

    [Header("Loop SFX")]
    public bool enableRepairLoopSfx = true;
    public TimedActionLoopSfxEmitter repairLoopSfx;
    public SfxId repairLoopSfxId = SfxId.Action_RepairLoop;
    public SfxId rebuildLoopSfxId = SfxId.Action_BuildLoop;

    [Header("Low Durability Warning")]
    [Range(0f, 1f)] public float lowDurabilityThreshold01 = 0.25f;

    [Header("Interact")]
    public int priority = 8;
    public bool debugLogs = false;

    [Header("Inventory")]
    public bool autoSaveInventoryOnRepair = true;

    [Header("Runtime State")]
    [SerializeField] private WallBuildState state = WallBuildState.Built;

    public event Action<int, int> OnDurabilityChanged;
    public event Action<bool> OnLowDurabilityChanged;
    public event Action OnWallDestroyed;
    public event Action<WallBuildState> OnStateChanged;

    public int Priority => priority;
    public int CurrentHP => health != null ? health.currentHP : 0;
    public int MaxHP => health != null ? health.maxHP : wallMaxHP;
    public WallBuildState CurrentState => state;
    public bool IsBuiltState => state == WallBuildState.Built;
    public bool IsRubbleState => state == WallBuildState.Rubble;

    public bool IsLowDurability
    {
        get
        {
            if (!IsBuiltState) return false;
            if (health == null || health.maxHP <= 0) return false;
            return (health.currentHP / (float)health.maxHP) <= lowDurabilityThreshold01;
        }
    }

    private bool _lastLow;
    private float _rubbleTimer = -1f;

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        if (wallDeathHandler == null) wallDeathHandler = GetComponent<WallDeathHandler>();

        if (repairLoopSfx == null)
            repairLoopSfx = GetComponentInChildren<TimedActionLoopSfxEmitter>(true);

        if (health != null)
        {
            if (forceHealthDestroyOnDeathFalse)
                health.destroyOnDeath = false;

            if (overrideHealthMaxOnAwake)
                health.SetMaxHP(wallMaxHP, fillToMaxOnAwake);

            health.OnHealthChanged += HandleHealthChanged;
            health.OnDied += HandleDied;

            if (health.dead)
                state = enableRubbleRebuild ? WallBuildState.Rubble : WallBuildState.Removed;

            HandleHealthChanged(health.currentHP, health.maxHP);
        }

        if (repairProgressHUD == null)
            repairProgressHUD = GetComponentInChildren<WallRepairProgressHUD>(true);

        AutoWireVisualsIfNull();
        HideRepairProgress();
        ApplyStateVisuals();

        if (state == WallBuildState.Rubble)
            _rubbleTimer = rubbleLifetimeSeconds > 0f ? rubbleLifetimeSeconds : -1f;
    }

    private void OnDestroy()
    {
        StopActionLoopSfx();

        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDied -= HandleDied;
        }
    }

    private void Update()
    {
        if (state != WallBuildState.Rubble) return;
        if (rubbleLifetimeSeconds <= 0f) return;

        float dt = useUnscaledRubbleLifetime ? Time.unscaledDeltaTime : Time.deltaTime;
        _rubbleTimer -= dt;

        if (_rubbleTimer <= 0f)
            RemoveRubble();
    }

    public void SetPlacementCostEquivalent(int cost)
    {
        planksCostEquivalent = Mathf.Max(0, cost);
    }

    public void ApplyWallDamage(int amount)
    {
        if (!IsBuiltState) return;
        if (health == null || health.dead) return;
        if (amount <= 0) return;

        int final = Mathf.Max(1, Mathf.RoundToInt(amount * incomingDamageMultiplier));
        health.TakeDamage(final);

        if (debugLogs)
            Debug.Log($"[WoodenWall] ApplyWallDamage {amount} -> {final} ({name})");
    }

    public bool ApplyPlayerAttackDamage(int amount, GameObject attacker)
    {
        if (!IsBuiltState) return false;
        if (health == null || health.dead) return false;
        if (amount <= 0) return false;

        int final = Mathf.Max(1, Mathf.RoundToInt(amount * incomingDamageMultiplier));

        if (allowPlayerAttackDismantle && final >= health.currentHP)
        {
            DismantleByPlayer(attacker);
            return true;
        }

        health.TakeDamage(final);

        if (debugLogs)
            Debug.Log($"[WoodenWall] Player hit {amount} -> {final} ({name})");

        return true;
    }

    public string GetPrompt()
    {
        if (state == WallBuildState.Removed) return "";
        if (state == WallBuildState.Rubble)
        {
            if (!enableRubbleRebuild) return "Wall Rubble";
            string mode = holdToRebuild ? "Hold Rebuild" : "Rebuild";
            return $"{mode} (-{planksPerRebuild} Planks)";
        }

        if (health == null) return "Repair";
        if (health.dead) return "Wall Destroyed";
        if (health.currentHP >= health.maxHP) return "Wall (Full)";

        string repairMode = holdToRepair ? "Hold Repair" : "Repair";
        return $"{repairMode} (-{planksPerRepairStep} Planks)";
    }

    public bool CanInteract(GameObject interactor)
    {
        if (state == WallBuildState.Removed) return false;

        var inv = ResolveInventory(interactor);
        if (inv == null) return false;

        if (state == WallBuildState.Rubble)
        {
            if (!enableRubbleRebuild) return false;

            if (restrictRebuildToDay)
            {
                var gsm = GameStateManager.Instance;
                if (gsm != null && gsm.CurrentPhase != DayNightPhase.Day)
                    return false;
            }

            return inv.CanSpend(ResourceType.Planks, planksPerRebuild);
        }

        if (health == null) return false;
        if (health.dead) return false;
        if (health.currentHP >= health.maxHP) return false;

        if (restrictRepairToDay)
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null && gsm.CurrentPhase != DayNightPhase.Day)
                return false;
        }

        return inv.CanSpend(ResourceType.Planks, planksPerRepairStep);
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        if (state == WallBuildState.Rubble)
        {
            StartTimedRebuild(interactor, inv);
            return;
        }

        StartTimedRepair(interactor, inv);
    }

    private void StartTimedRepair(GameObject interactor, PlayerResourceInventory inv)
    {
        var runner = interactor != null ? interactor.GetComponentInParent<TimedActionController>() : null;
        if (runner == null)
        {
            TryRepairImmediate(inv);
            return;
        }

        if (runner.IsBusy) return;
        if (!inv.CanSpend(ResourceType.Planks, planksPerRepairStep)) return;

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
            ShowRepairProgress(0f);
            spent = inv.Spend(ResourceType.Planks, planksPerRepairStep);
            if (!spent)
            {
                runner.CancelActive();
                return;
            }

            StartActionLoopSfx(repairLoopSfxId);
        };

        req.onProgress = (p) =>
        {
            ShowRepairProgress(p);
            if (p <= 0f) StopActionLoopSfx();
        };

        req.onCancel = () =>
        {
            StopActionLoopSfx();
            HideRepairProgress();

            if (spent)
            {
                inv.Add(ResourceType.Planks, planksPerRepairStep);
                if (autoSaveInventoryOnRepair) inv.SaveInMemory();
            }
        };

        req.onComplete = () =>
        {
            StopActionLoopSfx();
            HideRepairProgress();

            if (!spent) return;

            if (state != WallBuildState.Built || health == null || health.dead)
            {
                inv.Add(ResourceType.Planks, planksPerRepairStep);
                if (autoSaveInventoryOnRepair) inv.SaveInMemory();
                return;
            }

            if (health.currentHP >= health.maxHP)
            {
                inv.Add(ResourceType.Planks, planksPerRepairStep);
                if (autoSaveInventoryOnRepair) inv.SaveInMemory();
                return;
            }

            health.Heal(hpRestoredPerStep);

            if (autoSaveInventoryOnRepair) inv.SaveInMemory();

            if (debugLogs)
                Debug.Log($"[WoodenWall] Timed Repair +{hpRestoredPerStep} => {health.currentHP}/{health.maxHP} ({name})");
        };

        runner.TryBegin(req);
    }

    private void StartTimedRebuild(GameObject interactor, PlayerResourceInventory inv)
    {
        var runner = interactor != null ? interactor.GetComponentInParent<TimedActionController>() : null;
        if (runner == null)
        {
            TryRebuildImmediate(inv);
            return;
        }

        if (runner.IsBusy) return;
        if (!inv.CanSpend(ResourceType.Planks, planksPerRebuild)) return;

        bool spent = false;
        var pi = interactor != null ? interactor.GetComponentInParent<PlayerInteractor2D>() : null;
        KeyCode holdKey = pi != null ? pi.interactKey : KeyCode.E;

        var req = new TimedActionRequest();
        req.label = "Rebuilding...";
        req.duration = Mathf.Max(0.05f, rebuildDuration);
        req.requireHold = holdToRebuild;
        req.holdKey = holdKey;
        req.cancelKey = holdToRebuild ? KeyCode.None : holdKey;
        req.suppressCancelInputFrames = holdToRebuild ? 0 : 1;
        req.lockPlayerMovement = lockPlayerMovementWhileRebuilding;
        req.target = transform;
        req.maxDistance = maxRebuildDistance;
        req.cancelIfPhaseNotDay = restrictRebuildToDay;

        req.onBegin = () =>
        {
            ShowRepairProgress(0f);
            spent = inv.Spend(ResourceType.Planks, planksPerRebuild);
            if (!spent)
            {
                runner.CancelActive();
                return;
            }

            StartActionLoopSfx(rebuildLoopSfxId);
        };

        req.onProgress = (p) =>
        {
            ShowRepairProgress(p);
            if (p <= 0f) StopActionLoopSfx();
        };

        req.onCancel = () =>
        {
            StopActionLoopSfx();
            HideRepairProgress();

            if (spent)
            {
                inv.Add(ResourceType.Planks, planksPerRebuild);
                if (autoSaveInventoryOnRepair) inv.SaveInMemory();
            }
        };

        req.onComplete = () =>
        {
            StopActionLoopSfx();
            HideRepairProgress();

            if (!spent) return;

            if (state != WallBuildState.Rubble)
            {
                inv.Add(ResourceType.Planks, planksPerRebuild);
                if (autoSaveInventoryOnRepair) inv.SaveInMemory();
                return;
            }

            RebuildFromRubble();

            if (autoSaveInventoryOnRepair) inv.SaveInMemory();

            if (debugLogs)
                Debug.Log($"[WoodenWall] Rebuilt => {health.currentHP}/{health.maxHP} ({name})");
        };

        runner.TryBegin(req);
    }

    private void TryRepairImmediate(PlayerResourceInventory inv)
    {
        if (inv == null) return;
        if (!IsBuiltState) return;
        if (health == null || health.dead) return;
        if (health.currentHP >= health.maxHP) return;
        if (!inv.Spend(ResourceType.Planks, planksPerRepairStep)) return;

        health.Heal(hpRestoredPerStep);

        if (autoSaveInventoryOnRepair) inv.SaveInMemory();

        if (debugLogs)
            Debug.Log($"[WoodenWall] Immediate Repair +{hpRestoredPerStep} => {health.currentHP}/{health.maxHP} ({name})");
    }

    private void TryRebuildImmediate(PlayerResourceInventory inv)
    {
        if (inv == null) return;
        if (state != WallBuildState.Rubble) return;
        if (!inv.Spend(ResourceType.Planks, planksPerRebuild)) return;

        RebuildFromRubble();

        if (autoSaveInventoryOnRepair) inv.SaveInMemory();

        if (debugLogs)
            Debug.Log($"[WoodenWall] Immediate Rebuild => {health.currentHP}/{health.maxHP} ({name})");
    }

    private void RebuildFromRubble()
    {
        state = WallBuildState.Built;
        _rubbleTimer = -1f;

        ApplyStateVisuals();

        if (wallDeathHandler != null)
            wallDeathHandler.RestoreBlockingState();

        if (health != null)
            health.Revive(GetRebuiltHP());

        OnStateChanged?.Invoke(state);
    }

    private int GetRebuiltHP()
    {
        int max = health != null ? Mathf.Max(1, health.maxHP) : Mathf.Max(1, wallMaxHP);
        if (rebuiltHP <= 0) return max;
        return Mathf.Clamp(rebuiltHP, 1, max);
    }

    private void DismantleByPlayer(GameObject attacker)
    {
        StopActionLoopSfx();
        HideRepairProgress();

        if (wallDeathHandler != null)
            wallDeathHandler.EnterDestroyedState();

        int refund = GetPlayerDismantleRefund();
        var inv = ResolveInventory(attacker);

        if (refund > 0 && inv != null)
        {
            inv.Add(ResourceType.Planks, refund);
            inv.PushMessage($"Recovered Planks x{refund}");

            if (autoSaveInventoryOnRepair)
                inv.SaveInMemory();
        }

        state = WallBuildState.Removed;
        OnWallDestroyed?.Invoke();
        OnStateChanged?.Invoke(state);

        if (debugLogs)
            Debug.Log($"[WoodenWall] Dismantled by player. Refund={refund} ({name})");

        Destroy(gameObject);
    }

    private int GetPlayerDismantleRefund()
    {
        int baseCost = Mathf.Max(0, planksCostEquivalent);
        float ratio = Mathf.Clamp01(playerDismantleRefundRatio);
        return Mathf.Max(0, Mathf.FloorToInt(baseCost * ratio));
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

        bool low = IsBuiltState && (max > 0) && ((current / (float)max) <= lowDurabilityThreshold01);
        if (low != _lastLow)
        {
            _lastLow = low;
            OnLowDurabilityChanged?.Invoke(low);
        }
    }

    private void HandleDied()
    {
        StopActionLoopSfx();
        HideRepairProgress();

        if (state != WallBuildState.Built) return;

        if (enableRubbleRebuild)
        {
            EnterRubbleState();
        }
        else
        {
            state = WallBuildState.Removed;
            ApplyStateVisuals();
            OnWallDestroyed?.Invoke();
            OnStateChanged?.Invoke(state);
            Destroy(gameObject);
        }
    }

    private void EnterRubbleState()
    {
        state = WallBuildState.Rubble;
        _rubbleTimer = rubbleLifetimeSeconds > 0f ? rubbleLifetimeSeconds : -1f;

        ApplyStateVisuals();

        OnWallDestroyed?.Invoke();
        OnStateChanged?.Invoke(state);

        if (debugLogs)
            Debug.Log($"[WoodenWall] Enter rubble ({name})");
    }

    private void RemoveRubble()
    {
        if (state != WallBuildState.Rubble) return;

        state = WallBuildState.Removed;
        ApplyStateVisuals();
        OnStateChanged?.Invoke(state);

        if (debugLogs)
            Debug.Log($"[WoodenWall] Rubble expired ({name})");

        Destroy(gameObject);
    }

    private void StartActionLoopSfx(SfxId id)
    {
        if (!enableRepairLoopSfx) return;
        if (repairLoopSfx == null) return;
        repairLoopSfx.PlayLoop(id);
    }

    private void StopActionLoopSfx()
    {
        if (repairLoopSfx == null) return;
        repairLoopSfx.StopLoop();
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

    private void ApplyStateVisuals()
    {
        if (builtVisualRoot != null)
            builtVisualRoot.SetActive(state == WallBuildState.Built);

        if (rubbleVisualRoot != null)
            rubbleVisualRoot.SetActive(state == WallBuildState.Rubble);
    }

    private void AutoWireVisualsIfNull()
    {
        if (builtVisualRoot == null) builtVisualRoot = transform.Find("BuiltVisual")?.gameObject;
        if (rubbleVisualRoot == null) rubbleVisualRoot = transform.Find("RubbleVisual")?.gameObject;
    }
}