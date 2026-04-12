using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Single authority for quest runtime state, persistence, and HUD. Subscribes to <see cref="GameplayEventHub"/> only (no Update polling for progress).</summary>
[DisallowMultipleComponent]
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    const string PrefsLocalKey = "QuestRuntimeState_v1";

    [Header("UI")]
    public PlaytestObjectiveHUD objectiveHUD;
    public WaveEventBannerHUD bannerHUD;

    [Header("Refs (reconcile / wave)")]
    public WaveProgressTracker waveProgress;
    public PlayerResourceInventory inventory;

    [Header("Behavior")]
    public bool disableWaveAutoVictoryWhileActive = true;
    public bool hideObjectiveOnGameEnd = true;
    [Tooltip("Banner when an objective completes.")]
    public bool showBannerOnObjectiveComplete = true;
    [Tooltip("Banner when the whole quest completes.")]
    public bool showBannerOnQuestComplete = true;

    [Header("Debug (read-only at runtime)")]
    [SerializeField] private string debugQuestSummary;
    [SerializeField] private string debugProgressLine;
    public bool debugLogEachHudRefresh;

    private QuestDefinition _questDef;
    private QuestRuntimeState _state;
    private bool _hubSubscribed;
    private bool _storedWaveAutoVictory;
    private bool _storedWaveAutoVictoryValid;
    private bool _questVictoryEmitted;

    public QuestDefinition ActiveQuest => _questDef;
    public QuestRuntimeState State => _state;
    public bool HasActiveQuest => _state != null && _state.active && !_state.completed && !_state.failed;

    /// <summary>Lets legacy adapters (e.g. <see cref="PlaytestMilestoneController"/>) mirror <c>isCompleted</c> without polling.</summary>
    public event Action OnAfterQuestStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (objectiveHUD == null)
            objectiveHUD = FindFirstObjectByType<PlaytestObjectiveHUD>(FindObjectsInactive.Include);
        if (inventory == null)
            inventory = PlayerResourceInventory.Instance != null ? PlayerResourceInventory.Instance : FindFirstObjectByType<PlayerResourceInventory>();
        if (waveProgress == null)
            waveProgress = FindFirstObjectByType<WaveProgressTracker>();
    }

    private void OnEnable()
    {
        SubscribeHub();
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnGameEnded += HandleGameEnded;
    }

    private void OnDisable()
    {
        UnsubscribeHub();
        RestoreWaveAutoVictoryOverride();
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.OnGameEnded -= HandleGameEnded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ConfigureFromPlaytestBridge(PlaytestObjectiveHUD hud, WaveProgressTracker wave, PlayerResourceInventory inv,
        bool disableAutoVictory, bool hideHudOnEnd, WaveEventBannerHUD banner = null)
    {
        if (hud != null) objectiveHUD = hud;
        if (wave != null) waveProgress = wave;
        if (inv != null) inventory = inv;
        if (banner != null) bannerHUD = banner;
        disableWaveAutoVictoryWhileActive = disableAutoVictory;
        hideObjectiveOnGameEnd = hideHudOnEnd;
    }

    /// <summary>Starts or resumes a quest from definition (e.g. SO or legacy factory). Call from <see cref="PlaytestMilestoneController"/>.</summary>
    public void BeginQuest(QuestDefinition definition)
    {
        _questDef = definition;
        _questVictoryEmitted = false;

        if (definition == null || definition.objectives == null || definition.objectives.Count == 0)
        {
            RestoreWaveAutoVictoryOverride();
            _state = null;
            RefreshHUD();
            return;
        }

        if (TryLoadState(definition.questId, out var loaded) && loaded != null)
        {
            _state = loaded;
            SanitizeStateAgainstDefinition();
        }
        else
            _state = QuestRuntimeState.CreateNew(definition);

        ReconcileWithWorld();
        ApplyWaveAutoVictoryOverride();
        if (inventory != null)
            inventory.ResetQuestCollectBaseline();
        RefreshHUD();
        SaveState();
    }

    private bool TryLoadState(string questId, out QuestRuntimeState state)
    {
        state = null;
        string key = BaseWorldSession.ScopePlayerPrefsKey(PrefsLocalKey);
        if (!PlayerPrefs.HasKey(key)) return false;

        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            var dto = JsonUtility.FromJson<QuestPersistDto>(json);
            if (dto == null || dto.questId != questId) return false;

            state = new QuestRuntimeState
            {
                questId = dto.questId,
                active = dto.active,
                completed = dto.completed,
                failed = dto.failed,
                failReason = dto.failReason,
                parallelObjectives = dto.parallelObjectives,
                serialObjectiveIndex = dto.serialObjectiveIndex,
                objectives = new List<ObjectiveRuntimeState>()
            };

            if (dto.objectiveIds != null && dto.objectiveProgress != null && dto.objectiveDone != null)
            {
                int n = Mathf.Min(dto.objectiveIds.Length, Mathf.Min(dto.objectiveProgress.Length, dto.objectiveDone.Length));
                for (int i = 0; i < n; i++)
                {
                    state.objectives.Add(new ObjectiveRuntimeState(dto.objectiveIds[i])
                    {
                        currentProgress = dto.objectiveProgress[i],
                        completed = dto.objectiveDone[i]
                    });
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void SaveState()
    {
        if (_state == null || _questDef == null) return;

        var dto = new QuestPersistDto
        {
            questId = _state.questId,
            active = _state.active,
            completed = _state.completed,
            failed = _state.failed,
            failReason = _state.failReason,
            parallelObjectives = _state.parallelObjectives,
            serialObjectiveIndex = _state.serialObjectiveIndex,
            objectiveIds = new string[_state.objectives.Count],
            objectiveProgress = new int[_state.objectives.Count],
            objectiveDone = new bool[_state.objectives.Count]
        };

        for (int i = 0; i < _state.objectives.Count; i++)
        {
            dto.objectiveIds[i] = _state.objectives[i].objectiveId;
            dto.objectiveProgress[i] = _state.objectives[i].currentProgress;
            dto.objectiveDone[i] = _state.objectives[i].completed;
        }

        string json = JsonUtility.ToJson(dto);
        PlayerPrefs.SetString(BaseWorldSession.ScopePlayerPrefsKey(PrefsLocalKey), json);
        PlayerPrefs.Save();
    }

    private void SanitizeStateAgainstDefinition()
    {
        if (_questDef == null || _state == null) return;

        while (_state.objectives.Count < _questDef.objectives.Count)
        {
            int i = _state.objectives.Count;
            var od = _questDef.objectives[i];
            string oid = od != null && !string.IsNullOrEmpty(od.objectiveId) ? od.objectiveId : $"obj_{i}";
            _state.objectives.Add(new ObjectiveRuntimeState(oid));
        }

        while (_state.objectives.Count > _questDef.objectives.Count)
            _state.objectives.RemoveAt(_state.objectives.Count - 1);

        _state.parallelObjectives = _questDef.parallelObjectives;
    }

    public void ReconcileWithWorld()
    {
        if (_questDef == null || _state == null) return;
        if (inventory == null)
            inventory = PlayerResourceInventory.Instance;

        for (int i = 0; i < _questDef.objectives.Count && i < _state.objectives.Count; i++)
        {
            var def = _questDef.objectives[i];
            var rs = _state.objectives[i];
            if (def == null || rs.completed) continue;

            int req = Mathf.Max(1, def.requiredAmount);

            if (def.type == ObjectiveType.Collect && inventory != null)
            {
                int have = inventory.Get(def.resourceType);
                if (have >= req)
                {
                    rs.currentProgress = req;
                    rs.completed = true;
                }
            }
            else if (def.type == ObjectiveType.Build)
            {
                int built = CountBuiltWaterCollectorsMatching(def.targetId);
                rs.currentProgress = Mathf.Min(req, Mathf.Max(rs.currentProgress, built));
                if (rs.currentProgress >= req)
                    rs.completed = true;
            }
        }

        AdvanceSerialIndexPastCompleted();
        TryCompleteQuestIfAllObjectivesDone();
        RefreshHUD();
    }

    private static int CountBuiltWaterCollectorsMatching(string targetId)
    {
        var all = FindObjectsByType<WaterCollectorBuildSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null) return 0;

        int c = 0;
        for (int i = 0; i < all.Length; i++)
        {
            var w = all[i];
            if (w == null || !w.IsBuilt) continue;
            if (string.IsNullOrEmpty(targetId))
            {
                c++;
                continue;
            }

            if (string.Equals(targetId, w.questStructureId, StringComparison.Ordinal))
                c++;
        }

        return c;
    }

    public void AddObjectiveProgress(QuestRuntimeState qs, int objectiveIndex, int delta, int requiredAmount)
    {
        if (_questDef == null || qs == null || _state != qs) return;
        if (qs.completed || qs.failed || !qs.active) return;
        if (objectiveIndex < 0 || objectiveIndex >= qs.objectives.Count) return;
        if (objectiveIndex >= _questDef.objectives.Count) return;

        if (!IsObjectiveActiveForEvaluation(objectiveIndex))
            return;

        var os = qs.objectives[objectiveIndex];
        if (os.completed) return;

        int req = Mathf.Max(1, requiredAmount);
        os.currentProgress = Mathf.Min(req, os.currentProgress + Mathf.Max(0, delta));

        if (os.currentProgress >= req)
        {
            os.completed = true;
            OnObjectiveCompletedEvent(objectiveIndex);
        }

        AdvanceSerialIndexPastCompleted();
        TryCompleteQuestIfAllObjectivesDone();
        RefreshHUD();
        SaveState();
    }

    private bool IsObjectiveActiveForEvaluation(int index)
    {
        if (_state == null || _questDef == null) return false;
        if (_state.parallelObjectives)
            return index >= 0 && index < _state.objectives.Count && !_state.objectives[index].completed;

        return index == _state.serialObjectiveIndex;
    }

    private void AdvanceSerialIndexPastCompleted()
    {
        if (_state == null || _questDef == null || _state.parallelObjectives) return;

        while (_state.serialObjectiveIndex < _state.objectives.Count &&
               _state.objectives[_state.serialObjectiveIndex].completed)
        {
            _state.serialObjectiveIndex++;
        }
    }

    private void OnObjectiveCompletedEvent(int index)
    {
        if (showBannerOnObjectiveComplete && bannerHUD != null && _questDef != null &&
            index >= 0 && index < _questDef.objectives.Count)
        {
            var od = _questDef.objectives[index];
            string line = od != null && !string.IsNullOrWhiteSpace(od.displayText) ? od.displayText : $"Objective {index + 1} complete";
            bannerHUD.Show(line);
        }

        Debug.Log($"[QuestManager] Objective complete: index={index} quest={_questDef?.questId}");
    }

    private void TryCompleteQuestIfAllObjectivesDone()
    {
        if (_state == null || _questDef == null) return;
        if (_state.completed || _state.failed) return;

        for (int i = 0; i < _state.objectives.Count; i++)
        {
            if (!_state.objectives[i].completed)
                return;
        }

        CompleteQuestVictory();
    }

    private void CompleteQuestVictory()
    {
        if (_state == null || _questVictoryEmitted) return;
        _state.completed = true;
        _state.active = false;
        _questVictoryEmitted = true;

        string reason = _questDef != null && !string.IsNullOrWhiteSpace(_questDef.victoryReason)
            ? _questDef.victoryReason
            : "Quest complete!";

        if (showBannerOnQuestComplete && bannerHUD != null)
            bannerHUD.Show(reason);

        RefreshHUD();
        SaveState();

        if (GameFlowManager.Instance != null && !GameFlowManager.Instance.HasEnded)
            GameFlowManager.Instance.TriggerVictory(reason);
    }

    private void ProcessGameplayEvent(GameplayEvent ev)
    {
        if (_questDef == null || _state == null) return;
        if (_state.completed || _state.failed || !_state.active) return;
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.HasEnded) return;

        if (_state.parallelObjectives)
        {
            for (int i = 0; i < _questDef.objectives.Count; i++)
            {
                if (i >= _state.objectives.Count) break;
                if (_state.objectives[i].completed) continue;
                TryEvaluateObjective(i, ev);
            }
        }
        else
        {
            int i = _state.serialObjectiveIndex;
            if (i >= 0 && i < _questDef.objectives.Count && i < _state.objectives.Count)
                TryEvaluateObjective(i, ev);
        }
    }

    private void TryEvaluateObjective(int index, GameplayEvent ev)
    {
        var def = _questDef.objectives[index];
        if (def == null) return;

        var evaluator = ObjectiveEvaluatorRegistry.Get(def.type);
        evaluator.Evaluate(this, _questDef, _state, index, def, ev);
    }

    private void SubscribeHub()
    {
        if (_hubSubscribed) return;
        GameplayEventHub.OnResourceCollected += HandleResourceCollected;
        GameplayEventHub.OnStructureBuilt += HandleStructureBuilt;
        GameplayEventHub.OnStructureRepaired += HandleStructureRepaired;
        GameplayEventHub.OnEnemyKilled += HandleEnemyKilled;
        GameplayEventHub.OnPlayerEnteredArea += HandlePlayerEnteredArea;
        GameplayEventHub.OnNightSurvived += HandleNightSurvived;
        GameplayEventHub.OnCropPlantedAndWatered += HandleCropPlantedAndWatered;
        _hubSubscribed = true;
    }

    private void UnsubscribeHub()
    {
        if (!_hubSubscribed) return;
        GameplayEventHub.OnResourceCollected -= HandleResourceCollected;
        GameplayEventHub.OnStructureBuilt -= HandleStructureBuilt;
        GameplayEventHub.OnStructureRepaired -= HandleStructureRepaired;
        GameplayEventHub.OnEnemyKilled -= HandleEnemyKilled;
        GameplayEventHub.OnPlayerEnteredArea -= HandlePlayerEnteredArea;
        GameplayEventHub.OnNightSurvived -= HandleNightSurvived;
        GameplayEventHub.OnCropPlantedAndWatered -= HandleCropPlantedAndWatered;
        _hubSubscribed = false;
    }

    private void HandleResourceCollected(ResourceType type, int delta) =>
        ProcessGameplayEvent(new GameplayEvent(GameplayEventKind.ResourceCollected, type, delta));

    private void HandleStructureBuilt(string structureId, int instanceId) =>
        ProcessGameplayEvent(new GameplayEvent(GameplayEventKind.StructureBuilt, intValue: 1, stringId: structureId));

    private void HandleStructureRepaired(string targetId, int amount) =>
        ProcessGameplayEvent(new GameplayEvent(GameplayEventKind.StructureRepaired, intValue: amount, stringId: targetId));

    private void HandleEnemyKilled(string tag, int instanceId) =>
        ProcessGameplayEvent(new GameplayEvent(GameplayEventKind.EnemyKilled, intValue: 1, stringId: tag));

    private void HandlePlayerEnteredArea(string areaId) =>
        ProcessGameplayEvent(new GameplayEvent(GameplayEventKind.PlayerEnteredArea, intValue: 1, stringId: areaId));

    private void HandleNightSurvived() =>
        ProcessGameplayEvent(new GameplayEvent(GameplayEventKind.NightSurvived));

    private void HandleCropPlantedAndWatered(string plotQuestId, string cropId) =>
        ProcessGameplayEvent(new GameplayEvent(GameplayEventKind.CropPlantedAndWatered, stringId: plotQuestId, stringId2: cropId));

    private void HandleGameEnded(GameResult result, string reason)
    {
        if (!hideObjectiveOnGameEnd) return;
        if (objectiveHUD != null) objectiveHUD.SetVisible(false);
    }

    private void ApplyWaveAutoVictoryOverride()
    {
        if (!disableWaveAutoVictoryWhileActive) return;
        if (_state == null || !_state.active || _state.completed || _state.failed) return;
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

    public void RefreshHUD()
    {
        if (objectiveHUD == null) return;

        bool ended = GameFlowManager.Instance != null && GameFlowManager.Instance.HasEnded;
        bool show = _questDef != null && _state != null && _state.active && !_state.completed && !_state.failed && !ended;
        objectiveHUD.SetVisible(show);

        if (_questDef == null || _state == null)
        {
            objectiveHUD.SetTitle("Objective");
            objectiveHUD.SetProgress("No quest.");
            debugQuestSummary = "";
            debugProgressLine = "";
            return;
        }

        objectiveHUD.SetTitle(_questDef.displayTitle);
        string progress = BuildProgressText();
        objectiveHUD.SetProgress(progress);
        debugQuestSummary = $"{_questDef.questId} active={_state.active} done={_state.completed} fail={_state.failed} parallel={_state.parallelObjectives} serialIdx={_state.serialObjectiveIndex}";
        debugProgressLine = progress;

        if (debugLogEachHudRefresh)
            Debug.Log($"[QuestManager] {debugQuestSummary}\n{progress}");

        OnAfterQuestStateChanged?.Invoke();
    }

    private string BuildProgressText()
    {
        if (_questDef == null || _state == null) return "";

        var sb = new StringBuilder();
        for (int i = 0; i < _questDef.objectives.Count && i < _state.objectives.Count; i++)
        {
            var od = _questDef.objectives[i];
            var rs = _state.objectives[i];
            if (od == null) continue;

            if (sb.Length > 0) sb.Append('\n');

            int req = Mathf.Max(1, od.requiredAmount);
            int cur = Mathf.Clamp(rs.currentProgress, 0, req);
            string label = !string.IsNullOrWhiteSpace(od.displayText) ? od.displayText : od.type.ToString();
            string mark = rs.completed ? "[x] " : "[ ] ";
            sb.Append(mark).Append(label).Append(" ").Append(cur).Append("/").Append(req);
        }

        return sb.ToString();
    }

    [Serializable]
    private class QuestPersistDto
    {
        public string questId;
        public bool active;
        public bool completed;
        public bool failed;
        public string failReason;
        public bool parallelObjectives;
        public int serialObjectiveIndex;
        public string[] objectiveIds;
        public int[] objectiveProgress;
        public bool[] objectiveDone;
    }
}
