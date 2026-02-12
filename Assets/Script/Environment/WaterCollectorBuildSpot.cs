using System;
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

    [Header("Build Requirements")]
    public bool requireBuild = true;
    public int planksCost = 10;

    [Header("Timed Build Settings")]
    public float buildDuration = 4f;
    public bool buildHoldToComplete = true;
    public float maxBuildDistance = 2.5f;
    public bool lockPlayerMovementWhileBuilding = false;

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

    [Header("Runtime State (Serialized)")]
    [SerializeField] private bool isBuilt = false;
    [SerializeField] private int storedWater = 0;

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
    public string collectorSaveKey = "";

    public event Action<bool> OnBuiltChanged;
    public event Action<int, int> OnStoredWaterChanged;
    public event Action<int> OnWaterCollected;
    public event Action<int> OnWaterProduced;

    private float _secTimer = 0f;

    private GameStateManager _gsm;
    private bool _subscribed;
    private float _nextRetryTime;

    public int Priority => priority;

    private void Awake()
    {
        if (!requireBuild)
            isBuilt = true;

        if (!string.IsNullOrWhiteSpace(collectorSaveKey) && PlayerPrefs.HasKey(collectorSaveKey))
            LoadCollectorState();

        ClampStoredAndBroadcast();
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
        if (productionMode == ProductionMode.PhaseTick && !_subscribed)
        {
            if (Time.unscaledTime >= _nextRetryTime)
            {
                _nextRetryTime = Time.unscaledTime + 0.5f;
                EnsureSubscribedIfNeeded();
            }
        }

        if (!isBuilt) return;
        if (productionMode != ProductionMode.RealTimeSeconds) return;
        if (IsStorageFull()) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _secTimer += dt;

        if (_secTimer >= secondsPerWater)
        {
            int ticks = Mathf.FloorToInt(_secTimer / secondsPerWater);
            _secTimer -= ticks * secondsPerWater;

            int amount = ticks * waterPerProduction;
            ProduceWater(amount);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public string GetPrompt()
    {
        if (!isBuilt)
            return planksCost <= 0 ? "Build Water Collector" : $"Build Water Collector (-{planksCost} Planks)";

        if (storedWater > 0)
            return $"Collect Water (+{storedWater})";

        return $"Water Collector (0/{storageCap})";
    }

    public bool CanInteract(GameObject interactor)
    {
        if (!isBuilt)
        {
            var inv = ResolveInventory(interactor);
            if (inv == null) return false;
            return inv.CanSpend(ResourceType.Planks, planksCost);
        }

        return storedWater > 0;
    }

    public void Interact(GameObject interactor)
    {
        if (!isBuilt)
        {
            StartTimedBuild(interactor);
            return;
        }

        if (storedWater > 0)
            TryCollect(interactor);
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
                runner.CancelActive();
        };

        req.onCancel = () =>
        {
            if (spent)
            {
                inv.Add(ResourceType.Planks, planksCost);
                if (autoSaveInventoryOnChange) inv.SaveInMemory();
            }
        };

        req.onComplete = () =>
        {
            if (!spent) return;

            isBuilt = true;
            _secTimer = 0f;

            ApplyVisuals();
            OnBuiltChanged?.Invoke(true);

            if (autoSaveInventoryOnChange)
                inv.SaveInMemory();

            SaveCollectorStateIfEnabled();

            if (debugLogs)
                Debug.Log($"[WaterCollector] Built on {name}. Spent Planks={planksCost}");

            EnsureSubscribedIfNeeded();
        };

        runner.TryBegin(req);
    }

    private void TryBuildImmediate(PlayerResourceInventory inv)
    {
        if (inv == null) return;

        if (!inv.Spend(ResourceType.Planks, planksCost))
            return;

        isBuilt = true;
        _secTimer = 0f;

        ApplyVisuals();
        OnBuiltChanged?.Invoke(true);

        if (autoSaveInventoryOnChange)
            inv.SaveInMemory();

        SaveCollectorStateIfEnabled();

        if (debugLogs)
            Debug.Log($"[WaterCollector] Built on {name}. Spent Planks={planksCost}");

        EnsureSubscribedIfNeeded();
    }

    private void EnsureSubscribedIfNeeded()
    {
        if (productionMode != ProductionMode.PhaseTick) return;
        if (_subscribed) return;

        _gsm = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (_gsm == null) return;

        _gsm.OnDayStarted += HandleDayStarted;
        _gsm.OnNightStarted += HandleNightStarted;
        _subscribed = true;

        if (debugLogs)
            Debug.Log($"[WaterCollector] Subscribed to GameStateManager: {_gsm.name} ({name})");
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;

        if (_gsm != null)
        {
            _gsm.OnDayStarted -= HandleDayStarted;
            _gsm.OnNightStarted -= HandleNightStarted;
        }

        _subscribed = false;
        _gsm = null;
    }

    private void HandleDayStarted()
    {
        if (!isBuilt) return;
        if (productionMode != ProductionMode.PhaseTick) return;
        if (phaseTrigger != PhaseTrigger.OnDayStarted) return;
        if (IsStorageFull()) return;

        ProduceWater(waterPerProduction);
    }

    private void HandleNightStarted()
    {
        if (!isBuilt) return;
        if (productionMode != ProductionMode.PhaseTick) return;
        if (phaseTrigger != PhaseTrigger.OnNightStarted) return;
        if (IsStorageFull()) return;

        ProduceWater(waterPerProduction);
    }

    private void ProduceWater(int amount)
    {
        if (amount <= 0) return;
        if (!isBuilt) return;
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

    private bool IsStorageFull() => storedWater >= storageCap;

    private void TryCollect(GameObject interactor)
    {
        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        int take = collectAllAtOnce ? storedWater : Mathf.Min(collectAmountPerInteract, storedWater);
        if (take <= 0) return;

        storedWater -= take;

        inv.Add(ResourceType.Water, take);

        if (autoSaveInventoryOnChange)
            inv.SaveInMemory();

        ApplyVisuals();
        OnWaterCollected?.Invoke(take);
        OnStoredWaterChanged?.Invoke(storedWater, storageCap);

        SaveCollectorStateIfEnabled();

        if (debugLogs)
            Debug.Log($"[WaterCollector] Collected Water +{take}. Stored now {storedWater}/{storageCap} ({name})");
    }

    public bool IsBuilt => isBuilt;
    public int StoredWater => storedWater;
    public int StorageCap => storageCap;

    public void ForceSetBuilt(bool built)
    {
        isBuilt = built;
        ApplyVisuals();
        OnBuiltChanged?.Invoke(isBuilt);
        SaveCollectorStateIfEnabled();
    }

    public void ForceSetStoredWater(int amount)
    {
        storedWater = Mathf.Clamp(amount, 0, Mathf.Max(1, storageCap));
        ApplyVisuals();
        OnStoredWaterChanged?.Invoke(storedWater, storageCap);
        SaveCollectorStateIfEnabled();
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

    private void ClampStoredAndBroadcast()
    {
        if (storageCap <= 0) storageCap = 1;
        storedWater = Mathf.Clamp(storedWater, 0, storageCap);

        OnBuiltChanged?.Invoke(isBuilt);
        OnStoredWaterChanged?.Invoke(storedWater, storageCap);
    }

    private void SaveCollectorStateIfEnabled()
    {
        if (string.IsNullOrWhiteSpace(collectorSaveKey)) return;

        string data = $"{(isBuilt ? 1 : 0)}|{storedWater}|{storageCap}";
        PlayerPrefs.SetString(collectorSaveKey, data);
        PlayerPrefs.Save();
    }

    private void LoadCollectorState()
    {
        try
        {
            string data = PlayerPrefs.GetString(collectorSaveKey, "");
            if (string.IsNullOrWhiteSpace(data)) return;

            string[] parts = data.Split('|');
            if (parts.Length < 3) return;

            isBuilt = parts[0] == "1";
            int.TryParse(parts[1], out storedWater);
            int.TryParse(parts[2], out storageCap);
        }
        catch
        {
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (storageCap <= 0) storageCap = 1;
        storedWater = Mathf.Clamp(storedWater, 0, storageCap);
        ApplyVisuals();
    }
#endif
}
