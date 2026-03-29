using System;
using System.Collections.Generic;
using System.Text;
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

    [Serializable]
    public class CropVisualSet
    {
        public CropConfigSO crop;
        public List<GameObject> dryStageVisuals = new List<GameObject>();
        public List<GameObject> wateredStageVisuals = new List<GameObject>();
        public GameObject matureVisual;

        public GameObject GetDryStageVisual(int stageIndex)
        {
            if (dryStageVisuals == null || dryStageVisuals.Count == 0) return null;
            stageIndex = Mathf.Clamp(stageIndex, 0, dryStageVisuals.Count - 1);
            return dryStageVisuals[stageIndex];
        }

        public GameObject GetWateredStageVisual(int stageIndex)
        {
            if (wateredStageVisuals == null || wateredStageVisuals.Count == 0) return null;
            stageIndex = Mathf.Clamp(stageIndex, 0, wateredStageVisuals.Count - 1);
            return wateredStageVisuals[stageIndex];
        }
    }

    [SerializeField] private PlotState state = PlotState.Empty;

    [Header("Crop")]
    public CropConfigSO cropToPlant;

    [SerializeField] private CropConfigSO plantedCrop;
    [SerializeField] private int growthDaysCompleted = 0;
    [SerializeField] private bool wateredSinceLastDayStart = false;

    [Header("Legacy Plant Cost Fallback")]
    public int seedCost = 1;

    [Header("Harvest")]
    public ResourceDrop2D harvestDropPrefab;
    public bool harvestGoesToInventoryDirectly = true;

    [Header("Rules")]
    public bool restrictActionsToDay = true;

    [Header("Timed Actions")]
    public float plantDuration = 2f;
    public float waterDuration = 1f;
    public bool holdToComplete = true;
    public float maxActionDistance = 2.5f;
    public bool lockPlayerMovementWhileActing = false;
    public bool autoSaveInventoryOnAction = true;

    [Header("Interaction")]
    public int priority = 5;
    public int Priority => priority;

    [Header("Default Visuals")]
    public GameObject emptyVisual;
    public GameObject plantedVisual;
    public GameObject wateredVisual;
    public GameObject matureVisual;

    [Header("Crop Visual Sets")]
    public bool useCropSpecificVisualSets = true;
    public List<CropVisualSet> cropVisualSets = new List<CropVisualSet>();

    [Header("Persistence")]
    [Tooltip("Unique id per plot in Base scene (required for Base <-> Town construction snapshot).")]
    public string plotId = "";

    [Header("Debug")]
    public bool debugLogs = false;

    [Header("Action Loop SFX")]
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
            clip = overrideClips.Length == 1 ? overrideClips[0] : overrideClips[UnityEngine.Random.Range(0, overrideClips.Length)];
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
        if (pMax < pMin)
        {
            float t = pMin;
            pMin = pMax;
            pMax = t;
        }

        float pitch = pMin == pMax ? pMin : UnityEngine.Random.Range(pMin, pMax);

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
                    CropConfigSO crop = ResolveRequestedCrop(null);
                    if (crop == null) return "Select Seed";

                    string days = crop.daysToMature <= 0 ? "Instant" : $"{crop.daysToMature} days";
                    ResourceType plantType = GetPlantSeedResource(crop);
                    int plantCost = GetPlantSeedCost(crop);
                    string costText = plantCost <= 0 ? "" : $" (-{plantCost} {FormatResourceTypeName(plantType)})";
                    return $"Plant {crop.displayName}{costText}, {days}";
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
                    return $"Harvest (+{FormatResourceTypeName(plantedCrop.harvestResource)} x{plantedCrop.GetHarvestAmountLabel()})";
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
                {
                    CropConfigSO crop = ResolveRequestedCrop(interactor);
                    if (crop == null) return false;
                    if (inv == null) return false;
                    return inv.CanSpend(GetPlantSeedResource(crop), GetPlantSeedCost(crop));
                }

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
        CropConfigSO requestedCrop = ResolveRequestedCrop(interactor);
        if (requestedCrop == null) return;

        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        ResourceType plantType = GetPlantSeedResource(requestedCrop);
        int plantCost = GetPlantSeedCost(requestedCrop);

        if (!inv.CanSpend(plantType, plantCost)) return;

        var runner = interactor != null ? interactor.GetComponentInParent<TimedActionController>() : null;
        if (runner == null)
        {
            TryPlantImmediate(inv, requestedCrop, interactor);
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
            spent = inv.Spend(plantType, plantCost);
            if (!spent)
            {
                runner.CancelActive();
                return;
            }

            BeginActionLoop(requestedCrop.plantSfxId, plantActionLoopClips);
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
                inv.Add(plantType, plantCost);
                if (autoSaveInventoryOnAction) inv.SaveInMemory();
            }
        };

        req.onComplete = () =>
        {
            StopActionLoop();

            if (!spent) return;

            plantedCrop = requestedCrop;
            growthDaysCompleted = 0;
            wateredSinceLastDayStart = false;

            SetState(PlotState.PlantedDry);

            var plantingController = ResolvePlantingController(interactor);
            plantingController?.NotifyPlantCompleted(inv);

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

            BeginActionLoop(plantedCrop.waterSfxId, waterActionLoopClips);
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

    private void TryPlantImmediate(PlayerResourceInventory inv, CropConfigSO requestedCrop, GameObject interactor)
    {
        if (requestedCrop == null) return;
        if (inv == null) return;

        ResourceType plantType = GetPlantSeedResource(requestedCrop);
        int plantCost = GetPlantSeedCost(requestedCrop);

        if (!inv.Spend(plantType, plantCost))
            return;

        plantedCrop = requestedCrop;
        growthDaysCompleted = 0;
        wateredSinceLastDayStart = false;

        SetState(PlotState.PlantedDry);

        SfxPlayer.TryPlay(requestedCrop.plantSfxId, transform.position);

        var plantingController = ResolvePlantingController(interactor);
        plantingController?.NotifyPlantCompleted(inv);

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

        SfxPlayer.TryPlay(plantedCrop.waterSfxId, transform.position);

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

        int harvestAmount = plantedCrop.GetResolvedHarvestAmount();

        SfxPlayer.TryPlay(plantedCrop.harvestSfxId, transform.position);

        if (harvestGoesToInventoryDirectly || harvestDropPrefab == null)
        {
            var inv = ResolveInventory(interactor);
            if (inv != null) inv.Add(plantedCrop.harvestResource, harvestAmount);
        }
        else
        {
            var drop = Instantiate(harvestDropPrefab, transform.position, Quaternion.identity);
            drop.Configure(plantedCrop.harvestResource, harvestAmount);
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

    public FarmlandPlotEntry BuildSnapshotEntry()
    {
        return new FarmlandPlotEntry
        {
            plotId = plotId,
            cropId = plantedCrop != null ? plantedCrop.cropId : "",
            growthDaysCompleted = growthDaysCompleted,
            wateredSinceLastDayStart = wateredSinceLastDayStart,
            plotState = (int)state
        };
    }

    public void TryApplySnapshotFromTravel(FarmlandPlotEntry e, CropConfigSO[] cropCatalog)
    {
        if (e == null) return;

        if (!System.Enum.IsDefined(typeof(PlotState), e.plotState))
        {
            ResetPlot();
            return;
        }

        var st = (PlotState)e.plotState;

        if (st == PlotState.Empty || string.IsNullOrEmpty(e.cropId))
        {
            ResetPlot();
            return;
        }

        CropConfigSO crop = CropConfigCatalogUtil.ResolveByCropId(e.cropId, cropCatalog);
        if (crop == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[FarmlandPlot] Unknown cropId '{e.cropId}' on {name}. Resetting plot.");
            ResetPlot();
            return;
        }

        plantedCrop = crop;
        growthDaysCompleted = Mathf.Max(0, e.growthDaysCompleted);
        wateredSinceLastDayStart = e.wateredSinceLastDayStart;
        SetState(st);
    }

    public void SetState(PlotState newState)
    {
        state = newState;
        ApplyVisuals();
    }

    private CropConfigSO ResolveRequestedCrop(GameObject interactor)
    {
        var plantingController = ResolvePlantingController(interactor);
        if (plantingController != null && plantingController.TryGetSelectedCrop(out var selectedCrop) && selectedCrop != null)
            return selectedCrop;

        return cropToPlant;
    }

    private PlayerSeedPlantingController ResolvePlantingController(GameObject interactor)
    {
        var controller = interactor != null ? interactor.GetComponentInParent<PlayerSeedPlantingController>() : null;
        if (controller != null) return controller;
        return FindFirstObjectByType<PlayerSeedPlantingController>(FindObjectsInactive.Include);
    }

    private ResourceType GetPlantSeedResource(CropConfigSO crop)
    {
        if (crop == null) return ResourceType.Seeds;
        return crop.seedResource;
    }

    private int GetPlantSeedCost(CropConfigSO crop)
    {
        if (crop == null) return Mathf.Max(0, seedCost);
        return crop.GetResolvedSeedCost(seedCost);
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

        if (plantedVisual != null) plantedVisual.SetActive(false);
        if (wateredVisual != null) wateredVisual.SetActive(false);
        if (matureVisual != null) matureVisual.SetActive(false);

        DisableAllCropSpecificVisuals();

        if (state == PlotState.Empty)
            return;

        CropConfigSO visualCrop = plantedCrop != null ? plantedCrop : cropToPlant;
        bool usedSpecific = TryApplyCropSpecificVisuals(visualCrop);

        if (usedSpecific)
            return;

        if (plantedVisual != null) plantedVisual.SetActive(state == PlotState.PlantedDry);
        if (wateredVisual != null) wateredVisual.SetActive(state == PlotState.PlantedWatered);
        if (matureVisual != null) matureVisual.SetActive(state == PlotState.ReadyToHarvest);
    }

    private bool TryApplyCropSpecificVisuals(CropConfigSO crop)
    {
        if (!useCropSpecificVisualSets) return false;
        if (crop == null) return false;

        CropVisualSet set = FindVisualSet(crop);
        if (set == null) return false;

        if (state == PlotState.ReadyToHarvest)
        {
            if (set.matureVisual != null)
            {
                set.matureVisual.SetActive(true);
                return true;
            }

            return false;
        }

        int stageIndex = crop.GetGrowthStageIndex(growthDaysCompleted);

        if (state == PlotState.PlantedWatered)
        {
            GameObject wateredGo = set.GetWateredStageVisual(stageIndex);
            if (wateredGo != null)
            {
                wateredGo.SetActive(true);
                return true;
            }

            GameObject dryFallback = set.GetDryStageVisual(stageIndex);
            if (dryFallback != null)
            {
                dryFallback.SetActive(true);
                return true;
            }

            return false;
        }

        if (state == PlotState.PlantedDry)
        {
            GameObject dryGo = set.GetDryStageVisual(stageIndex);
            if (dryGo != null)
            {
                dryGo.SetActive(true);
                return true;
            }

            GameObject wateredFallback = set.GetWateredStageVisual(stageIndex);
            if (wateredFallback != null)
            {
                wateredFallback.SetActive(true);
                return true;
            }

            return false;
        }

        return false;
    }

    private CropVisualSet FindVisualSet(CropConfigSO crop)
    {
        if (crop == null || cropVisualSets == null) return null;

        for (int i = 0; i < cropVisualSets.Count; i++)
        {
            var set = cropVisualSets[i];
            if (set == null) continue;
            if (set.crop == crop) return set;
        }

        return null;
    }

    private void DisableAllCropSpecificVisuals()
    {
        if (cropVisualSets == null) return;

        for (int i = 0; i < cropVisualSets.Count; i++)
        {
            var set = cropVisualSets[i];
            if (set == null) continue;

            if (set.dryStageVisuals != null)
            {
                for (int j = 0; j < set.dryStageVisuals.Count; j++)
                {
                    if (set.dryStageVisuals[j] != null)
                        set.dryStageVisuals[j].SetActive(false);
                }
            }

            if (set.wateredStageVisuals != null)
            {
                for (int j = 0; j < set.wateredStageVisuals.Count; j++)
                {
                    if (set.wateredStageVisuals[j] != null)
                        set.wateredStageVisuals[j].SetActive(false);
                }
            }

            if (set.matureVisual != null)
                set.matureVisual.SetActive(false);
        }
    }

    private void AutoWireVisualsIfNull()
    {
        if (emptyVisual == null) emptyVisual = transform.Find("EmptyVisual")?.gameObject;
        if (plantedVisual == null) plantedVisual = transform.Find("PlantedVisual")?.gameObject;
        if (wateredVisual == null) wateredVisual = transform.Find("WateredVisual")?.gameObject;
        if (matureVisual == null) matureVisual = transform.Find("MatureVisual")?.gameObject;
    }

    private static string FormatResourceTypeName(ResourceType type)
    {
        string raw = type.ToString();
        if (string.IsNullOrEmpty(raw)) return raw;

        StringBuilder sb = new StringBuilder(raw.Length + 8);
        sb.Append(raw[0]);

        for (int i = 1; i < raw.Length; i++)
        {
            char c = raw[i];
            char prev = raw[i - 1];

            if (char.IsUpper(c) && !char.IsUpper(prev))
                sb.Append(' ');

            sb.Append(c);
        }

        return sb.ToString();
    }
}
