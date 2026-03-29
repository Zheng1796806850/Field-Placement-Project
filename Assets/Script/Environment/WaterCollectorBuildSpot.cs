using System;
using System.Globalization;
using UnityEngine;

public class WaterCollectorBuildSpot : MonoBehaviour, IInteractable
{
    public enum ProductionMode
    {
        RealTimeSeconds = 0,
        PhaseTick = 1
    }

    public enum PhaseTrigger
    {
        OnDayStarted = 0,
        OnNightStarted = 1
    }

    public enum DurabilityDecayMode
    {
        None = 0,
        ByNightStarted = 1,
        ByTimeSeconds = 2,
        ByWaveStarted = 3
    }

    [Header("Build Requirements")]
    public bool requireBuild = true;
    public int planksCost = 10;

    [Header("Timed Build Settings")]
    public float buildDuration = 4f;
    public bool buildHoldToComplete = true;
    public float maxBuildDistance = 2.5f;
    public bool lockPlayerMovementWhileBuilding = false;

    [Header("Build Loop SFX")]
    public bool enableBuildLoopSfx = true;
    public TimedActionLoopSfxEmitter buildLoopSfx;
    public SfxId buildLoopSfxId = SfxId.Action_BuildLoop;

    [Header("Production Settings")]
    public ProductionMode productionMode = ProductionMode.RealTimeSeconds;
    public float secondsPerWater = 30f;
    public PhaseTrigger phaseTrigger = PhaseTrigger.OnNightStarted;
    public bool useUnscaledTime = false;
    public int waterPerProduction = 1;

    [Header("Storage & Collection Settings")]
    public int storageCap = 3;
    public bool collectAllAtOnce = true;
    public int collectAmountPerInteract = 1;
    public bool prioritizeRepairOverCollect = true;

    [Header("Durability")]
    public int maxDurability = 100;
    [Range(0f, 1f)] public float lowDurabilityThreshold01 = 0.25f;
    public bool stopProductionWhenBroken = true;

    [Header("Durability Decay")]
    public DurabilityDecayMode durabilityDecayMode = DurabilityDecayMode.ByNightStarted;
    public int durabilityDecayPerTrigger = 10;
    public float secondsPerDurabilityDecay = 60f;
    public bool useUnscaledDurabilityTime = false;

    [Header("Repair")]
    public bool restrictRepairToDay = false;
    public bool holdToRepair = true;
    public int planksPerRepairStep = 1;
    public int durabilityRestoredPerStep = 25;
    public float holdRepairInterval = 2f;
    public float maxRepairDistance = 2.5f;
    public bool lockPlayerMovementWhileRepairing = false;

    [Header("Repair Loop SFX")]
    public bool enableRepairLoopSfx = true;
    public TimedActionLoopSfxEmitter repairLoopSfx;
    public SfxId repairLoopSfxId = SfxId.Action_RepairLoop;

    [Header("Repair Progress HUD")]
    public WallRepairProgressHUD repairProgressHUD;

    [Header("Runtime State (Serialized)")]
    [SerializeField] private bool isBuilt = false;
    [SerializeField] private int storedWater = 0;
    [SerializeField] private int currentDurability = 0;

    [Header("Interaction Settings")]
    public int priority = 6;
    public bool debugLogs = false;

    [Header("Visual References")]
    public GameObject unbuiltVisual;
    public GameObject builtVisual;
    public GameObject builtEmptyVisual;
    public GameObject builtHasWaterVisual;

    [Header("Save Settings")]
    public bool autoSaveInventoryOnChange = true;
    [Tooltip("Local key; actual PlayerPrefs key is scoped per run via BaseWorldSession (see BaseWorldSession / MainMenu advance run).")]
    public string collectorSaveKey = "";

    public event Action<bool> OnBuiltChanged;
    public event Action<int, int> OnStoredWaterChanged;
    public event Action<int> OnWaterCollected;
    public event Action<int> OnWaterProduced;
    public event Action<int, int> OnDurabilityChanged;
    public event Action<bool> OnLowDurabilityChanged;

    private float _secTimer = 0f;
    private float _durabilityTimer = 0f;

    private GameStateManager _gsm;
    private WaveProgressTracker _waveProgress;
    private bool _phaseSubscribed;
    private bool _waveSubscribed;
    private float _nextRetryTime;
    private bool _lastLowDurability;

    public int Priority => priority;
    public bool IsBuilt => isBuilt;
    public int StoredWater => storedWater;
    public int StorageCap => storageCap;
    public int CurrentDurability => currentDurability;
    public int MaxDurability => Mathf.Max(1, maxDurability);
    public bool IsBroken => isBuilt && currentDurability <= 0;
    public bool NeedsRepair => isBuilt && currentDurability < MaxDurability;
    public bool IsLowDurability => isBuilt && currentDurability > 0 && (currentDurability / (float)MaxDurability) <= lowDurabilityThreshold01;

    private void Awake()
    {
        if (!requireBuild)
            isBuilt = true;

        if (buildLoopSfx == null)
            buildLoopSfx = GetComponentInChildren<TimedActionLoopSfxEmitter>(true);

        if (repairLoopSfx == null)
            repairLoopSfx = GetComponentInChildren<TimedActionLoopSfxEmitter>(true);

        if (repairProgressHUD == null)
            repairProgressHUD = GetComponentInChildren<WallRepairProgressHUD>(true);

        bool loadedLegacySave = false;
        bool loadedAnySave = false;

        if (!string.IsNullOrWhiteSpace(collectorSaveKey))
            loadedAnySave = TryLoadCollectorStateFromPrefs(out loadedLegacySave);

        InitializeRuntimeState(loadedAnySave, loadedLegacySave);
        ClampRuntimeAndBroadcast();
        HideRepairProgress();
        ApplyVisuals();
    }

    private void OnEnable()
    {
        EnsureSubscribedIfNeeded();
    }

    private void Start()
    {
        EnsureSubscribedIfNeeded();
    }

    private void Update()
    {
        if ((NeedsPhaseSubscription() && !_phaseSubscribed) || (NeedsWaveSubscription() && !_waveSubscribed))
        {
            if (Time.unscaledTime >= _nextRetryTime)
            {
                _nextRetryTime = Time.unscaledTime + 0.5f;
                EnsureSubscribedIfNeeded();
            }
        }

        UpdateDurabilityDecayByTime();
        UpdateProductionByTime();
    }

    private void OnDisable()
    {
        StopBuildLoopSfx();
        StopRepairLoopSfx();
        HideRepairProgress();
        Unsubscribe();
    }

    public string GetPrompt()
    {
        if (!isBuilt)
            return planksCost <= 0 ? "Build Water Collector" : $"Build Water Collector (-{planksCost} Planks)";

        if (prioritizeRepairOverCollect && CanOfferRepairPrompt())
        {
            string mode = holdToRepair ? "Hold Repair" : "Repair";
            return $"{mode} Water Collector (-{planksPerRepairStep} Planks)";
        }

        if (storedWater > 0)
            return $"Collect Water (+{storedWater})";

        if (CanOfferRepairPrompt())
        {
            string mode = holdToRepair ? "Hold Repair" : "Repair";
            return $"{mode} Water Collector (-{planksPerRepairStep} Planks)";
        }

        if (IsBroken)
            return "Water Collector (Broken)";

        return $"Water Collector ({storedWater}/{storageCap})";
    }

    public bool CanInteract(GameObject interactor)
    {
        if (!isBuilt)
        {
            var inv = ResolveInventory(interactor);
            if (inv == null) return false;
            return inv.CanSpend(ResourceType.Planks, planksCost);
        }

        if (prioritizeRepairOverCollect)
        {
            if (CanRepair(interactor))
                return true;

            return CanCollect(interactor);
        }

        if (CanCollect(interactor))
            return true;

        return CanRepair(interactor);
    }

    public void Interact(GameObject interactor)
    {
        if (!isBuilt)
        {
            StartTimedBuild(interactor);
            return;
        }

        if (prioritizeRepairOverCollect)
        {
            if (CanRepair(interactor))
            {
                StartTimedRepair(interactor);
                return;
            }

            if (CanCollect(interactor))
            {
                TryCollect(interactor);
                return;
            }

            return;
        }

        if (CanCollect(interactor))
        {
            TryCollect(interactor);
            return;
        }

        if (CanRepair(interactor))
            StartTimedRepair(interactor);
    }

    public float GetNextTickSecondsRemaining()
    {
        if (!isBuilt) return -1f;
        if (IsStorageFull()) return -1f;
        if (stopProductionWhenBroken && IsBroken) return -1f;

        if (productionMode == ProductionMode.RealTimeSeconds)
            return Mathf.Max(0f, Mathf.Max(0.01f, secondsPerWater) - _secTimer);

        var gsm = ResolveGameStateManager();
        if (gsm == null) return -1f;

        bool targetDay = phaseTrigger == PhaseTrigger.OnDayStarted;
        bool currentlyTargetPhase = gsm.CurrentPhase == (targetDay ? DayNightPhase.Day : DayNightPhase.Night);

        if (currentlyTargetPhase)
        {
            float otherDuration = targetDay ? gsm.nightDuration : gsm.dayDuration;
            return Mathf.Max(0f, gsm.PhaseTimeRemaining + otherDuration);
        }

        return Mathf.Max(0f, gsm.PhaseTimeRemaining);
    }

    public string GetNextTickText()
    {
        if (!isBuilt) return "Unbuilt";
        if (IsStorageFull()) return "Full";
        if (stopProductionWhenBroken && IsBroken) return "Broken";

        if (productionMode == ProductionMode.RealTimeSeconds)
            return FormatTimeShort(GetNextTickSecondsRemaining());

        string phaseName = phaseTrigger == PhaseTrigger.OnDayStarted ? "Day" : "Night";
        float remaining = GetNextTickSecondsRemaining();
        if (remaining < 0f) return phaseName;
        return $"{phaseName} {FormatTimeShort(remaining)}";
    }

    public void ForceSetBuilt(bool built)
    {
        isBuilt = built;
        if (isBuilt && currentDurability <= 0)
            currentDurability = MaxDurability;
        if (!isBuilt)
            currentDurability = 0;

        ApplyVisuals();
        OnBuiltChanged?.Invoke(isBuilt);
        BroadcastDurability();
        SaveCollectorStateIfEnabled();
    }

    public void ForceSetStoredWater(int amount)
    {
        storedWater = Mathf.Clamp(amount, 0, Mathf.Max(1, storageCap));
        ApplyVisuals();
        OnStoredWaterChanged?.Invoke(storedWater, storageCap);
        SaveCollectorStateIfEnabled();
    }

    public void ForceSetDurability(int amount)
    {
        currentDurability = Mathf.Clamp(amount, 0, MaxDurability);
        BroadcastDurability();
        SaveCollectorStateIfEnabled();
    }

    public void ForcePersistRuntimeState()
    {
        SaveCollectorStateIfEnabled();
    }

    private void StartTimedBuild(GameObject interactor)
    {
        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        if (!inv.CanSpend(ResourceType.Planks, planksCost)) return;

        var runner = interactor != null ? interactor.GetComponentInParent<TimedActionController>() : null;
        if (runner == null)
        {
            TryBuildImmediate(inv);
            return;
        }

        if (runner.IsBusy) return;

        bool spent = false;
        var pi = interactor != null ? interactor.GetComponentInParent<PlayerInteractor2D>() : null;
        KeyCode holdKey = pi != null ? pi.interactKey : KeyCode.E;

        var req = new TimedActionRequest();
        req.label = "Building...";
        req.duration = Mathf.Max(0.05f, buildDuration);
        req.requireHold = buildHoldToComplete;
        req.holdKey = holdKey;
        req.lockPlayerMovement = lockPlayerMovementWhileBuilding;
        req.target = transform;
        req.maxDistance = maxBuildDistance;
        req.cancelIfPhaseNotDay = false;

        req.onBegin = () =>
        {
            spent = inv.Spend(ResourceType.Planks, planksCost);
            if (!spent)
            {
                runner.CancelActive();
                return;
            }

            StartBuildLoopSfx();
        };

        req.onProgress = (p) =>
        {
            if (p <= 0f) StopBuildLoopSfx();
        };

        req.onCancel = () =>
        {
            StopBuildLoopSfx();

            if (spent)
            {
                inv.Add(ResourceType.Planks, planksCost);
                if (autoSaveInventoryOnChange) inv.SaveInMemory();
            }
        };

        req.onComplete = () =>
        {
            StopBuildLoopSfx();

            if (!spent) return;

            isBuilt = true;
            currentDurability = MaxDurability;
            _secTimer = 0f;
            _durabilityTimer = 0f;

            ApplyVisuals();
            OnBuiltChanged?.Invoke(true);
            BroadcastDurability();

            if (autoSaveInventoryOnChange)
                inv.SaveInMemory();

            SaveCollectorStateIfEnabled();

            if (debugLogs)
                Debug.Log($"[WaterCollector] Built on {name}. Spent Planks={planksCost}");

            EnsureSubscribedIfNeeded();
        };

        runner.TryBegin(req);
    }

    private void StartTimedRepair(GameObject interactor)
    {
        var inv = ResolveInventory(interactor);
        if (inv == null) return;

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

            StartRepairLoopSfx();
        };

        req.onProgress = (p) =>
        {
            ShowRepairProgress(p);
            if (p <= 0f) StopRepairLoopSfx();
        };

        req.onCancel = () =>
        {
            StopRepairLoopSfx();
            HideRepairProgress();

            if (spent)
            {
                inv.Add(ResourceType.Planks, planksPerRepairStep);
                if (autoSaveInventoryOnChange) inv.SaveInMemory();
            }
        };

        req.onComplete = () =>
        {
            StopRepairLoopSfx();
            HideRepairProgress();

            if (!spent) return;

            if (!isBuilt)
            {
                inv.Add(ResourceType.Planks, planksPerRepairStep);
                if (autoSaveInventoryOnChange) inv.SaveInMemory();
                return;
            }

            if (currentDurability >= MaxDurability)
            {
                inv.Add(ResourceType.Planks, planksPerRepairStep);
                if (autoSaveInventoryOnChange) inv.SaveInMemory();
                return;
            }

            RestoreDurability(durabilityRestoredPerStep);

            if (autoSaveInventoryOnChange)
                inv.SaveInMemory();

            if (debugLogs)
                Debug.Log($"[WaterCollector] Repaired +{durabilityRestoredPerStep} => {currentDurability}/{MaxDurability} ({name})");
        };

        runner.TryBegin(req);
    }

    private void TryBuildImmediate(PlayerResourceInventory inv)
    {
        if (inv == null) return;

        if (!inv.Spend(ResourceType.Planks, planksCost))
            return;

        isBuilt = true;
        currentDurability = MaxDurability;
        _secTimer = 0f;
        _durabilityTimer = 0f;

        ApplyVisuals();
        OnBuiltChanged?.Invoke(true);
        BroadcastDurability();

        if (autoSaveInventoryOnChange)
            inv.SaveInMemory();

        SaveCollectorStateIfEnabled();

        if (debugLogs)
            Debug.Log($"[WaterCollector] Built on {name}. Spent Planks={planksCost}");

        EnsureSubscribedIfNeeded();
    }

    private void TryRepairImmediate(PlayerResourceInventory inv)
    {
        if (inv == null) return;
        if (!isBuilt) return;
        if (currentDurability >= MaxDurability) return;
        if (!inv.Spend(ResourceType.Planks, planksPerRepairStep)) return;

        RestoreDurability(durabilityRestoredPerStep);

        if (autoSaveInventoryOnChange)
            inv.SaveInMemory();

        if (debugLogs)
            Debug.Log($"[WaterCollector] Immediate Repair +{durabilityRestoredPerStep} => {currentDurability}/{MaxDurability} ({name})");
    }

    private void UpdateProductionByTime()
    {
        if (!CanProduceWater()) return;
        if (productionMode != ProductionMode.RealTimeSeconds) return;
        if (IsStorageFull()) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _secTimer += dt;

        float interval = Mathf.Max(0.01f, secondsPerWater);
        if (_secTimer >= interval)
        {
            int ticks = Mathf.FloorToInt(_secTimer / interval);
            _secTimer -= ticks * interval;

            int amount = ticks * Mathf.Max(1, waterPerProduction);
            ProduceWater(amount);
        }
    }

    private void UpdateDurabilityDecayByTime()
    {
        if (durabilityDecayMode != DurabilityDecayMode.ByTimeSeconds) return;
        if (!isBuilt) return;
        if (currentDurability <= 0) return;

        float dt = useUnscaledDurabilityTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _durabilityTimer += dt;

        float interval = Mathf.Max(0.01f, secondsPerDurabilityDecay);
        if (_durabilityTimer >= interval)
        {
            int ticks = Mathf.FloorToInt(_durabilityTimer / interval);
            _durabilityTimer -= ticks * interval;
            ApplyDurabilityDecay(ticks * Mathf.Max(1, durabilityDecayPerTrigger));
        }
    }

    private void EnsureSubscribedIfNeeded()
    {
        if (NeedsPhaseSubscription() && !_phaseSubscribed)
        {
            _gsm = ResolveGameStateManager();
            if (_gsm != null)
            {
                _gsm.OnDayStarted += HandleDayStarted;
                _gsm.OnNightStarted += HandleNightStarted;
                _phaseSubscribed = true;

                if (debugLogs)
                    Debug.Log($"[WaterCollector] Subscribed to GameStateManager: {_gsm.name} ({name})");
            }
        }

        if (NeedsWaveSubscription() && !_waveSubscribed)
        {
            _waveProgress = ResolveWaveProgress();
            if (_waveProgress != null)
            {
                _waveProgress.OnWaveStarted += HandleWaveStarted;
                _waveSubscribed = true;

                if (debugLogs)
                    Debug.Log($"[WaterCollector] Subscribed to WaveProgressTracker: {_waveProgress.name} ({name})");
            }
        }
    }

    private void Unsubscribe()
    {
        if (_phaseSubscribed && _gsm != null)
        {
            _gsm.OnDayStarted -= HandleDayStarted;
            _gsm.OnNightStarted -= HandleNightStarted;
        }

        if (_waveSubscribed && _waveProgress != null)
        {
            _waveProgress.OnWaveStarted -= HandleWaveStarted;
        }

        _phaseSubscribed = false;
        _waveSubscribed = false;
        _gsm = null;
        _waveProgress = null;
    }

    private void HandleDayStarted()
    {
        if (!isBuilt) return;

        if (productionMode == ProductionMode.PhaseTick && phaseTrigger == PhaseTrigger.OnDayStarted && CanProduceWater() && !IsStorageFull())
            ProduceWater(Mathf.Max(1, waterPerProduction));
    }

    private void HandleNightStarted()
    {
        if (!isBuilt) return;

        if (productionMode == ProductionMode.PhaseTick && phaseTrigger == PhaseTrigger.OnNightStarted && CanProduceWater() && !IsStorageFull())
            ProduceWater(Mathf.Max(1, waterPerProduction));

        if (durabilityDecayMode == DurabilityDecayMode.ByNightStarted)
            ApplyDurabilityDecay(Mathf.Max(1, durabilityDecayPerTrigger));
    }

    private void HandleWaveStarted(int waveId)
    {
        if (!isBuilt) return;
        if (durabilityDecayMode != DurabilityDecayMode.ByWaveStarted) return;
        ApplyDurabilityDecay(Mathf.Max(1, durabilityDecayPerTrigger));
    }

    private void ProduceWater(int amount)
    {
        if (amount <= 0) return;
        if (!isBuilt) return;
        if (!CanProduceWater()) return;
        if (storageCap <= 0) storageCap = 1;

        int before = storedWater;
        storedWater = Mathf.Min(storageCap, storedWater + amount);

        int produced = storedWater - before;
        if (produced <= 0) return;

        ApplyVisuals();
        OnWaterProduced?.Invoke(produced);
        OnStoredWaterChanged?.Invoke(storedWater, storageCap);

        SaveCollectorStateIfEnabled();

        if (debugLogs)
            Debug.Log($"[WaterCollector] Produced Water +{produced} => {storedWater}/{storageCap} ({name})");
    }

    private void TryCollect(GameObject interactor)
    {
        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        int take = collectAllAtOnce ? storedWater : Mathf.Min(Mathf.Max(1, collectAmountPerInteract), storedWater);
        if (take <= 0) return;

        BackpackOverflowMode mode = inv.GetOverflowMode();
        inv.TryAdd(ResourceType.Water, take, transform.position, out int accepted, out int rejected, true);

        int movedOut = accepted;
        if (rejected > 0 && mode != BackpackOverflowMode.DenyPickup)
            movedOut += rejected;

        if (movedOut <= 0) return;

        storedWater = Mathf.Max(0, storedWater - Mathf.Min(movedOut, storedWater));

        if (autoSaveInventoryOnChange)
            inv.SaveInMemory();

        ApplyVisuals();
        OnWaterCollected?.Invoke(movedOut);
        OnStoredWaterChanged?.Invoke(storedWater, storageCap);

        SaveCollectorStateIfEnabled();

        SfxPlayer.TryPlay(SfxId.Economy_WaterCollect, transform.position);

        if (debugLogs)
            Debug.Log($"[WaterCollector] Collected Water {movedOut}. Stored now {storedWater}/{storageCap} ({name})");
    }

    private void ApplyDurabilityDecay(int amount)
    {
        if (amount <= 0) return;
        if (!isBuilt) return;
        if (currentDurability <= 0) return;

        int before = currentDurability;
        currentDurability = Mathf.Clamp(currentDurability - amount, 0, MaxDurability);
        if (currentDurability == before) return;

        BroadcastDurability();
        SaveCollectorStateIfEnabled();

        if (debugLogs)
            Debug.Log($"[WaterCollector] Durability -{before - currentDurability} => {currentDurability}/{MaxDurability} ({name})");
    }

    private void RestoreDurability(int amount)
    {
        if (amount <= 0) return;
        if (!isBuilt) return;

        int before = currentDurability;
        currentDurability = Mathf.Clamp(currentDurability + amount, 0, MaxDurability);
        if (currentDurability == before) return;

        BroadcastDurability();
        SaveCollectorStateIfEnabled();
    }

    private bool CanOfferRepairPrompt()
    {
        if (!isBuilt) return false;
        return currentDurability < MaxDurability;
    }

    private bool CanCollect(GameObject interactor)
    {
        if (!isBuilt) return false;
        if (storedWater <= 0) return false;
        return ResolveInventory(interactor) != null;
    }

    private bool CanRepair(GameObject interactor)
    {
        if (!isBuilt) return false;
        if (currentDurability >= MaxDurability) return false;

        if (restrictRepairToDay)
        {
            var gsm = ResolveGameStateManager();
            if (gsm != null && gsm.CurrentPhase != DayNightPhase.Day)
                return false;
        }

        var inv = ResolveInventory(interactor);
        if (inv == null) return false;
        return inv.CanSpend(ResourceType.Planks, planksPerRepairStep);
    }

    private bool CanProduceWater()
    {
        if (!isBuilt) return false;
        if (stopProductionWhenBroken && currentDurability <= 0) return false;
        return true;
    }

    private bool IsStorageFull() => storedWater >= storageCap;

    private bool NeedsPhaseSubscription()
    {
        return productionMode == ProductionMode.PhaseTick || durabilityDecayMode == DurabilityDecayMode.ByNightStarted;
    }

    private bool NeedsWaveSubscription()
    {
        return durabilityDecayMode == DurabilityDecayMode.ByWaveStarted;
    }

    private GameStateManager ResolveGameStateManager()
    {
        if (_gsm == null)
            _gsm = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        return _gsm;
    }

    private WaveProgressTracker ResolveWaveProgress()
    {
        if (_waveProgress == null)
            _waveProgress = FindFirstObjectByType<WaveProgressTracker>(FindObjectsInactive.Include);
        return _waveProgress;
    }

    private PlayerResourceInventory ResolveInventory(GameObject interactor)
    {
        var inv = interactor != null ? interactor.GetComponentInParent<PlayerResourceInventory>() : null;
        if (inv != null) return inv;
        return PlayerResourceInventory.Instance;
    }

    private void ApplyVisuals()
    {
        if (unbuiltVisual != null) unbuiltVisual.SetActive(!isBuilt);
        if (builtVisual != null) builtVisual.SetActive(isBuilt);
        if (builtEmptyVisual != null) builtEmptyVisual.SetActive(isBuilt && storedWater <= 0);
        if (builtHasWaterVisual != null) builtHasWaterVisual.SetActive(isBuilt && storedWater > 0);
    }

    private void InitializeRuntimeState(bool loadedAnySave, bool loadedLegacySave)
    {
        if (maxDurability <= 0) maxDurability = 1;
        if (storageCap <= 0) storageCap = 1;

        if (!loadedAnySave)
        {
            if (isBuilt)
                currentDurability = MaxDurability;
            else
                currentDurability = 0;

            _secTimer = 0f;
            _durabilityTimer = 0f;
            return;
        }

        if (loadedLegacySave && isBuilt)
            currentDurability = MaxDurability;

        currentDurability = Mathf.Clamp(currentDurability, 0, MaxDurability);
        storedWater = Mathf.Clamp(storedWater, 0, storageCap);
        _secTimer = Mathf.Clamp(_secTimer, 0f, Mathf.Max(0.01f, secondsPerWater));
        _durabilityTimer = Mathf.Clamp(_durabilityTimer, 0f, Mathf.Max(0.01f, secondsPerDurabilityDecay));
    }

    private void ClampRuntimeAndBroadcast()
    {
        if (storageCap <= 0) storageCap = 1;
        if (maxDurability <= 0) maxDurability = 1;

        storedWater = Mathf.Clamp(storedWater, 0, storageCap);
        currentDurability = Mathf.Clamp(currentDurability, 0, MaxDurability);

        OnBuiltChanged?.Invoke(isBuilt);
        OnStoredWaterChanged?.Invoke(storedWater, storageCap);
        BroadcastDurability();
    }

    private void BroadcastDurability()
    {
        currentDurability = Mathf.Clamp(currentDurability, 0, MaxDurability);
        OnDurabilityChanged?.Invoke(currentDurability, MaxDurability);

        bool low = IsLowDurability || IsBroken;
        if (low != _lastLowDurability)
        {
            _lastLowDurability = low;
            OnLowDurabilityChanged?.Invoke(low);
        }
    }

    private void SaveCollectorStateIfEnabled()
    {
        if (string.IsNullOrWhiteSpace(collectorSaveKey)) return;

        string prefsKey = BaseWorldSession.ScopePlayerPrefsKey(collectorSaveKey);

        string data = string.Join("|",
            isBuilt ? "1" : "0",
            storedWater.ToString(CultureInfo.InvariantCulture),
            storageCap.ToString(CultureInfo.InvariantCulture),
            currentDurability.ToString(CultureInfo.InvariantCulture),
            _secTimer.ToString(CultureInfo.InvariantCulture),
            _durabilityTimer.ToString(CultureInfo.InvariantCulture));

        PlayerPrefs.SetString(prefsKey, data);
        PlayerPrefs.Save();
    }

    private bool TryLoadCollectorStateFromPrefs(out bool loadedLegacySave)
    {
        loadedLegacySave = false;

        if (string.IsNullOrWhiteSpace(collectorSaveKey))
            return false;

        string scopedKey = BaseWorldSession.ScopePlayerPrefsKey(collectorSaveKey);

        if (PlayerPrefs.HasKey(scopedKey))
            return ParseCollectorPayload(PlayerPrefs.GetString(scopedKey, ""), out loadedLegacySave);

        if (PlayerPrefs.HasKey(collectorSaveKey))
        {
            bool ok = ParseCollectorPayload(PlayerPrefs.GetString(collectorSaveKey, ""), out loadedLegacySave);
            if (ok)
            {
                PlayerPrefs.DeleteKey(collectorSaveKey);
                SaveCollectorStateIfEnabled();
                PlayerPrefs.Save();
            }

            return ok;
        }

        return false;
    }

    private bool ParseCollectorPayload(string data, out bool loadedLegacySave)
    {
        loadedLegacySave = false;

        try
        {
            if (string.IsNullOrWhiteSpace(data))
                return false;

            string[] parts = data.Split('|');
            if (parts.Length < 3)
                return false;

            loadedLegacySave = parts.Length < 6;

            isBuilt = parts[0] == "1";
            int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out storedWater);
            int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out storageCap);

            if (parts.Length >= 4)
                int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out currentDurability);

            if (parts.Length >= 5)
                float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out _secTimer);

            if (parts.Length >= 6)
                float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out _durabilityTimer);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void StartBuildLoopSfx()
    {
        if (!enableBuildLoopSfx) return;
        if (buildLoopSfx == null) return;
        buildLoopSfx.PlayLoop(buildLoopSfxId);
    }

    private void StopBuildLoopSfx()
    {
        if (buildLoopSfx == null) return;
        buildLoopSfx.StopLoop();
    }

    private void StartRepairLoopSfx()
    {
        if (!enableRepairLoopSfx) return;
        if (repairLoopSfx == null) return;
        repairLoopSfx.PlayLoop(repairLoopSfxId);
    }

    private void StopRepairLoopSfx()
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

    private static string FormatTimeShort(float seconds)
    {
        int t = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int m = t / 60;
        int s = t % 60;
        return $"{m:00}:{s:00}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (storageCap <= 0) storageCap = 1;
        if (maxDurability <= 0) maxDurability = 1;
        storedWater = Mathf.Clamp(storedWater, 0, storageCap);
        currentDurability = Mathf.Clamp(currentDurability, 0, MaxDurability);
        ApplyVisuals();
    }
#endif
}
