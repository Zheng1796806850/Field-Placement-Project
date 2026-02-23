using System;
using System.Collections.Generic;
using UnityEngine;

public class PlaytestMilestoneController : MonoBehaviour
{
    [Header("Milestone")]
    public PlaytestMilestoneSO milestone;

    [Header("Refs")]
    public GameStateManager gameState;
    public PlayerResourceInventory inventory;
    public WaveProgressTracker waveProgress;
    public PlaytestObjectiveHUD objectiveHUD;

    [Header("Water Collector Detection")]
    public bool autoFindWaterCollectors = true;
    public List<WaterCollectorBuildSpot> waterCollectors = new List<WaterCollectorBuildSpot>();

    [Header("Behavior")]
    public bool disableWaveAutoVictoryWhileActive = true;
    public bool hideObjectiveOnGameEnd = true;

    [Header("Runtime")]
    public bool isActive = true;
    public bool isCompleted = false;

    public int nightsSurvived = 0;
    public int nightsSurvivedAfterBuild = 0;

    public int totalFoodCollected = 0;
    public int totalPlanksCollected = 0;

    public int builtWells = 0;

    private bool _subscribed;
    private bool _nightWasActive;
    private bool _buildMetAtNightStart;
    private readonly Dictionary<ResourceType, int> _lastAmounts = new Dictionary<ResourceType, int>();

    private bool _storedWaveAutoVictory;
    private bool _storedWaveAutoVictoryValid;

    private void Awake()
    {
        ResolveRefs();
        RefreshCollectorsIfNeeded();
        RecountBuiltWells();
        InitLastAmounts();
        RefreshHUD();
    }

    private void OnEnable()
    {
        ResolveRefs();
        RefreshCollectorsIfNeeded();
        RecountBuiltWells();
        InitLastAmounts();
        Subscribe();
        ApplyWaveAutoVictoryOverride();
        RefreshHUD();
    }

    private void OnDisable()
    {
        Unsubscribe();
        RestoreWaveAutoVictoryOverride();
    }

    private void ResolveRefs()
    {
        if (gameState == null)
            gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();

        if (inventory == null)
            inventory = PlayerResourceInventory.Instance != null ? PlayerResourceInventory.Instance : FindFirstObjectByType<PlayerResourceInventory>();

        if (waveProgress == null)
            waveProgress = FindFirstObjectByType<WaveProgressTracker>();

        if (objectiveHUD == null)
            objectiveHUD = FindFirstObjectByType<PlaytestObjectiveHUD>(FindObjectsInactive.Include);
    }

    private void RefreshCollectorsIfNeeded()
    {
        if (!autoFindWaterCollectors) return;

        waterCollectors.Clear();
        var all = FindObjectsByType<WaterCollectorBuildSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null) return;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null) waterCollectors.Add(all[i]);
        }
    }

    private void RecountBuiltWells()
    {
        int count = 0;
        for (int i = 0; i < waterCollectors.Count; i++)
        {
            var c = waterCollectors[i];
            if (c == null) continue;
            if (c.IsBuilt) count++;
        }
        builtWells = count;
    }

    private void InitLastAmounts()
    {
        _lastAmounts.Clear();
        if (inventory == null) return;

        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
        {
            _lastAmounts[t] = inventory.Get(t);
        }
    }

    private void Subscribe()
    {
        if (_subscribed) return;

        if (gameState != null)
        {
            gameState.OnNightStarted += HandleNightStarted;
            gameState.OnDayStarted += HandleDayStarted;
        }

        if (inventory != null)
        {
            inventory.OnResourceChanged += HandleResourceChanged;
        }

        for (int i = 0; i < waterCollectors.Count; i++)
        {
            var c = waterCollectors[i];
            if (c == null) continue;
            c.OnBuiltChanged += HandleCollectorBuiltChanged;
        }

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.OnGameEnded += HandleGameEnded;
        }

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;

        if (gameState != null)
        {
            gameState.OnNightStarted -= HandleNightStarted;
            gameState.OnDayStarted -= HandleDayStarted;
        }

        if (inventory != null)
        {
            inventory.OnResourceChanged -= HandleResourceChanged;
        }

        for (int i = 0; i < waterCollectors.Count; i++)
        {
            var c = waterCollectors[i];
            if (c == null) continue;
            c.OnBuiltChanged -= HandleCollectorBuiltChanged;
        }

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.OnGameEnded -= HandleGameEnded;
        }

        _subscribed = false;
    }

    private void ApplyWaveAutoVictoryOverride()
    {
        if (!disableWaveAutoVictoryWhileActive) return;
        if (!isActive) return;
        if (waveProgress == null) return;
        if (_storedWaveAutoVictoryValid) return;

        _storedWaveAutoVictory = waveProgress.enableAutoVictoryOnDayStart;
        _storedWaveAutoVictoryValid = true;
        waveProgress.enableAutoVictoryOnDayStart = false;
    }

    private void RestoreWaveAutoVictoryOverride()
    {
        if (!_storedWaveAutoVictoryValid) return;
        if (waveProgress != null)
            waveProgress.enableAutoVictoryOnDayStart = _storedWaveAutoVictory;

        _storedWaveAutoVictoryValid = false;
    }

    private void HandleNightStarted()
    {
        if (!isActive || isCompleted) return;

        _nightWasActive = true;
        _buildMetAtNightStart = IsBuildRequirementMet();
        RefreshHUD();
    }

    private void HandleDayStarted()
    {
        if (!isActive || isCompleted) return;

        if (_nightWasActive)
        {
            _nightWasActive = false;
            nightsSurvived = Mathf.Max(0, nightsSurvived + 1);

            if (_buildMetAtNightStart)
                nightsSurvivedAfterBuild = Mathf.Max(0, nightsSurvivedAfterBuild + 1);
        }

        EvaluateCompletion();
        RefreshHUD();
    }

    private void HandleResourceChanged(ResourceType type, int amount)
    {
        if (!isActive || isCompleted) return;

        if (!_lastAmounts.TryGetValue(type, out int last))
            last = amount;

        int delta = amount - last;
        _lastAmounts[type] = amount;

        if (delta > 0)
        {
            if (type == ResourceType.Food) totalFoodCollected += delta;
            if (type == ResourceType.Planks) totalPlanksCollected += delta;
        }

        EvaluateCompletion();
        RefreshHUD();
    }

    private void HandleCollectorBuiltChanged(bool built)
    {
        if (!isActive || isCompleted) return;

        RecountBuiltWells();
        EvaluateCompletion();
        RefreshHUD();
    }

    private void HandleGameEnded(GameResult result, string reason)
    {
        if (!hideObjectiveOnGameEnd) return;
        if (objectiveHUD != null) objectiveHUD.SetVisible(false);
    }

    private bool IsBuildRequirementMet()
    {
        int req = milestone != null ? Mathf.Max(1, milestone.requiredBuiltWells) : 1;
        return builtWells >= req;
    }

    private void EvaluateCompletion()
    {
        if (isCompleted) return;
        if (!isActive) return;
        if (milestone == null) return;

        switch (milestone.type)
        {
            case PlaytestMilestoneSO.MilestoneType.SurviveNights:
                {
                    int req = Mathf.Max(1, milestone.requiredNights);
                    if (nightsSurvived >= req) Complete();
                    break;
                }
            case PlaytestMilestoneSO.MilestoneType.GatherFoodAndPlanks:
                {
                    int reqFood = Mathf.Max(0, milestone.requiredFood);
                    int reqPlanks = Mathf.Max(0, milestone.requiredPlanks);
                    if (totalFoodCollected >= reqFood && totalPlanksCollected >= reqPlanks) Complete();
                    break;
                }
            case PlaytestMilestoneSO.MilestoneType.BuildWaterCollectorAndSurvive:
                {
                    int reqWell = Mathf.Max(1, milestone.requiredBuiltWells);
                    int reqNight = Mathf.Max(1, milestone.requiredNightsAfterBuild);
                    if (builtWells >= reqWell && nightsSurvivedAfterBuild >= reqNight) Complete();
                    break;
                }
        }
    }

    private void Complete()
    {
        if (isCompleted) return;
        isCompleted = true;

        string reason = milestone != null && !string.IsNullOrWhiteSpace(milestone.victoryReason)
            ? milestone.victoryReason
            : "Milestone achieved!";

        RefreshHUD();
        GameFlowManager.Instance?.TriggerVictory(reason);
    }

    private void RefreshHUD()
    {
        if (objectiveHUD == null) return;

        bool ended = GameFlowManager.Instance != null && GameFlowManager.Instance.HasEnded;
        objectiveHUD.SetVisible(isActive && !ended && !isCompleted);

        if (milestone == null)
        {
            objectiveHUD.SetTitle("Objective");
            objectiveHUD.SetProgress("No milestone assigned.");
            return;
        }

        string title = !string.IsNullOrWhiteSpace(milestone.objectiveTitle) ? milestone.objectiveTitle : "Objective";
        objectiveHUD.SetTitle(title);
        objectiveHUD.SetProgress(BuildProgressText());
    }

    private string BuildProgressText()
    {
        if (milestone == null) return "";

        switch (milestone.type)
        {
            case PlaytestMilestoneSO.MilestoneType.SurviveNights:
                {
                    int req = Mathf.Max(1, milestone.requiredNights);
                    int cur = Mathf.Clamp(nightsSurvived, 0, req);
                    return $"Survive {req} Night(s): {cur}/{req}";
                }
            case PlaytestMilestoneSO.MilestoneType.GatherFoodAndPlanks:
                {
                    int reqFood = Mathf.Max(0, milestone.requiredFood);
                    int reqPlanks = Mathf.Max(0, milestone.requiredPlanks);
                    int curFood = Mathf.Clamp(totalFoodCollected, 0, reqFood);
                    int curPlanks = Mathf.Clamp(totalPlanksCollected, 0, reqPlanks);
                    return $"Gather Food {curFood}/{reqFood} | Planks {curPlanks}/{reqPlanks}";
                }
            case PlaytestMilestoneSO.MilestoneType.BuildWaterCollectorAndSurvive:
                {
                    int reqWell = Mathf.Max(1, milestone.requiredBuiltWells);
                    int reqNight = Mathf.Max(1, milestone.requiredNightsAfterBuild);
                    int curWell = Mathf.Clamp(builtWells, 0, reqWell);
                    int curNight = Mathf.Clamp(nightsSurvivedAfterBuild, 0, reqNight);
                    return $"Build Well {curWell}/{reqWell} | Survive Night {curNight}/{reqNight}";
                }
        }

        return "";
    }
}