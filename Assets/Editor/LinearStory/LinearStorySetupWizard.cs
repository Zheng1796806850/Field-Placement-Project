#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>一键配置线性剧情场景与资源。</summary>
public static class LinearStorySetupWizard
{
    const string BaseScenePath = "Assets/Scenes/BaseScene.unity";

    /// <summary>Unity 6 起 <see cref="Undo.SetTransformParent"/> 三参为 string；用 RecordObject + SetParent 保留 worldPositionStays 语义。</summary>
    static void SetTransformParentUndo(Transform child, Transform parent, bool worldPositionStays, string undoName)
    {
        Undo.RecordObject(child, undoName);
        child.SetParent(parent, worldPositionStays);
    }

    [MenuItem("Tools/Linear Story/Run Full Story Setup")]
    public static void RunFullSetup()
    {
        var warnings = new List<string>();
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Linear Story Full Setup");

        try
        {
            if (!EnsureBaseSceneOpen(warnings))
            {
                foreach (var w in warnings) Debug.LogWarning(w);
                return;
            }

            LinearStoryQuestAssetFactory.EnsureQuestAssets(warnings, refreshQuestContent: true);

            var scene = SceneManager.GetActiveScene();
            var storyRoot = GetOrCreateStoryRoot(scene, warnings);

            var director = GetOrCreateLinearStoryDirector(storyRoot, warnings);
            var fade = GetOrCreateStoryFade(storyRoot, director, warnings);

            BindQuestsToDirector(director, warnings);
            EnsureDefaultDialogueAuthoring(director);
            AutoWireDirectorRefs(director, warnings);

            EnsureAreaTriggers(scene, storyRoot, warnings);
            AutoConfigureZoneTeleports(scene, warnings);
            EnsureDay2Pit(scene, storyRoot, warnings);
            ConfigureNpcDialogue(director, warnings);
            TryConfigureSfxStoryGrowl(warnings);
            EnsureBackpackAntiInflammatoryRule(warnings);
            FixWaterCollectorQuestIds(scene, warnings);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            foreach (var w in warnings)
                Debug.LogWarning(w);

            Undo.CollapseUndoOperations(undo);
            Debug.Log("[LinearStorySetupWizard] Run Full Story Setup 完成。请执行 Tools > Linear Story > Validate Story Setup 复查。");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Undo.RevertAllDownToGroup(undo);
        }
    }

    [MenuItem("Tools/Linear Story/Validate Story Setup")]
    public static void RunValidate()
    {
        var entries = LinearStorySetupValidator.ValidateActiveScene();
        LinearStorySetupValidator.LogReport(entries);
    }

    [MenuItem("Tools/Linear Story/Ping SFXLibrary Asset")]
    public static void PingSfxLibrary()
    {
        var lib = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Audio/SFXLibrary.asset");
        if (lib != null)
            EditorGUIUtility.PingObject(lib);
    }

    static bool EnsureBaseSceneOpen(List<string> warnings)
    {
        var active = EditorSceneManager.GetActiveScene();
        string want = BaseScenePath.Replace('\\', '/');
        string cur = string.IsNullOrEmpty(active.path) ? "" : active.path.Replace('\\', '/');
        if (cur == want)
            return true;

        if (!EditorUtility.DisplayDialog(
                "Linear Story Setup",
                $"当前场景为「{active.name}」，建议切换到 BaseScene 再执行全自动配置。\n\n是否打开：{want}？\n（取消则对当前场景继续配置，可能不是基地场景）",
                "打开 BaseScene",
                "使用当前场景"))
        {
            warnings.Add($"[Wizard] 用户在非 BaseScene 上执行配置：{active.name}");
            return true;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        EditorSceneManager.OpenScene(want);
        return true;
    }

    static GameObject GetOrCreateStoryRoot(Scene scene, List<string> warnings)
    {
        GameObject gameRoot = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "_Game")
            {
                gameRoot = root;
                break;
            }
        }

        if (gameRoot == null)
        {
            gameRoot = new GameObject("_Game");
            Undo.RegisterCreatedObjectUndo(gameRoot, "Linear Story: _Game");
        }

        Transform storyT = null;
        for (int i = 0; i < gameRoot.transform.childCount; i++)
        {
            var c = gameRoot.transform.GetChild(i);
            if (c.name == "Story")
            {
                storyT = c;
                break;
            }
        }

        if (storyT == null)
        {
            var storyGo = new GameObject("Story");
            Undo.RegisterCreatedObjectUndo(storyGo, "Linear Story: Story");
            SetTransformParentUndo(storyGo.transform, gameRoot.transform, true, "Linear Story: Story parent");
            storyT = storyGo.transform;
        }

        return storyT.gameObject;
    }

    static LinearStoryDirector GetOrCreateLinearStoryDirector(GameObject storyRoot, List<string> warnings)
    {
        var existing = storyRoot.GetComponentInChildren<LinearStoryDirector>(true);
        if (existing != null)
        {
            Undo.RecordObject(existing, "Linear Story: Director");
            existing.enableLinearStory = true;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var go = new GameObject("LinearStoryDirector");
        Undo.RegisterCreatedObjectUndo(go, "Linear Story: Director");
        SetTransformParentUndo(go.transform, storyRoot.transform, false, "Linear Story: Director parent");
        var dir = Undo.AddComponent<LinearStoryDirector>(go);
        dir.enableLinearStory = true;
        EditorUtility.SetDirty(dir);
        return dir;
    }

    static StoryFadeController GetOrCreateStoryFade(GameObject storyRoot, LinearStoryDirector director, List<string> warnings)
    {
        var fade = storyRoot.GetComponentInChildren<StoryFadeController>(true);
        if (fade != null)
        {
            Undo.RecordObject(director, "Bind storyFade");
            director.storyFade = fade;
            EditorUtility.SetDirty(director);
            return fade;
        }

        var go = new GameObject("StoryFadeCanvas");
        Undo.RegisterCreatedObjectUndo(go, "Linear Story: Fade Canvas");
        SetTransformParentUndo(go.transform, storyRoot.transform, false, "Linear Story: Fade canvas parent");

        var canvas = Undo.AddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50000;

        var scaler = Undo.AddComponent<CanvasScaler>(go);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var cg = Undo.AddComponent<CanvasGroup>(go);

        var blackGo = new GameObject("Blackout");
        Undo.RegisterCreatedObjectUndo(blackGo, "Linear Story: Blackout");
        SetTransformParentUndo(blackGo.transform, go.transform, false, "Linear Story: Blackout parent");
        var rt = blackGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var img = Undo.AddComponent<Image>(blackGo);
        img.color = Color.black;
        img.raycastTarget = false;

        var sfc = Undo.AddComponent<StoryFadeController>(go);
        var so = new SerializedObject(sfc);
        so.FindProperty("canvasGroup").objectReferenceValue = cg;
        so.FindProperty("blackoutImage").objectReferenceValue = img;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(director, "Bind storyFade");
        director.storyFade = sfc;
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(sfc);

        warnings.Add("[Wizard] 已创建 StoryFadeCanvas 占位：请在 Scene 视图中确认全屏黑场无需调整。");
        return sfc;
    }

    static void BindQuestsToDirector(LinearStoryDirector director, List<string> warnings)
    {
        Undo.RecordObject(director, "Bind quests");
        director.questBuildWell = LinearStoryQuestAssetFactory.LoadQuest("Quest_Linear_BuildWell.asset");
        director.questTillSoil = LinearStoryQuestAssetFactory.LoadQuest("Quest_Linear_PlantCrop.asset");
        director.questWaterOnce = LinearStoryQuestAssetFactory.LoadQuest("Quest_Linear_WaterOnce.asset");
        director.questSurviveFirstNight = LinearStoryQuestAssetFactory.LoadQuest("Quest_Linear_FirstNight.asset");
        director.questVisitBackyard = LinearStoryQuestAssetFactory.LoadQuest("Quest_Linear_VisitBackyard.asset");
        director.questTownFetchSupplies = LinearStoryQuestAssetFactory.LoadQuest("Quest_Linear_TownSupplies.asset");
        EditorUtility.SetDirty(director);
    }

    static void AutoWireDirectorRefs(LinearStoryDirector director, List<string> warnings)
    {
        Undo.RecordObject(director, "Auto wire director refs");
        if (director.gameStateManager == null)
            director.gameStateManager = UnityEngine.Object.FindFirstObjectByType<GameStateManager>(FindObjectsInactive.Include);
        if (director.questManager == null)
            director.questManager = UnityEngine.Object.FindFirstObjectByType<QuestManager>(FindObjectsInactive.Include);
        if (director.playerMovement == null)
            director.playerMovement = UnityEngine.Object.FindFirstObjectByType<PlayerMovementController>(FindObjectsInactive.Include);
        if (director.playerCombat == null)
            director.playerCombat = UnityEngine.Object.FindFirstObjectByType<PlayerCombat2D>(FindObjectsInactive.Include);
        if (director.playerInteractor == null)
            director.playerInteractor = UnityEngine.Object.FindFirstObjectByType<PlayerInteractor2D>(FindObjectsInactive.Include);
        if (director.playerInventory == null)
            director.playerInventory = UnityEngine.Object.FindFirstObjectByType<PlayerResourceInventory>(FindObjectsInactive.Include);
        if (director.backpackHud == null)
            director.backpackHud = UnityEngine.Object.FindFirstObjectByType<BackpackPanelHUD>(FindObjectsInactive.Include);
        if (director.pauseMenuController == null)
            director.pauseMenuController = UnityEngine.Object.FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);

        EditorUtility.SetDirty(director);
    }

    static void EnsureDefaultDialogueAuthoring(LinearStoryDirector director)
    {
        Undo.RecordObject(director, "Linear Story: default dialogue config");
        director.EnsureDefaultDialogueStepsIfEmpty();
        EditorUtility.SetDirty(director);
    }

    static void EnsureAreaTriggers(Scene scene, GameObject storyRoot, List<string> warnings)
    {
        bool HasFrontPresence() =>
            UnityEngine.Object.FindObjectsByType<StoryAreaPresenceVolume2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(v => v && string.Equals(v.presenceAreaId, "story_front_yard", StringComparison.Ordinal))
            || UnityEngine.Object.FindObjectsByType<QuestAreaTrigger2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(q => q && string.Equals(q.areaId, "story_front_yard", StringComparison.Ordinal));

        bool HasBackPresence() =>
            UnityEngine.Object.FindObjectsByType<StoryAreaPresenceVolume2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(v => v && string.Equals(v.presenceAreaId, "story_backyard", StringComparison.Ordinal));

        bool HasBackQuest() =>
            UnityEngine.Object.FindObjectsByType<QuestAreaTrigger2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(v => v && string.Equals(v.areaId, "story_backyard", StringComparison.Ordinal));

        if (!HasFrontPresence())
            CreatePlaceholderArea("Story_FrontYardArea", "story_front_yard", isQuestArea: false, storyRoot, warnings);
        if (!HasBackPresence())
            CreatePlaceholderArea("Story_BackYardPresence", "story_backyard", isQuestArea: false, storyRoot, warnings);
        if (!HasBackQuest())
            CreatePlaceholderArea("Story_BackYardQuestArea", "story_backyard", isQuestArea: true, storyRoot, warnings);
    }

    static void CreatePlaceholderArea(string goName, string areaId, bool isQuestArea, GameObject storyRoot, List<string> warnings)
    {
        var go = new GameObject(goName);
        Undo.RegisterCreatedObjectUndo(go, "Linear Story: " + goName);
        SetTransformParentUndo(go.transform, storyRoot.transform, false, "Linear Story: Area placeholder parent");
        go.transform.position = Vector3.zero;

        var box = Undo.AddComponent<BoxCollider2D>(go);
        box.isTrigger = true;
        box.size = new Vector2(4f, 4f);

        if (isQuestArea)
        {
            var q = Undo.AddComponent<QuestAreaTrigger2D>(go);
            q.areaId = areaId;
        }
        else
        {
            var p = Undo.AddComponent<StoryAreaPresenceVolume2D>(go);
            p.presenceAreaId = areaId;
        }

        warnings.Add($"[Wizard] 已创建占位区域「{goName}」areaId/presence={areaId}：请在场景中调整位置与 Collider 大小。");
    }

    static void AutoConfigureZoneTeleports(Scene scene, List<string> warnings)
    {
        var ambiguous = new List<string>();
        foreach (var z in UnityEngine.Object.FindObjectsByType<ZoneTeleportTrigger2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (z == null) continue;
            if (z.linearStoryTravelGate != ZoneTeleportTrigger2D.LinearStoryTravelGate.None)
                continue;

            var gate = InferTravelGate(z, out bool uncertain);
            if (gate == ZoneTeleportTrigger2D.LinearStoryTravelGate.None)
                continue;

            if (uncertain)
            {
                ambiguous.Add(z.name);
                continue;
            }

            Undo.RecordObject(z, "Zone gate");
            z.linearStoryTravelGate = gate;
            EditorUtility.SetDirty(z);
        }

        if (ambiguous.Count > 0)
            warnings.Add("[Wizard] 以下 ZoneTeleportTrigger2D 名称/场景名模糊，未自动设置 linearStoryTravelGate，请手动确认：\n  - " +
                          string.Join("\n  - ", ambiguous));
    }

    static ZoneTeleportTrigger2D.LinearStoryTravelGate InferTravelGate(ZoneTeleportTrigger2D z, out bool uncertain)
    {
        uncertain = false;
        string n = z.name.ToLowerInvariant();
        string scene = (z.targetSceneName ?? "").ToLowerInvariant();

        bool town = n.Contains("town") || scene.Contains("town");
        bool front = n.Contains("front") || n.Contains("前院") || (n.Contains("yard") && !n.Contains("back"));

        if (town && front)
        {
            uncertain = true;
            return ZoneTeleportTrigger2D.LinearStoryTravelGate.None;
        }

        if (town)
            return ZoneTeleportTrigger2D.LinearStoryTravelGate.Town;
        if (front || n.Contains("door") || n.Contains("gate"))
            return ZoneTeleportTrigger2D.LinearStoryTravelGate.FrontYard;

        if (Regex.IsMatch(n, @"\b(to)?_?town\b") || Regex.IsMatch(scene, @"town"))
            return ZoneTeleportTrigger2D.LinearStoryTravelGate.Town;
        if (Regex.IsMatch(n, @"front|前"))
            return ZoneTeleportTrigger2D.LinearStoryTravelGate.FrontYard;

        return ZoneTeleportTrigger2D.LinearStoryTravelGate.None;
    }

    static void EnsureDay2Pit(Scene scene, GameObject storyRoot, List<string> warnings)
    {
        var pit = UnityEngine.Object.FindObjectsByType<StoryDay2PitInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault();

        if (pit != null)
        {
            Undo.RecordObject(pit, "Pit");
            var col = pit.GetComponent<Collider2D>();
            if (col == null)
            {
                var c = Undo.AddComponent<CircleCollider2D>(pit.gameObject);
                c.isTrigger = true;
                c.radius = 1.2f;
            }
            else
                col.isTrigger = true;

            EditorUtility.SetDirty(pit);
            return;
        }

        string[] keys = { "pit", "hole", "sewer", "day2", "npc", "坑洞" };
        Transform candidate = FindTransformByNameHints(scene, keys);

        GameObject pitGo;
        if (candidate != null)
        {
            pitGo = candidate.gameObject;
            if (pitGo.GetComponent<StoryDay2PitInteractable>() == null)
                Undo.AddComponent<StoryDay2PitInteractable>(pitGo);
        }
        else
        {
            pitGo = new GameObject("Story_Day2PitInteractable");
            Undo.RegisterCreatedObjectUndo(pitGo, "Linear Story: Pit placeholder");
            SetTransformParentUndo(pitGo.transform, storyRoot.transform, false, "Linear Story: Pit placeholder parent");
            pitGo.transform.position = Vector3.zero;
            var c = Undo.AddComponent<CircleCollider2D>(pitGo);
            c.isTrigger = true;
            c.radius = 1.2f;
            Undo.AddComponent<StoryDay2PitInteractable>(pitGo);
            warnings.Add("[Wizard] 未按名称找到坑洞对象，已创建 Story_Day2PitInteractable 占位：请移到水井/坑洞位置。");
        }

        var pitComp = pitGo.GetComponent<StoryDay2PitInteractable>();
        Undo.RecordObject(pitComp, "Pit director");
        pitComp.storyDirector = UnityEngine.Object.FindFirstObjectByType<LinearStoryDirector>(FindObjectsInactive.Include);
        EditorUtility.SetDirty(pitComp);
    }

    static void ConfigureNpcDialogue(LinearStoryDirector director, List<string> warnings)
    {
        var hud = UnityEngine.Object.FindFirstObjectByType<NpcDialoguePanelHUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            warnings.Add("[Wizard] 未找到 NpcDialoguePanelHUD，无法自动配置对话 UI。");
            return;
        }

        Undo.RecordObject(hud, "NpcDialogue setup");
        Undo.RecordObject(director, "Bind dialogueHud");

        director.dialogueHud = hud;

        var font = FindTypewriterFont();
        if (font != null)
        {
            director.storyDialogueFont = font;
            if (hud.npcNameText != null) hud.npcNameText.font = font;
            if (hud.dialogueText != null) hud.dialogueText.font = font;
        }
        else
            warnings.Add("[Wizard] 未找到名称包含 typewriter 的 TMP_FontAsset（AA_typewriter SDF）。请在 Project 搜索后手动指定。");

        var hide = new HashSet<GameObject>();
        void TryAdd(Component c)
        {
            if (c == null) return;
            var root = c.gameObject;
            if (hud.panelRoot != null && (root == hud.panelRoot || root.transform.IsChildOf(hud.panelRoot.transform)))
                return;
            hide.Add(root);
        }

        TryAdd(UnityEngine.Object.FindFirstObjectByType<BackpackPanelHUD>(FindObjectsInactive.Include));
        TryAdd(UnityEngine.Object.FindFirstObjectByType<PlaytestObjectiveHUD>(FindObjectsInactive.Include));
        TryAdd(UnityEngine.Object.FindFirstObjectByType<PhaseClockHUD>(FindObjectsInactive.Include));
        TryAdd(UnityEngine.Object.FindFirstObjectByType<QuickSlotsHUD>(FindObjectsInactive.Include));
        TryAdd(UnityEngine.Object.FindFirstObjectByType<WaveEventBannerHUD>(FindObjectsInactive.Include));

        hud.uiToHideDuringDialogue = hide.Where(g => g != null).Distinct().ToArray();
        EditorUtility.SetDirty(hud);
        EditorUtility.SetDirty(director);

        warnings.Add($"[Wizard] 已写入 NpcDialoguePanelHUD.uiToHideDuringDialogue（{hud.uiToHideDuringDialogue.Length} 项），请确认未误藏对话根物体。");
    }

    static TMP_FontAsset FindTypewriterFont()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (f == null) continue;
            if (f.name.IndexOf("typewriter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                f.name.IndexOf("AA_typewriter", StringComparison.OrdinalIgnoreCase) >= 0)
                return f;
        }

        return null;
    }

    static void TryConfigureSfxStoryGrowl(List<string> warnings)
    {
        const string SfxPath = "Assets/Audio/SFXLibrary.asset";
        var lib = AssetDatabase.LoadAssetAtPath<SfxLibrarySO>(SfxPath);
        if (lib == null)
        {
            warnings.Add("[Wizard] 未找到 Assets/Audio/SFXLibrary.asset。");
            return;
        }

        Undo.RecordObject(lib, "SFX Story growl");

        var clip = FindGrowlClip();
        var so = new SerializedObject(lib);
        var entries = so.FindProperty("entries");
        int idx = FindSfxEntryIndex(entries, (int)SfxId.Story_DistantGrowl);
        if (idx < 0)
        {
            int newIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(newIndex);
            idx = newIndex;
        }

        var el = entries.GetArrayElementAtIndex(idx);
        el.FindPropertyRelative("id").intValue = (int)SfxId.Story_DistantGrowl;
        var clips = el.FindPropertyRelative("clips");
        clips.ClearArray();
        if (clip != null)
        {
            clips.InsertArrayElementAtIndex(0);
            clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
        }
        else
            warnings.Add("[Wizard] 未找到合适的 growl AudioClip，Story_DistantGrowl 仍为空，请在 SFXLibrary 中手动指定。");

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(lib);
    }

    static int FindSfxEntryIndex(SerializedProperty entries, int sfxIdInt)
    {
        for (int i = 0; i < entries.arraySize; i++)
        {
            var id = entries.GetArrayElementAtIndex(i).FindPropertyRelative("id");
            if (id != null && id.intValue == sfxIdInt)
                return i;
        }

        return -1;
    }

    static AudioClip FindGrowlClip()
    {
        string[] tokens = { "growl", "distant", "zombie", "low", "groan", "嘶吼", "低沉" };
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            foreach (var t in tokens)
            {
                if (name.Contains(t))
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }

        return null;
    }

    static void EnsureBackpackAntiInflammatoryRule(List<string> warnings)
    {
        var guids = AssetDatabase.FindAssets("t:BackpackRulesSO");
        if (guids.Length == 0)
        {
            warnings.Add("[Wizard] 未找到 BackpackRulesSO，无法自动添加 AntiInflammatory。");
            return;
        }

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var rules = AssetDatabase.LoadAssetAtPath<BackpackRulesSO>(path);
            if (rules == null || rules.rules == null) continue;

            bool has = rules.rules.Any(r => r != null && r.type == ResourceType.AntiInflammatory);
            if (has)
                continue;

            Undo.RecordObject(rules, "Add AntiInflammatory rule");
            rules.rules.Add(new BackpackRulesSO.ResourceRule
            {
                type = ResourceType.AntiInflammatory,
                displayName = "Anti-inflammatory Medicine",
                stackSize = 10,
                maxCarry = -1,
                showInUI = true
            });
            EditorUtility.SetDirty(rules);
            warnings.Add($"[Wizard] 已在「{path}」添加 AntiInflammatory 默认规则（无图标）。");
        }
    }

    static void FixWaterCollectorQuestIds(Scene scene, List<string> warnings)
    {
        foreach (var w in UnityEngine.Object.FindObjectsByType<WaterCollectorBuildSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (w == null) continue;
            if (!string.IsNullOrWhiteSpace(w.questStructureId))
                continue;

            Undo.RecordObject(w, "Water collector quest id");
            w.questStructureId = WaterCollectorQuestIds.StructureId;
            EditorUtility.SetDirty(w);
            warnings.Add($"[Wizard] WaterCollectorBuildSpot「{w.name}」questStructureId 为空，已设为 {WaterCollectorQuestIds.StructureId}。");
        }
    }

    static Transform FindTransformByNameHints(Scene scene, string[] keys)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var hit = SearchRec(root.transform, keys);
            if (hit != null)
                return hit;
        }

        return null;
    }

    static Transform SearchRec(Transform t, string[] keys)
    {
        string ln = t.name.ToLowerInvariant();
        foreach (var k in keys)
        {
            if (ln.Contains(k))
                return t;
        }

        for (int i = 0; i < t.childCount; i++)
        {
            var c = SearchRec(t.GetChild(i), keys);
            if (c != null)
                return c;
        }

        return null;
    }
}
#endif
