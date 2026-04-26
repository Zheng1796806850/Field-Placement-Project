using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[DisallowMultipleComponent]
public class BaseNpcQuestGiver : MonoBehaviour, IInteractable
{
    private const string MetOnceKeyLocal = "npc_weapon_quest_met_once";
    private const string QuestCompletedKeyLocal = "npc_weapon_quest_completed";
    private const string RewardGrantedKeyLocal = "npc_weapon_quest_reward_granted";

    private enum DialogueBranch
    {
        None = 0,
        FirstTime = 1,
        MissingItem = 2,
        Completion = 3,
        AlreadyCompleted = 4
    }

    [Header("NPC Identity")]
    public string npcName = "Wanderer";
    public string playerDisplayName = "Player";
    [TextArea] public string promptText = "Talk";
    public int priority = 10;

    [Header("Spawn Schedule")]
    [Min(1)] public int spawnDay = 2;
    public DayNightPhase spawnPhase = DayNightPhase.Day;
    public bool hideAfterCompleted = false;
    [Tooltip("Optional root to toggle for spawn visibility. Avoid assigning this script's own GameObject.")]
    public GameObject npcRootToToggle;

    [Header("Dialogue")]
    public NpcDialoguePanelHUD dialoguePanel;
    [TextArea(2, 5)] public List<string> firstTimeDialogueLines = new List<string>();
    [TextArea(2, 5)] public List<string> missingItemDialogueLines = new List<string>();
    [TextArea(2, 5)] public List<string> completionDialogueLines = new List<string>();
    [TextArea(2, 5)] public List<string> alreadyCompletedDialogueLines = new List<string>();

    [Header("Quest Requirement")]
    public ResourceType requiredResourceType = ResourceType.Flashlight;
    public bool consumeRequiredItemOnComplete = true;

    [Header("Reward")]
    [Min(1f)] public float damageMultiplier = 2f;
    [Tooltip("Optional. Assigned when quest reward is granted (e.g. AnimationController_Axe).")]
    public RuntimeAnimatorController axeUpgradeAnimatorController;

    [Header("Runtime State")]
    [SerializeField] private bool hasMetOnce;
    [SerializeField] private bool questCompleted;
    [SerializeField] private bool rewardGranted;

    private bool _isVisibleBySchedule;
    private DialogueBranch _activeBranch;
    private readonly List<Renderer> _renderers = new List<Renderer>(8);
    private readonly List<Collider2D> _colliders = new List<Collider2D>(8);

    public int Priority => priority;

    private void Awake()
    {
        if (dialoguePanel == null)
            dialoguePanel = NpcDialoguePanelHUD.Instance != null
                ? NpcDialoguePanelHUD.Instance
                : FindFirstObjectByType<NpcDialoguePanelHUD>(FindObjectsInactive.Include);

        CacheVisibilityTargets();
        LoadState();
        SyncVisibilityFromSchedule();
    }

    private void OnEnable()
    {
        var gsm = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (gsm != null)
            gsm.OnPhaseChanged += HandlePhaseChanged;
        SyncVisibilityFromSchedule();
    }

    private void OnDisable()
    {
        var gsm = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (gsm != null)
            gsm.OnPhaseChanged -= HandlePhaseChanged;
    }

    public string GetPrompt()
    {
        if (!_isVisibleBySchedule)
            return string.Empty;
        return string.IsNullOrWhiteSpace(promptText) ? "Talk" : promptText;
    }

    public bool CanInteract(GameObject interactor)
    {
        if (!_isVisibleBySchedule)
            return false;
        if (dialoguePanel == null || dialoguePanel.IsRunning)
            return false;
        if (interactor == null)
            return false;
        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
            return;

        ResolveDialoguePanelIfMissing();
        if (dialoguePanel == null)
            return;

        _activeBranch = ResolveDialogueBranch(interactor);
        IReadOnlyList<string> lines = GetLinesForBranch(_activeBranch);
        dialoguePanel.BeginDialogue(npcName, playerDisplayName, lines, HandleDialogueCompleted);
    }

    private void ResolveDialoguePanelIfMissing()
    {
        if (dialoguePanel == null)
            dialoguePanel = NpcDialoguePanelHUD.Instance != null
                ? NpcDialoguePanelHUD.Instance
                : FindFirstObjectByType<NpcDialoguePanelHUD>(FindObjectsInactive.Include);
    }

    private DialogueBranch ResolveDialogueBranch(GameObject interactor)
    {
        if (!hasMetOnce)
            return DialogueBranch.FirstTime;
        if (questCompleted)
            return DialogueBranch.AlreadyCompleted;

        PlayerResourceInventory inv = ResolveInventory(interactor);
        if (inv != null && inv.Get(requiredResourceType) > 0)
            return DialogueBranch.Completion;

        return DialogueBranch.MissingItem;
    }

    private IReadOnlyList<string> GetLinesForBranch(DialogueBranch branch)
    {
        switch (branch)
        {
            case DialogueBranch.FirstTime:
                return firstTimeDialogueLines;
            case DialogueBranch.MissingItem:
                return missingItemDialogueLines;
            case DialogueBranch.Completion:
                return completionDialogueLines;
            case DialogueBranch.AlreadyCompleted:
                return alreadyCompletedDialogueLines;
            default:
                return missingItemDialogueLines;
        }
    }

    private void HandleDialogueCompleted()
    {
        switch (_activeBranch)
        {
            case DialogueBranch.FirstTime:
                hasMetOnce = true;
                break;

            case DialogueBranch.Completion:
                hasMetOnce = true;
                questCompleted = true;
                TryConsumeQuestItem();
                TryGrantReward();
                break;

            case DialogueBranch.MissingItem:
            case DialogueBranch.AlreadyCompleted:
                break;
        }

        SaveState();
        SyncVisibilityFromSchedule();
        _activeBranch = DialogueBranch.None;
    }

    private void TryConsumeQuestItem()
    {
        if (!consumeRequiredItemOnComplete)
            return;
        PlayerResourceInventory inv = ResolveInventory(null);
        if (inv == null)
            return;
        inv.Spend(requiredResourceType, 1);
        inv.SaveInMemory();
    }

    private void TryGrantReward()
    {
        if (rewardGranted)
            return;

        PlayerCombat2D combat = FindFirstObjectByType<PlayerCombat2D>(FindObjectsInactive.Include);
        if (combat != null)
            combat.GrantAxeUpgrade(damageMultiplier, axeUpgradeAnimatorController);

        // Keep requested persistence keys as source of truth even if combat object is not loaded yet.
        PlayerPrefs.SetInt(Scoped("player_axe_upgrade_granted"), 1);
        PlayerPrefs.SetString(Scoped("player_axe_damage_multiplier"), damageMultiplier.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        rewardGranted = true;
    }

    private PlayerResourceInventory ResolveInventory(GameObject interactor)
    {
        var inv = interactor != null ? interactor.GetComponentInParent<PlayerResourceInventory>() : null;
        if (inv != null) return inv;
        return PlayerResourceInventory.Instance != null ? PlayerResourceInventory.Instance : FindFirstObjectByType<PlayerResourceInventory>(FindObjectsInactive.Include);
    }

    private void HandlePhaseChanged(DayNightPhase _)
    {
        SyncVisibilityFromSchedule();
    }

    private void SyncVisibilityFromSchedule()
    {
        var gsm = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        bool visible = false;
        if (gsm != null)
        {
            bool schedulePassed = gsm.CurrentDay >= Mathf.Max(1, spawnDay) && gsm.CurrentPhase == spawnPhase;
            visible = schedulePassed;
        }

        if (hideAfterCompleted && questCompleted)
            visible = false;

        SetNpcVisible(visible);
    }

    private void SetNpcVisible(bool visible)
    {
        _isVisibleBySchedule = visible;

        if (npcRootToToggle != null && npcRootToToggle != gameObject)
        {
            if (npcRootToToggle.activeSelf != visible)
                npcRootToToggle.SetActive(visible);
            return;
        }

        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].enabled = visible;
        }

        for (int i = 0; i < _colliders.Count; i++)
        {
            if (_colliders[i] != null)
                _colliders[i].enabled = visible;
        }
    }

    private void CacheVisibilityTargets()
    {
        _renderers.Clear();
        _colliders.Clear();

        Renderer[] rs = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rs.Length; i++)
            _renderers.Add(rs[i]);

        Collider2D[] cs = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cs.Length; i++)
            _colliders.Add(cs[i]);
    }

    private string Scoped(string localKey) => BaseWorldSession.ScopePlayerPrefsKey(localKey);

    private void SaveState()
    {
        PlayerPrefs.SetInt(Scoped(MetOnceKeyLocal), hasMetOnce ? 1 : 0);
        PlayerPrefs.SetInt(Scoped(QuestCompletedKeyLocal), questCompleted ? 1 : 0);
        PlayerPrefs.SetInt(Scoped(RewardGrantedKeyLocal), rewardGranted ? 1 : 0);
        if (rewardGranted)
        {
            PlayerPrefs.SetInt(Scoped("player_axe_upgrade_granted"), 1);
            PlayerPrefs.SetString(Scoped("player_axe_damage_multiplier"), damageMultiplier.ToString(CultureInfo.InvariantCulture));
        }
        PlayerPrefs.Save();
    }

    private void LoadState()
    {
        hasMetOnce = PlayerPrefs.GetInt(Scoped(MetOnceKeyLocal), 0) == 1;
        questCompleted = PlayerPrefs.GetInt(Scoped(QuestCompletedKeyLocal), 0) == 1;
        rewardGranted = PlayerPrefs.GetInt(Scoped(RewardGrantedKeyLocal), 0) == 1;

        // Backward consistency with requested keys.
        if (!rewardGranted)
            rewardGranted = PlayerPrefs.GetInt(Scoped("player_axe_upgrade_granted"), 0) == 1;
    }
}

