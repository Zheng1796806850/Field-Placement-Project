using UnityEngine;

/// <summary>
/// Adapter: starts <see cref="QuestManager"/> from a <see cref="QuestDefinitionSO"/> or legacy <see cref="PlaytestMilestoneSO"/>.
/// All progress and victory flow through <see cref="QuestManager"/> only.
/// </summary>
public class PlaytestMilestoneController : MonoBehaviour
{
    [Header("Quest (new)")]
    public QuestDefinitionSO questDefinition;

    [Header("Milestone (legacy)")]
    public PlaytestMilestoneSO milestone;

    [Header("Quest runtime")]
    public QuestManager questManager;

    [Header("Refs (bridge)")]
    public GameStateManager gameState;
    public PlayerResourceInventory inventory;
    public WaveProgressTracker waveProgress;
    public PlaytestObjectiveHUD objectiveHUD;
    public WaveEventBannerHUD bannerHUD;

    [Header("Water Collector (legacy inspector; unused — quest reconcile scans the scene)")]
    public bool autoFindWaterCollectors = true;
    public System.Collections.Generic.List<WaterCollectorBuildSpot> waterCollectors = new System.Collections.Generic.List<WaterCollectorBuildSpot>();

    [Header("Behavior")]
    public bool disableWaveAutoVictoryWhileActive = true;
    public bool hideObjectiveOnGameEnd = true;

    [Header("Compatibility (mirrored from QuestManager)")]
    public bool isActive = true;
    public bool isCompleted;

    private void Awake()
    {
        ResolveRefs();
        EnsureQuestManager();
    }

    private void OnEnable()
    {
        ResolveRefs();
        EnsureQuestManager();

        if (questManager != null)
        {
            questManager.OnAfterQuestStateChanged += MirrorCompletionFromQuest;
            SyncQuestManagerRefs();

            if (!isActive)
                questManager.BeginQuest(null);
            else
                questManager.BeginQuest(ResolveQuestDefinition());
        }

        MirrorCompletionFromQuest();
    }

    private void OnDisable()
    {
        if (questManager != null)
            questManager.OnAfterQuestStateChanged -= MirrorCompletionFromQuest;
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

        if (bannerHUD == null)
            bannerHUD = FindFirstObjectByType<WaveEventBannerHUD>(FindObjectsInactive.Include);
    }

    private void EnsureQuestManager()
    {
        if (questManager != null) return;

        questManager = GetComponent<QuestManager>();
        if (questManager == null)
            questManager = GetComponentInParent<QuestManager>();
        if (questManager == null)
            questManager = GetComponentInChildren<QuestManager>();

        if (questManager == null)
            questManager = FindFirstObjectByType<QuestManager>();

        if (questManager == null)
            questManager = gameObject.AddComponent<QuestManager>();
    }

    private void SyncQuestManagerRefs()
    {
        if (questManager == null) return;

        questManager.ConfigureFromPlaytestBridge(
            objectiveHUD,
            waveProgress,
            inventory,
            disableWaveAutoVictoryWhileActive,
            hideObjectiveOnGameEnd,
            bannerHUD);
    }

    private QuestDefinition ResolveQuestDefinition()
    {
        if (questDefinition != null)
            return questDefinition.ToRuntimeCopy();

        if (milestone != null)
            return PlaytestMilestoneQuestFactory.FromMilestone(milestone);

        return null;
    }

    private void MirrorCompletionFromQuest()
    {
        if (questManager == null || questManager.State == null)
        {
            isCompleted = false;
            return;
        }

        isCompleted = questManager.State.completed;
    }
}
