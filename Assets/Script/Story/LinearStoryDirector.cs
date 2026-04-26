using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>线性剧情导演：以协程 + 事件订阅推进，不在单文件巨型 Update 中堆叠全部流程。</summary>
[DisallowMultipleComponent]
public class LinearStoryDirector : MonoBehaviour
{
    public static LinearStoryDirector Instance { get; private set; }

    public static void NotifyAreaPresence(string areaId, bool inside)
    {
        if (Instance == null || string.IsNullOrEmpty(areaId)) return;
        Instance.ApplyAreaPresence(areaId, inside);
    }

    [Header("Master")]
    [Tooltip("关闭时本组件不驱动剧情（正式场景可勾上）。")]
    public bool enableLinearStory = true;

    [Header("Refs (optional auto-find)")]
    public GameStateManager gameStateManager;
    public QuestManager questManager;
    public NpcDialoguePanelHUD dialogueHud;
    public StoryFadeController storyFade;
    public PlayerMovementController playerMovement;
    public PlayerCombat2D playerCombat;
    public PlayerInteractor2D playerInteractor;
    public PlayerResourceInventory playerInventory;
    public BackpackPanelHUD backpackHud;
    public PauseMenuController pauseMenuController;

    [Header("Story area ids (must match triggers / volumes)")]
    public string frontYardPresenceAreaId = "story_front_yard";
    public string backyardPresenceAreaId = "story_backyard";

    [Header("Quest assets (QuestDefinitionSO)")]
    public QuestDefinitionSO questBuildWell;
    public QuestDefinitionSO questTillSoil;
    public QuestDefinitionSO questWaterOnce;
    public QuestDefinitionSO questSurviveFirstNight;
    public QuestDefinitionSO questVisitBackyard;
    public QuestDefinitionSO questTownFetchSupplies;

    [Header("Day2 reward")]
    [Min(1f)] public float day2AxeDamageMultiplier = 2f;
    public RuntimeAnimatorController day2AxeAnimatorOverride;

    [Header("Fade")]
    [Min(0.05f)] public float openingBlackHoldSeconds = 0.35f;
    [Min(0.05f)] public float openingFadeInSeconds = 1.25f;

    [Header("TMP (optional)")]
    public TMP_FontAsset storyDialogueFont;

    [Header("Deny messages (match ZoneTeleportTrigger2D gates)")]
    [TextArea] public string denyFrontYardThirsty = "I'm really thirsty right now, so I guess I'll dig the well first.";
    [TextArea] public string denyFrontYardFarm = "I should plant some crops first";
    [TextArea] public string denyFrontYardWater = "I should water the crops first.";
    [TextArea] public string denyTownBackyard = "Something's going on in the backyard—I should check it first.";
    [TextArea] public string denyPlantingBeforePlantQuest = "I'm really thirsty right now, so I guess I'll dig the well first.";

    [Header("Step pacing")]
    [Min(0.1f)] public float day1InitialMovementSeconds = 2f;

    int _checkpoint;
    bool _day2HandoffGranted;
    bool _bootStarted;
    bool _inFrontYard;
    bool _inBackyard;
    float _moveSecondsCp1;
    float _moveSecondsFrontNight;
    bool _step8IntroStarted;
    bool _processingStep8;
    Coroutine _bootRoutine;
    bool _stepBusy;
    bool _day2MorningLaunched;
    bool _pitInteractBusy;
    bool _openingUiRevealed;

    public bool IsLinearStoryActive => enableLinearStory && isActiveAndEnabled;
    public int CurrentCheckpoint => _checkpoint;
    public bool IsDay2PitInteractablePhase => IsLinearStoryActive && _checkpoint >= 8;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (!enableLinearStory)
        {
            enabled = false;
            return;
        }

        ResolveRefs();
        ApplyStoryFontIfAny();
        _checkpoint = StoryProgressStore.LoadCheckpoint(0);
        _day2HandoffGranted = StoryProgressStore.LoadDay2HandoffComplete();
        if (_checkpoint == 0 && storyFade != null)
            storyFade.SnapToBlack();
        ApplyRestrictionForCheckpoint();
        if (_checkpoint >= 7)
            _day2MorningLaunched = true;

        if (gameStateManager != null)
            gameStateManager.SetStoryClockFrozen(_checkpoint > 0 && _checkpoint < 5);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (!enableLinearStory) return;
        if (_bootStarted) return;
        _bootStarted = true;
        _bootRoutine = StartCoroutine(BootstrapRoutine());
    }

    void OnEnable()
    {
        if (!enableLinearStory) return;
        if (_checkpoint == 0 && storyFade != null)
            storyFade.SnapToBlack();
        GameplayEventHub.OnStructureBuilt += HandleStructureBuiltGlobal;
        GameplayEventHub.OnCropPlanted += HandleCropPlantedGlobal;
        GameplayEventHub.OnPlotWatered += HandlePlotWateredGlobal;
        GameplayEventHub.OnPlayerEnteredArea += HandlePlayerEnteredAreaGlobal;
        GameplayEventHub.OnNightSurvived += HandleNightSurvivedGlobal;

        if (gameStateManager == null)
            gameStateManager = GameStateManager.Instance;
    }

    void OnDisable()
    {
        GameplayEventHub.OnStructureBuilt -= HandleStructureBuiltGlobal;
        GameplayEventHub.OnCropPlanted -= HandleCropPlantedGlobal;
        GameplayEventHub.OnPlotWatered -= HandlePlotWateredGlobal;
        GameplayEventHub.OnPlayerEnteredArea -= HandlePlayerEnteredAreaGlobal;
        GameplayEventHub.OnNightSurvived -= HandleNightSurvivedGlobal;

        if (Instance == this)
            StoryRestrictionGate.ClearAll();
    }

    void Update()
    {
        if (!IsLinearStoryActive) return;
        if (dialogueHud != null && dialogueHud.IsRunning) return;

        TickMovementAccumulation(Time.deltaTime);
    }

    /// <summary>Ensures the main camera follow rig snaps to the player after spawn routing, so the view does not sit at world origin during the opening.</summary>
    void SnapMainCameraToStoryPlayer()
    {
        if (playerMovement == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        var follow = cam.GetComponent<CameraFollowBounds2D>();
        if (follow == null) return;
        if (follow.target == null)
            follow.target = playerMovement.transform;
        follow.SnapToFollowTarget();
    }

    void ResolveRefs()
    {
        if (gameStateManager == null) gameStateManager = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (questManager == null) questManager = QuestManager.Instance != null ? QuestManager.Instance : FindFirstObjectByType<QuestManager>();
        if (dialogueHud == null) dialogueHud = NpcDialoguePanelHUD.Instance != null ? NpcDialoguePanelHUD.Instance : FindFirstObjectByType<NpcDialoguePanelHUD>(FindObjectsInactive.Include);
        if (storyFade == null) storyFade = GetComponentInChildren<StoryFadeController>(true);
        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovementController>();
        if (playerCombat == null) playerCombat = FindFirstObjectByType<PlayerCombat2D>();
        if (playerInteractor == null) playerInteractor = FindFirstObjectByType<PlayerInteractor2D>(FindObjectsInactive.Include);
        if (playerInventory == null) playerInventory = PlayerResourceInventory.Instance != null ? PlayerResourceInventory.Instance : FindFirstObjectByType<PlayerResourceInventory>();
        if (backpackHud == null) backpackHud = FindFirstObjectByType<BackpackPanelHUD>(FindObjectsInactive.Include);
        if (pauseMenuController == null) pauseMenuController = FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);
    }

    void ApplyStoryFontIfAny()
    {
        if (storyDialogueFont == null || dialogueHud == null) return;
        if (dialogueHud.npcNameText != null) dialogueHud.npcNameText.font = storyDialogueFont;
        if (dialogueHud.dialogueText != null) dialogueHud.dialogueText.font = storyDialogueFont;
    }

    void ApplyAreaPresence(string areaId, bool inside)
    {
        if (string.Equals(areaId, frontYardPresenceAreaId, StringComparison.Ordinal))
            _inFrontYard = inside;
        else if (string.Equals(areaId, backyardPresenceAreaId, StringComparison.Ordinal))
            _inBackyard = inside;
    }

    IEnumerator BootstrapRoutine()
    {
        yield return null;
        ResolveRefs();
        SnapMainCameraToStoryPlayer();

        if (_checkpoint == 0)
        {
            yield return RunOpeningSequence();
            _checkpoint = 1;
            Persist();
        }
        else
            ApplyRestrictionForCheckpoint();

        SnapMainCameraToStoryPlayer();

        if (gameStateManager != null)
        {
            bool freeze = _checkpoint > 0 && _checkpoint < 5;
            gameStateManager.SetStoryClockFrozen(freeze);
        }

        TryScheduleDay2MorningIfNeeded();
    }

    void TickMovementAccumulation(float dt)
    {
        if (playerMovement == null) return;
        var rb = playerMovement.GetComponent<Rigidbody2D>();
        bool moving = rb != null && rb.linearVelocity.sqrMagnitude > 0.02f;

        if (_checkpoint == 1 && moving)
            _moveSecondsCp1 += dt;

        if (_checkpoint == 5 && _inFrontYard && moving)
            _moveSecondsFrontNight += dt;

        if (_checkpoint == 1 && _moveSecondsCp1 >= day1InitialMovementSeconds)
        {
            _moveSecondsCp1 = 0f;
            _stepBusy = true;
            StartCoroutine(RunStep2AfterMoveRoutine());
        }

        if (_checkpoint == 5 && _moveSecondsFrontNight >= 1f)
        {
            _moveSecondsFrontNight = 0f;
            _stepBusy = true;
            StartCoroutine(RunStep6FrontYardRoutine());
        }
    }

    IEnumerator RunOpeningSequence()
    {
        ResolveRefs();
        SnapMainCameraToStoryPlayer();
        pauseMenuController?.PushExternalPauseBlock();
        try
        {
            if (gameStateManager != null)
            {
                gameStateManager.SetStoryClockFrozen(true);
            }

            StoryRestrictionGate.ClearAll();
            StoryRestrictionGate.SetFrontYardBlocked(true, denyFrontYardThirsty);
            StoryRestrictionGate.SetTownBlocked(false, denyTownBackyard);

            if (questManager != null)
                questManager.ClearActiveQuestAndDeleteSave();

            LockPlayerHard();
            LockBackpackToggle(true);

            if (storyFade != null)
            {
                storyFade.SnapToBlack();
                float hold = Mathf.Max(0f, openingBlackHoldSeconds);
                float t = 0f;
                while (t < hold)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return storyFade.FadeFromBlack(openingFadeInSeconds);
            }

            SnapMainCameraToStoryPlayer();
            HideDialogueTargetsForOpening();
            yield return RunStoryDialogueLines("Narrator", "Survivor", StoryLines.Step1, freezeTimeScale: false);
            RevealDialogueTargetsAfterOpening();

            UnlockPlayerHard();
            LockBackpackToggle(false);
        }
        finally
        {
            pauseMenuController?.PopExternalPauseBlock();
        }
    }

    IEnumerator RunStep2AfterMoveRoutine()
    {
        if (_checkpoint != 1)
        {
            _stepBusy = false;
            yield break;
        }

        _checkpoint = -1;
        yield return RunStoryDialogueLines("Narrator", "Survivor", StoryLines.Step2_PreGrowl, freezeTimeScale: true);
        SfxPlayer.TryPlay(SfxId.Story_DistantGrowl, playerMovement != null ? playerMovement.transform.position : Vector3.zero);
        yield return RunStoryDialogueLines("Narrator", "Survivor", StoryLines.Step2_GrowlLineOnly, freezeTimeScale: true);
        yield return RunStoryDialogueLines("Narrator", "Survivor", StoryLines.Step2_PostGrowl, freezeTimeScale: true);
        BeginQuestOrLog(questBuildWell, "questBuildWell");
        StoryRestrictionGate.SetFrontYardBlocked(true, denyFrontYardThirsty);
        _checkpoint = 2;
        Persist();
        _stepBusy = false;
    }

    void HandleStructureBuiltGlobal(string structureId, int _)
    {
        if (!IsLinearStoryActive) return;
        if (_stepBusy) return;
        if (_checkpoint != 2) return;
        if (!string.Equals(structureId, WaterCollectorQuestIds.StructureId, StringComparison.Ordinal))
            return;

        StartCoroutine(RunStep3WellBuiltRoutine());
    }

    IEnumerator RunStep3WellBuiltRoutine()
    {
        if (_checkpoint != 2) yield break;
        _checkpoint = -1;
        yield return RunStoryDialogueLines("Narrator", "Survivor", StoryLines.Step3, freezeTimeScale: false);
        BeginQuestOrLog(questTillSoil, "questTillSoil");
        StoryRestrictionGate.SetFrontYardBlocked(true, denyFrontYardFarm);
        _checkpoint = 3;
        Persist();
    }

    void HandleCropPlantedGlobal(string _)
    {
        if (!IsLinearStoryActive) return;
        if (_stepBusy) return;
        if (_checkpoint != 3) return;
        StartCoroutine(RunStep4PlantedRoutine());
    }

    IEnumerator RunStep4PlantedRoutine()
    {
        if (_checkpoint != 3) yield break;
        _checkpoint = -1;
        yield return RunStoryDialogueLines("Narrator", "Survivor", StoryLines.Step4, freezeTimeScale: false);
        BeginQuestOrLog(questWaterOnce, "questWaterOnce");
        StoryRestrictionGate.SetFrontYardBlocked(true, denyFrontYardWater);
        _checkpoint = 4;
        Persist();
    }

    void HandlePlotWateredGlobal(string _)
    {
        if (!IsLinearStoryActive) return;
        if (_stepBusy) return;
        if (_checkpoint != 4) return;
        StartCoroutine(RunStep5WateredRoutine());
    }

    IEnumerator RunStep5WateredRoutine()
    {
        if (_checkpoint != 4) yield break;
        _checkpoint = -1;
        yield return RunStoryDialogueLines("Narrator", "Survivor", StoryLines.Step5, freezeTimeScale: false);

        BeginQuestOrLog(questSurviveFirstNight, "questSurviveFirstNight");
        if (gameStateManager != null)
        {
            gameStateManager.SetStoryClockFrozen(false);
            gameStateManager.ForceNight();
        }

        StoryRestrictionGate.SetFrontYardBlocked(false, denyFrontYardThirsty);
        StoryRestrictionGate.SetTownBlocked(false, denyTownBackyard);
        _checkpoint = 5;
        Persist();
    }

    IEnumerator RunStep6FrontYardRoutine()
    {
        if (_checkpoint != 5)
        {
            _stepBusy = false;
            yield break;
        }

        _checkpoint = -1;
        yield return RunStoryDialogueLines("Narrator", "Survivor", StoryLines.Step6, freezeTimeScale: true);
        _checkpoint = 6;
        Persist();
        _stepBusy = false;
    }

    void HandleNightSurvivedGlobal()
    {
        if (!IsLinearStoryActive) return;
        TryScheduleDay2MorningIfNeeded();
    }

    void TryScheduleDay2MorningIfNeeded()
    {
        if (_day2MorningLaunched) return;
        if (_checkpoint != 6) return;
        if (gameStateManager == null) return;
        if (gameStateManager.CurrentDay < 2) return;
        if (gameStateManager.CurrentPhase != DayNightPhase.Day) return;

        _day2MorningLaunched = true;
        StartCoroutine(RunStep7Day2MorningRoutine());
    }

    IEnumerator RunStep7Day2MorningRoutine()
    {
        if (_checkpoint != 6) yield break;
        _checkpoint = -1;

        KillRemainingZombiesForStoryCleanup();

        LockPlayerHard();
        LockBackpackToggle(true);
        yield return RunStoryDialogueLines("Narrator", "Survivor", StoryLines.Step7, freezeTimeScale: true);
        UnlockPlayerHard();
        LockBackpackToggle(false);

        BeginQuestOrLog(questVisitBackyard, "questVisitBackyard");
        StoryRestrictionGate.SetTownBlocked(true, denyTownBackyard);
        StoryRestrictionGate.SetFrontYardBlocked(false, denyFrontYardThirsty);
        _checkpoint = 7;
        Persist();
    }

    void HandlePlayerEnteredAreaGlobal(string areaId)
    {
        if (!IsLinearStoryActive) return;
        if (_checkpoint != 7) return;
        if (string.IsNullOrEmpty(areaId)) return;
        if (!string.Equals(areaId, backyardPresenceAreaId, StringComparison.Ordinal)) return;
        if (_step8IntroStarted) return;
        _step8IntroStarted = true;
        StartCoroutine(RunStep8BackyardRoutine());
    }

    IEnumerator RunStep8BackyardRoutine()
    {
        if (_processingStep8) yield break;
        _processingStep8 = true;
        yield return RunStoryDialogueLines("???", "Survivor", StoryLines.Step8, freezeTimeScale: true);
        BeginQuestOrLog(questTownFetchSupplies, "questTownFetchSupplies");
        StoryRestrictionGate.SetTownBlocked(false, denyTownBackyard);
        _checkpoint = 8;
        Persist();
        _processingStep8 = false;
    }

    public void HandleDay2PitInteract(GameObject interactor)
    {
        if (!IsLinearStoryActive) return;
        StartCoroutine(Day2PitRoutine());
    }

    IEnumerator Day2PitRoutine()
    {
        ResolveRefs();
        if (dialogueHud == null || playerInventory == null) yield break;

        if (_pitInteractBusy) yield break;
        _pitInteractBusy = true;

        dialogueHud.freezeTimeScaleDuringDialogue = true;

        try
        {
            if (_day2HandoffGranted || _checkpoint >= 9)
            {
                yield return RunStoryDialogueLines("???", "Survivor", StoryLines.PitSilent, freezeTimeScale: true);
                yield break;
            }

            bool hasMed = playerInventory.Get(ResourceType.AntiInflammatory) >= 1;
            bool hasLight = playerInventory.Get(ResourceType.Flashlight) >= 1;
            if (!hasMed || !hasLight)
            {
                yield return RunStoryDialogueLines("???", "Survivor", StoryLines.PitSilent, freezeTimeScale: true);
                yield break;
            }

            if (!playerInventory.Spend(ResourceType.AntiInflammatory, 1))
                yield break;
            if (!playerInventory.Spend(ResourceType.Flashlight, 1))
            {
                playerInventory.Add(ResourceType.AntiInflammatory, 1);
                yield break;
            }

            if (playerCombat != null)
                playerCombat.GrantAxeUpgrade(day2AxeDamageMultiplier, day2AxeAnimatorOverride);

            yield return RunStoryDialogueLines("???", "Survivor", StoryLines.PitReward, freezeTimeScale: true);

            _day2HandoffGranted = true;
            _checkpoint = 9;
            Persist();
        }
        finally
        {
            _pitInteractBusy = false;
        }
    }

    IEnumerator RunStoryDialogueLines(string npcName, string playerDisplayName, IReadOnlyList<string> lines, bool freezeTimeScale)
    {
        ResolveRefs();
        if (dialogueHud == null || lines == null || lines.Count == 0)
            yield break;

        bool oldStoryFrozen = gameStateManager != null && gameStateManager.StoryClockFrozen;
        bool hadMovement = playerMovement != null && playerMovement.CanMove;
        if (playerMovement != null)
            playerMovement.SetCanMove(false);
        pauseMenuController?.PushExternalPauseBlock();

        try
        {
            if (gameStateManager != null)
                gameStateManager.SetStoryClockFrozen(true);

            // Keep existing parameter for compatibility; story sequences still force gameplay lock.
            dialogueHud.freezeTimeScaleDuringDialogue = true;
            bool done = false;
            dialogueHud.BeginDialogue(npcName, playerDisplayName, lines, () => done = true);

            while (!done && dialogueHud.IsRunning)
                yield return null;

            while (dialogueHud.IsRunning)
                yield return null;
        }
        finally
        {
            pauseMenuController?.PopExternalPauseBlock();
            if (playerMovement != null)
                playerMovement.SetCanMove(hadMovement);
            if (gameStateManager != null)
                gameStateManager.SetStoryClockFrozen(oldStoryFrozen);
        }
    }

    void BeginQuestOrLog(QuestDefinitionSO so, string label)
    {
        if (so == null)
        {
            Debug.LogWarning($"[LinearStoryDirector] Missing QuestDefinitionSO: {label}");
            return;
        }

        if (questManager == null) return;
        questManager.BeginQuest(so.ToRuntimeCopy());
    }

    void Persist()
    {
        StoryProgressStore.Save(Mathf.Max(0, _checkpoint), _day2HandoffGranted);
        ApplyRestrictionForCheckpoint();
    }

    void ApplyRestrictionForCheckpoint()
    {
        StoryRestrictionGate.ClearAll();
        if (_checkpoint >= 1 && _checkpoint < 5)
        {
            string msg = denyFrontYardThirsty;
            if (_checkpoint == 3) msg = denyFrontYardFarm;
            else if (_checkpoint == 4) msg = denyFrontYardWater;
            StoryRestrictionGate.SetFrontYardBlocked(true, msg);
        }

        if (_checkpoint == 7)
            StoryRestrictionGate.SetTownBlocked(true, denyTownBackyard);

        bool blockPlanting = _checkpoint < 3;
        StoryRestrictionGate.SetPlantingBlocked(blockPlanting, denyPlantingBeforePlantQuest);
    }

    void LockPlayerHard()
    {
        if (playerMovement != null) playerMovement.SetCanMove(false);
        if (playerInteractor != null) playerInteractor.SetInputEnabled(false);
        if (playerCombat != null) playerCombat.PushExternalInputBlock();
    }

    void UnlockPlayerHard()
    {
        if (playerCombat != null) playerCombat.PopExternalInputBlock();
        if (playerInteractor != null) playerInteractor.SetInputEnabled(true);
        if (playerMovement != null) playerMovement.SetCanMove(true);
    }

    void LockBackpackToggle(bool locked)
    {
        if (backpackHud == null) return;
        backpackHud.enableToggleKey = !locked;
    }

    void HideDialogueTargetsForOpening()
    {
        if (_openingUiRevealed) return;
        if (dialogueHud == null || dialogueHud.uiToHideDuringDialogue == null) return;

        for (int i = 0; i < dialogueHud.uiToHideDuringDialogue.Length; i++)
        {
            var go = dialogueHud.uiToHideDuringDialogue[i];
            if (go != null)
                go.SetActive(false);
        }
    }

    void RevealDialogueTargetsAfterOpening()
    {
        if (_openingUiRevealed) return;
        _openingUiRevealed = true;
        if (dialogueHud == null || dialogueHud.uiToHideDuringDialogue == null) return;

        for (int i = 0; i < dialogueHud.uiToHideDuringDialogue.Length; i++)
        {
            var go = dialogueHud.uiToHideDuringDialogue[i];
            if (go != null)
                go.SetActive(true);
        }
    }

    static void KillRemainingZombiesForStoryCleanup()
    {
        var enemies = FindObjectsByType<EnemyAI2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            var ai = enemies[i];
            if (ai == null) continue;
            var hp = ai.GetComponent<Health>();
            if (hp == null || hp.dead) continue;
            hp.TakeDamage(Mathf.Max(1, hp.currentHP));
        }
    }

    static class StoryLines
    {
        public static readonly string[] Step1 =
        {
            "S: ...Consciousness shreds and stitches itself back together. You slowly open your eyes.",
            "S: The air is heavy with rot.",
            "I: ...Where am I? Who am I..."
        };

        public static readonly string[] Step2_PreGrowl =
        {
            "I: Nobody here..."
        };

        public static readonly string[] Step2_GrowlLineOnly =
        {
            "S: A low growl rolls in from far away."
        };

        public static readonly string[] Step2_PostGrowl =
        {
            "P: Who's there??",
            "S: Only silence answers you.",
            "I: So thirsty... I need water.",
            "S: A dry well site (buildable).",
            "I: Maybe I can build a well."
        };

        public static readonly string[] Step3 =
        {
            "I: ...It worked.",
            "I: Oh—I still have my backpack.",
            "S: You open the pack and rummage through it.",
            "I: ...Did I bring these seeds? Maybe I can try planting them."
        };

        public static readonly string[] Step4 =
        {
            "S: The soil is cracked; barely a trace of life.",
            "I: Maybe it needs water."
        };

        public static readonly string[] Step5 =
        {
            "S: Water seeps into the ground with a soft sound.",
            "S: Night draws closer. Distant growling.",
            "I: What was that? Maybe I should go out front and look."
        };

        public static readonly string[] Step6 =
        {
            "I: ...People? No—things shaped like people?",
            "S: The well and the field come to mind.",
            "I: Then this place... I have to hold it."
        };

        public static readonly string[] Step7 =
        {
            "I: I... I survived. What the hell are those things!?",
            "S: Something moves in the backyard—go see what's going on."
        };

        public static readonly string[] Step8 =
        {
            "S: A pit gapes beside the well.",
            "M: ...Hey, you up there. Voice from the sewer.",
            "P:?!??!",
            "M: Oh... called it. Relax. If they haven't eaten you yet, you might still be useful.",
            "P: Who are you?",
            "M: Doesn't matter. What matters is a deal. I can't leave this hole—go to town and get me a few things. Will you?",
            "P: Why?",
            "M: The things shambling out there have noticed you. Without my help you won't last long. I've got a better weapon—you'll last a few more days with it.",
            "P: What are they?",
            "M: The things above? Zombies, walking dead—call them whatever. Look, I'm busy. Are we doing this or not?",
            "P: ...What do you need?",
            "M: Good. Bring me anti-inflammatory meds. Oh—and something to see by; it's dark as hell down here. Hand those over and we're square."
        };

        public static readonly string[] PitSilent = { "M: ...Nothing but silence." };

        public static readonly string[] PitReward =
        {
            "M: That's it! Here's your cut.",
            "M: Well then—so long. Try to stay alive.",
            "S: The voice from the pit fades away."
        };
    }
}
