using UnityEngine;

public class FarmlandPlot : MonoBehaviour, IInteractable
{
    public enum PlotState
    {
        Empty = 0,
        PlantedDry = 1,
        PlantedWatered = 2,
        ReadyToHarvest = 3
    }

    [SerializeField] private PlotState state = PlotState.Empty;

    public CropConfigSO cropToPlant;

    [SerializeField] private CropConfigSO plantedCrop;
    [SerializeField] private int growthDaysCompleted = 0;

    [SerializeField] private bool wateredSinceLastDayStart = false;

    public int seedCost = 1;

    public ResourceDrop2D harvestDropPrefab;
    public bool harvestGoesToInventoryDirectly = true;

    public bool restrictActionsToDay = true;

    public float plantDuration = 2f;
    public float waterDuration = 1f;
    public bool holdToComplete = true;
    public float maxActionDistance = 2.5f;
    public bool lockPlayerMovementWhileActing = false;
    public bool autoSaveInventoryOnAction = true;

    public int priority = 5;
    public int Priority => priority;

    public GameObject emptyVisual;
    public GameObject plantedVisual;
    public GameObject wateredVisual;
    public GameObject matureVisual;

    public bool debugLogs = false;

    public bool enableActionLoopSfx = true;
    public AudioClip[] plantActionLoopClips;
    public AudioClip[] waterActionLoopClips;
    public float actionLoopVolumeMultiplier = 1f;

    private GameStateManager _gsm;
    private bool _subscribed;

    private AudioSource _actionLoopSource;

    private void Awake()
    {
        AutoWireVisualsIfNull();
        ApplyVisuals();
        TrySubscribe();
        EnsureActionLoopSource();
    }

    private void OnEnable()
    {
        TrySubscribe();
        EnsureActionLoopSource();
    }

    private void Start()
    {
        TrySubscribe();
        EnsureActionLoopSource();
    }

    private void Update()
    {
        if (!_subscribed) TrySubscribe();
    }

    private void OnDisable()
    {
        StopActionLoop();
        TryUnsubscribe();
    }

    private void EnsureActionLoopSource()
    {
        if (!enableActionLoopSfx) return;
        if (_actionLoopSource != null) return;

        var go = new GameObject("ActionLoopSFX");
        go.transform.SetParent(transform, false);

        _actionLoopSource = go.AddComponent<AudioSource>();
        _actionLoopSource.playOnAwake = false;
        _actionLoopSource.loop = true;
        _actionLoopSource.spatialBlend = 0f;
        _actionLoopSource.volume = 1f;
        _actionLoopSource.pitch = 1f;
    }

    private void BeginActionLoop(SfxId id, AudioClip[] overrideClips)
    {
        if (!enableActionLoopSfx) return;
        EnsureActionLoopSource();
        if (_actionLoopSource == null) return;

        StopActionLoop();

        AudioClip clip = null;

        if (overrideClips != null && overrideClips.Length > 0)
        {
            clip = overrideClips.Length == 1 ? overrideClips[0] : overrideClips[Random.Range(0, overrideClips.Length)];
        }
        else
        {
            var sp = SfxPlayer.Instance;
            if (sp != null) clip = sp.PickClip(id);
        }

        if (clip == null) return;

        float volume = 1f;
        Vector2 pitchRange = new Vector2(1f, 1f);
        float spatialBlend = 0f;

        var player = SfxPlayer.Instance;
        if (player != null && player.TryGetEntry(id, out var entry) && entry != null)
        {
            volume = entry.volume;
            pitchRange = entry.pitchRange;
            spatialBlend = entry.spatialBlend;
        }

        float pMin = pitchRange.x <= 0f ? 0.01f : pitchRange.x;
        float pMax = pitchRange.y <= 0f ? 0.01f : pitchRange.y;
        if (pMax < pMin) { float t = pMin; pMin = pMax; pMax = t; }
        float pitch = (pMin == pMax) ? pMin : Random.Range(pMin, pMax);

        _actionLoopSource.clip = clip;
        _actionLoopSource.loop = true;
        _actionLoopSource.spatialBlend = Mathf.Clamp01(spatialBlend);
        _actionLoopSource.volume = Mathf.Clamp01(volume * Mathf.Max(0f, actionLoopVolumeMultiplier));
        _actionLoopSource.pitch = pitch;
        _actionLoopSource.Play();
    }

    private void StopActionLoop()
    {
        if (_actionLoopSource == null) return;
        if (_actionLoopSource.isPlaying) _actionLoopSource.Stop();
        _actionLoopSource.clip = null;
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;

        if (_gsm == null)
            _gsm = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();

        if (_gsm == null) return;

        _gsm.OnDayStarted += HandleDayStarted;
        _subscribed = true;

        if (debugLogs)
            Debug.Log($"[FarmlandPlot] Subscribed OnDayStarted -> {_gsm.name} ({name})");
    }

    private void TryUnsubscribe()
    {
        if (!_subscribed) return;

        if (_gsm != null)
            _gsm.OnDayStarted -= HandleDayStarted;

        _subscribed = false;
    }

    public string GetPrompt()
    {
        switch (state)
        {
            case PlotState.Empty:
                {
                    if (cropToPlant == null) return "Plant";
                    string days = cropToPlant.daysToMature <= 0 ? "Instant" : $"{cropToPlant.daysToMature} days";
                    return seedCost <= 0
                        ? $"Plant {cropToPlant.displayName} ({days})"
                        : $"Plant {cropToPlant.displayName} (-{seedCost} Seeds, {days})";
                }

            case PlotState.PlantedDry:
                {
                    if (plantedCrop == null) return "Planted";
                    return plantedCrop.requiresDailyWater
                        ? $"Water ({growthDaysCompleted}/{plantedCrop.daysToMature})"
                        : $"Growing... ({growthDaysCompleted}/{plantedCrop.daysToMature})";
                }

            case PlotState.PlantedWatered:
                {
                    if (plantedCrop == null) return "Growing...";
                    return $"Watered ({growthDaysCompleted}/{plantedCrop.daysToMature})";
                }

            case PlotState.ReadyToHarvest:
                {
                    if (plantedCrop == null) return "Harvest";
                    return $"Harvest (+{plantedCrop.harvestResource} x{plantedCrop.harvestAmount})";
                }

            default:
                return "Interact";
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        if (restrictActionsToDay)
        {
            var gsm = GameStateManager.Instance != null ? GameStateManager.Instance : _gsm;
            if (gsm != null && gsm.CurrentPhase != DayNightPhase.Day)
                return false;
        }

        var inv = ResolveInventory(interactor);

        switch (state)
        {
            case PlotState.Empty:
                if (cropToPlant == null) return false;
                return inv != null && inv.CanSpend(ResourceType.Seeds, seedCost);

            case PlotState.PlantedDry:
                if (plantedCrop == null) return false;
                if (!plantedCrop.requiresDailyWater) return false;
                if (wateredSinceLastDayStart) return false;
                return inv != null && inv.CanSpend(ResourceType.Water, plantedCrop.waterCostPerDay);

            case PlotState.PlantedWatered:
                return false;

            case PlotState.ReadyToHarvest:
                return true;

            default:
                return false;
        }
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
            return;

        switch (state)
        {
            case PlotState.Empty:
                StartTimedPlant(interactor);
                break;

            case PlotState.PlantedDry:
                StartTimedWater(interactor);
                break;

            case PlotState.ReadyToHarvest:
                TryHarvest(interactor);
                break;
        }
    }

    private void StartTimedPlant(GameObject interactor)
    {
        if (cropToPlant == null) return;

        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        if (!inv.CanSpend(ResourceType.Seeds, seedCost)) return;

        var runner = interactor != null ? interactor.GetComponentInParent<TimedActionController>() : null;
        if (runner == null)
        {
            TryPlantImmediate(inv);
            return;
        }

        if (runner.IsBusy) return;

        bool spent = false;
        var pi = interactor != null ? interactor.GetComponentInParent<PlayerInteractor2D>() : null;
        KeyCode holdKey = pi != null ? pi.interactKey : KeyCode.E;

        var req = new TimedActionRequest();
        req.label = "Planting...";
        req.duration = Mathf.Max(0.05f, plantDuration);
        req.requireHold = holdToComplete;
        req.holdKey = holdKey;
        req.lockPlayerMovement = lockPlayerMovementWhileActing;
        req.target = transform;
        req.maxDistance = maxActionDistance;
        req.cancelIfPhaseNotDay = restrictActionsToDay;

        req.onBegin = () =>
        {
            spent = inv.Spend(ResourceType.Seeds, seedCost);
            if (!spent)
            {
                runner.CancelActive();
                return;
            }

            BeginActionLoop(SfxId.Farming_Plant, plantActionLoopClips);
        };

        req.onProgress = (p) =>
        {
            if (p <= 0f) StopActionLoop();
        };

        req.onCancel = () =>
        {
            StopActionLoop();

            if (spent)
            {
                inv.Add(ResourceType.Seeds, seedCost);
                if (autoSaveInventoryOnAction) inv.SaveInMemory();
            }
        };

        req.onComplete = () =>
        {
            StopActionLoop();

            if (!spent) return;

            plantedCrop = cropToPlant;
            growthDaysCompleted = 0;
            wateredSinceLastDayStart = false;

            SetState(PlotState.PlantedDry);

            if (autoSaveInventoryOnAction) inv.SaveInMemory();

            if (debugLogs)
                Debug.Log($"[FarmlandPlot] Plant -> {plantedCrop.displayName} on {name} (daysToMature={plantedCrop.daysToMature})");
        };

        runner.TryBegin(req);
    }

    private void StartTimedWater(GameObject interactor)
    {
        if (plantedCrop == null) return;
        if (!plantedCrop.requiresDailyWater) return;
        if (wateredSinceLastDayStart) return;

        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        int waterCost = plantedCrop.waterCostPerDay;
        if (!inv.CanSpend(ResourceType.Water, waterCost)) return;

        var runner = interactor != null ? interactor.GetComponentInParent<TimedActionController>() : null;
        if (runner == null)
        {
            TryWaterImmediate(inv);
            return;
        }

        if (runner.IsBusy) return;

        bool spent = false;
        var pi = interactor != null ? interactor.GetComponentInParent<PlayerInteractor2D>() : null;
        KeyCode holdKey = pi != null ? pi.interactKey : KeyCode.E;

        var req = new TimedActionRequest();
        req.label = "Watering...";
        req.duration = Mathf.Max(0.05f, waterDuration);
        req.requireHold = holdToComplete;
        req.holdKey = holdKey;
        req.lockPlayerMovement = lockPlayerMovementWhileActing;
        req.target = transform;
        req.maxDistance = maxActionDistance;
        req.cancelIfPhaseNotDay = restrictActionsToDay;

        req.onBegin = () =>
        {
            spent = inv.Spend(ResourceType.Water, waterCost);
            if (!spent)
            {
                runner.CancelActive();
                return;
            }

            BeginActionLoop(SfxId.Farming_Water, waterActionLoopClips);
        };

        req.onProgress = (p) =>
        {
            if (p <= 0f) StopActionLoop();
        };

        req.onCancel = () =>
        {
            StopActionLoop();

            if (spent)
            {
                inv.Add(ResourceType.Water, waterCost);
                if (autoSaveInventoryOnAction) inv.SaveInMemory();
            }
        };

        req.onComplete = () =>
        {
            StopActionLoop();

            if (!spent) return;

            wateredSinceLastDayStart = true;
            SetState(PlotState.PlantedWatered);

            if (autoSaveInventoryOnAction) inv.SaveInMemory();

            if (debugLogs)
                Debug.Log($"[FarmlandPlot] Water -> {name} (will be counted at next DayStart)");
        };

        runner.TryBegin(req);
    }

    private void TryPlantImmediate(PlayerResourceInventory inv)
    {
        if (cropToPlant == null) return;
        if (inv == null) return;

        if (!inv.Spend(ResourceType.Seeds, seedCost))
            return;

        plantedCrop = cropToPlant;
        growthDaysCompleted = 0;
        wateredSinceLastDayStart = false;

        SetState(PlotState.PlantedDry);

        SfxPlayer.TryPlay(SfxId.Farming_Plant, transform.position);

        if (autoSaveInventoryOnAction) inv.SaveInMemory();

        if (debugLogs)
            Debug.Log($"[FarmlandPlot] Plant -> {plantedCrop.displayName} on {name} (daysToMature={plantedCrop.daysToMature})");
    }

    private void TryWaterImmediate(PlayerResourceInventory inv)
    {
        if (plantedCrop == null) return;
        if (!plantedCrop.requiresDailyWater) return;
        if (wateredSinceLastDayStart) return;
        if (inv == null) return;

        if (!inv.Spend(ResourceType.Water, plantedCrop.waterCostPerDay))
            return;

        wateredSinceLastDayStart = true;
        SetState(PlotState.PlantedWatered);

        SfxPlayer.TryPlay(SfxId.Farming_Water, transform.position);

        if (autoSaveInventoryOnAction) inv.SaveInMemory();

        if (debugLogs)
            Debug.Log($"[FarmlandPlot] Water -> {name} (will be counted at next DayStart)");
    }

    private void TryHarvest(GameObject interactor)
    {
        if (plantedCrop == null)
        {
            ResetPlot();
            return;
        }

        SfxPlayer.TryPlay(SfxId.Farming_Harvest, transform.position);

        if (harvestGoesToInventoryDirectly || harvestDropPrefab == null)
        {
            var inv = ResolveInventory(interactor);
            if (inv != null) inv.Add(plantedCrop.harvestResource, plantedCrop.harvestAmount);
        }
        else
        {
            var drop = Instantiate(harvestDropPrefab, transform.position, Quaternion.identity);
            drop.Configure(plantedCrop.harvestResource, plantedCrop.harvestAmount);
        }

        if (debugLogs)
            Debug.Log($"[FarmlandPlot] Harvest -> {plantedCrop.displayName} on {name}");

        ResetPlot();
    }

    private void HandleDayStarted()
    {
        if (state != PlotState.PlantedDry && state != PlotState.PlantedWatered)
            return;

        if (plantedCrop == null)
        {
            ResetPlot();
            return;
        }

        bool canGrowToday = true;

        if (plantedCrop.requiresDailyWater)
            canGrowToday = wateredSinceLastDayStart;

        if (debugLogs)
            Debug.Log($"[FarmlandPlot] DayStart -> {name} canGrow={canGrowToday} wateredFlag={wateredSinceLastDayStart} progress={growthDaysCompleted}/{plantedCrop.daysToMature}");

        if (canGrowToday)
            growthDaysCompleted++;

        wateredSinceLastDayStart = false;

        int target = Mathf.Max(0, plantedCrop.daysToMature);
        if (growthDaysCompleted >= target)
            SetState(PlotState.ReadyToHarvest);
        else
            SetState(PlotState.PlantedDry);
    }

    public void ResetPlot()
    {
        plantedCrop = null;
        growthDaysCompleted = 0;
        wateredSinceLastDayStart = false;
        SetState(PlotState.Empty);
    }

    public PlotState GetState() => state;

    public void SetState(PlotState newState)
    {
        state = newState;
        ApplyVisuals();
    }

    private PlayerResourceInventory ResolveInventory(GameObject interactor)
    {
        var inv = interactor != null ? interactor.GetComponentInParent<PlayerResourceInventory>() : null;
        if (inv != null) return inv;
        return PlayerResourceInventory.Instance;
    }

    private void ApplyVisuals()
    {
        if (emptyVisual != null) emptyVisual.SetActive(state == PlotState.Empty);
        if (plantedVisual != null) plantedVisual.SetActive(state == PlotState.PlantedDry);
        if (wateredVisual != null) wateredVisual.SetActive(state == PlotState.PlantedWatered);
        if (matureVisual != null) matureVisual.SetActive(state == PlotState.ReadyToHarvest);
    }

    private void AutoWireVisualsIfNull()
    {
        if (emptyVisual == null) emptyVisual = transform.Find("EmptyVisual")?.gameObject;
        if (plantedVisual == null) plantedVisual = transform.Find("PlantedVisual")?.gameObject;
        if (wateredVisual == null) wateredVisual = transform.Find("WateredVisual")?.gameObject;
        if (matureVisual == null) matureVisual = transform.Find("MatureVisual")?.gameObject;
    }
}
