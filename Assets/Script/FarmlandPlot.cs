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

    [Header("State")]
    [SerializeField] private PlotState state = PlotState.Empty;

    [Header("Crop (Demo)")]
    public CropConfigSO cropToPlant;

    [SerializeField] private CropConfigSO plantedCrop;
    [SerializeField] private int growthDaysCompleted = 0;

    [Tooltip("Set true when player waters during the day. Evaluated at next DayStart, then reset.")]
    [SerializeField] private bool wateredSinceLastDayStart = false;

    [Header("Plant Cost (Demo)")]
    [Min(0)] public int seedCost = 1;

    [Header("Harvest (Optional)")]
    public ResourceDrop2D harvestDropPrefab;
    public bool harvestGoesToInventoryDirectly = true;

    [Header("Rules")]
    public bool restrictActionsToDay = true;

    [Header("Interact")]
    public int priority = 5;
    public int Priority => priority;

    [Header("Visual Placeholders (Optional)")]
    public GameObject emptyVisual;
    public GameObject plantedVisual;
    public GameObject wateredVisual;
    public GameObject matureVisual;

    [Header("Debug")]
    public bool debugLogs = false;

    // --- Day system subscription (robust) ---
    private GameStateManager _gsm;
    private bool _subscribed;

    private void Awake()
    {
        AutoWireVisualsIfNull();
        ApplyVisuals();
        TrySubscribe(); // in case GSM already exists
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void Update()
    {
        // If GSM spawns later or execution order makes Instance not ready in Awake/OnEnable.
        if (!_subscribed) TrySubscribe();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
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
                    if (cropToPlant == null) return "Plant (No Crop Config)";
                    string days = cropToPlant.daysToMature <= 0 ? "Instant" : $"{cropToPlant.daysToMature} days";
                    return seedCost <= 0
                        ? $"Plant {cropToPlant.displayName} ({days})"
                        : $"Plant {cropToPlant.displayName} (-{seedCost} Seeds, {days})";
                }

            case PlotState.PlantedDry:
                {
                    if (plantedCrop == null) return "Planted (Invalid Crop)";
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
                if (wateredSinceLastDayStart) return false; // already watered today
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
                TryPlant(interactor);
                break;

            case PlotState.PlantedDry:
                TryWater(interactor);
                break;

            case PlotState.ReadyToHarvest:
                TryHarvest(interactor);
                break;
        }
    }

    private void TryPlant(GameObject interactor)
    {
        if (cropToPlant == null) return;

        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        if (!inv.Spend(ResourceType.Seeds, seedCost))
            return;

        plantedCrop = cropToPlant;
        growthDaysCompleted = 0;
        wateredSinceLastDayStart = false;

        SetState(PlotState.PlantedDry);

        if (debugLogs)
            Debug.Log($"[FarmlandPlot] Plant -> {plantedCrop.displayName} on {name} (daysToMature={plantedCrop.daysToMature})");
    }

    private void TryWater(GameObject interactor)
    {
        if (plantedCrop == null) return;
        if (!plantedCrop.requiresDailyWater) return;
        if (wateredSinceLastDayStart) return;

        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        if (!inv.Spend(ResourceType.Water, plantedCrop.waterCostPerDay))
            return;

        wateredSinceLastDayStart = true;
        SetState(PlotState.PlantedWatered);

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

        if (harvestGoesToInventoryDirectly || harvestDropPrefab == null)
        {
            var inv = ResolveInventory(interactor);
            if (inv != null) inv.Add(plantedCrop.harvestResource, plantedCrop.harvestAmount);
            else Debug.LogWarning($"[FarmlandPlot] No inventory found; harvest lost on {name}");
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

    // --- Timer system: grow at DayStart ---
    private void HandleDayStarted()
    {
        // Only active crops
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

        // New day begins: reset watered requirement
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
        // Prevent ¡°state changed but visuals didn't¡± due to missing inspector references.
        if (emptyVisual == null) emptyVisual = transform.Find("EmptyVisual")?.gameObject;
        if (plantedVisual == null) plantedVisual = transform.Find("PlantedVisual")?.gameObject;
        if (wateredVisual == null) wateredVisual = transform.Find("WateredVisual")?.gameObject;
        if (matureVisual == null) matureVisual = transform.Find("MatureVisual")?.gameObject;
    }
}
