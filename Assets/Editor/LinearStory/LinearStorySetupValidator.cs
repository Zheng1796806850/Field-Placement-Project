#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>校验线性剧情场景与资源；由菜单 Tools/Linear Story/Validate Story Setup 调用。</summary>
public static class LinearStorySetupValidator
{
    public enum Level
    {
        Info,
        Warning,
        Error
    }

    public readonly struct Entry
    {
        public readonly Level Level;
        public readonly string Message;

        public Entry(Level level, string message)
        {
            Level = level;
            Message = message;
        }
    }

    public static List<Entry> ValidateActiveScene()
    {
        var list = new List<Entry>();
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            list.Add(new Entry(Level.Error, "当前没有有效活动场景。"));
            return list;
        }

        ValidateApiPresence(list);
        ValidateDirector(scene, list);
        ValidateStoryFade(list);
        ValidateQuests(list);
        ValidateAreas(scene, list);
        ValidateZoneTeleports(scene, list);
        ValidatePit(scene, list);
        ValidateDialogue(scene, list);
        ValidateDialogueSteps(scene, list);
        ValidateSfxLibrary(list);
        ValidateBackpackRules(list);
        ValidateWaterCollectors(scene, list);
        ValidatePhaseClock(scene, list);

        return list;
    }

    public static void LogReport(List<Entry> entries)
    {
        int e = 0, w = 0;
        var sb = new StringBuilder();
        sb.AppendLine("=== Linear Story Validate ===");
        foreach (var x in entries)
        {
            if (x.Level == Level.Error) e++;
            if (x.Level == Level.Warning) w++;
            sb.Append('[').Append(x.Level).Append("] ").AppendLine(x.Message);
        }

        sb.AppendLine($"Summary: {e} error(s), {w} warning(s).");
        Debug.Log(sb.ToString());
    }

    static void ValidateApiPresence(List<Entry> list)
    {
        var gsm = typeof(GameStateManager).GetProperty("StoryClockFrozen", BindingFlags.Public | BindingFlags.Instance);
        if (gsm == null)
            list.Add(new Entry(Level.Error, "GameStateManager 缺少 StoryClockFrozen API。"));

        var pausePush = typeof(PauseMenuController).GetMethod("PushExternalPauseBlock", BindingFlags.Public | BindingFlags.Instance);
        var pausePop = typeof(PauseMenuController).GetMethod("PopExternalPauseBlock", BindingFlags.Public | BindingFlags.Instance);
        if (pausePush == null || pausePop == null)
            list.Add(new Entry(Level.Error, "PauseMenuController 缺少 PushExternalPauseBlock / PopExternalPauseBlock。"));

        var pauseLegacy = typeof(PauseMenuController).GetMethod("SetExternalPauseBlocked", BindingFlags.Public | BindingFlags.Instance);
        if (pauseLegacy == null)
            list.Add(new Entry(Level.Error, "PauseMenuController 缺少 SetExternalPauseBlocked。"));
    }

    static void ValidateDirector(Scene scene, List<Entry> list)
    {
        var dir = UnityEngine.Object.FindFirstObjectByType<LinearStoryDirector>(FindObjectsInactive.Include);
        if (dir == null)
        {
            list.Add(new Entry(Level.Error, $"场景 [{scene.name}] 中未找到 LinearStoryDirector。"));
            return;
        }

        if (!dir.enableLinearStory)
            list.Add(new Entry(Level.Warning, "LinearStoryDirector.enableLinearStory 未勾选。"));

        if (dir.storyFade == null)
            list.Add(new Entry(Level.Error, "LinearStoryDirector.storyFade 未绑定。"));
        else
        {
            var fade = dir.storyFade;
            var so = new SerializedObject(fade);
            var cg = so.FindProperty("canvasGroup");
            var img = so.FindProperty("blackoutImage");
            if (cg != null && cg.objectReferenceValue == null)
                list.Add(new Entry(Level.Error, "StoryFadeController.canvasGroup 为空。"));
            if (img != null && img.objectReferenceValue == null)
                list.Add(new Entry(Level.Warning, "StoryFadeController.blackoutImage 为空（将仅靠 CanvasGroup）。"));
        }

        if (dir.questBuildWell == null) list.Add(new Entry(Level.Error, "LinearStoryDirector.questBuildWell 未绑定。"));
        if (dir.questTillSoil == null) list.Add(new Entry(Level.Error, "LinearStoryDirector.questTillSoil 未绑定。"));
        if (dir.questWaterOnce == null) list.Add(new Entry(Level.Error, "LinearStoryDirector.questWaterOnce 未绑定。"));
        if (dir.questSurviveFirstNight == null) list.Add(new Entry(Level.Error, "LinearStoryDirector.questSurviveFirstNight 未绑定。"));
        if (dir.questVisitBackyard == null) list.Add(new Entry(Level.Error, "LinearStoryDirector.questVisitBackyard 未绑定。"));
        if (dir.questTownFetchSupplies == null) list.Add(new Entry(Level.Error, "LinearStoryDirector.questTownFetchSupplies 未绑定。"));

        if (dir.dialogueHud == null)
            list.Add(new Entry(Level.Error, "LinearStoryDirector.dialogueHud（NpcDialoguePanelHUD）未绑定。"));

        if (dir.storyDialogueFont == null)
            list.Add(new Entry(Level.Warning, "LinearStoryDirector.storyDialogueFont（AA_typewriter SDF）未绑定。"));
    }

    static void ValidateStoryFade(List<Entry> list)
    {
        var fade = UnityEngine.Object.FindFirstObjectByType<StoryFadeController>(FindObjectsInactive.Include);
        if (fade == null)
            list.Add(new Entry(Level.Error, "场景中未找到 StoryFadeController。"));
    }

    static void ValidateQuests(List<Entry> list)
    {
        string[] files =
        {
            "Quest_Linear_BuildWell.asset",
            "Quest_Linear_PlantCrop.asset",
            "Quest_Linear_WaterOnce.asset",
            "Quest_Linear_FirstNight.asset",
            "Quest_Linear_VisitBackyard.asset",
            "Quest_Linear_TownSupplies.asset"
        };

        foreach (var f in files)
        {
            var q = LinearStoryQuestAssetFactory.LoadQuest(f);
            if (q == null)
            {
                list.Add(new Entry(Level.Error, $"缺少任务资源：{LinearStoryQuestAssetFactory.TargetDir}/{f}"));
                continue;
            }

            if (q.triggerGameFlowVictoryOnComplete)
                list.Add(new Entry(Level.Error, $"{f}：triggerGameFlowVictoryOnComplete 应为 false。"));

            if (f == "Quest_Linear_FirstNight.asset" && q.objectives != null)
            {
                foreach (var o in q.objectives)
                {
                    if (o != null && o.type == ObjectiveType.Kill && o.requiredAmount == 10)
                        list.Add(new Entry(Level.Warning, $"{f}：Kill requiredAmount=10，请按实际波次敌人数量确认。"));
                }
            }

        }
    }

    static void ValidateAreas(Scene scene, List<Entry> list)
    {
        bool front = false, backQuest = false, backPresence = false;

        foreach (var q in UnityEngine.Object.FindObjectsByType<QuestAreaTrigger2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (q == null) continue;
            if (string.Equals(q.areaId, "story_front_yard", StringComparison.Ordinal))
                front = true;
            if (string.Equals(q.areaId, "story_backyard", StringComparison.Ordinal))
                backQuest = true;
        }

        foreach (var v in UnityEngine.Object.FindObjectsByType<StoryAreaPresenceVolume2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (v == null) continue;
            if (string.Equals(v.presenceAreaId, "story_front_yard", StringComparison.Ordinal))
                front = true;
            if (string.Equals(v.presenceAreaId, "story_backyard", StringComparison.Ordinal))
                backPresence = true;
        }

        if (!front)
            list.Add(new Entry(Level.Warning, "未找到前院区域：QuestAreaTrigger2D.areaId 或 StoryAreaPresenceVolume2D.presenceAreaId = story_front_yard（占位或缺失）。"));
        if (!backQuest)
            list.Add(new Entry(Level.Warning, "未找到 QuestAreaTrigger2D.areaId = story_backyard（占位或缺失）。"));
        if (!backPresence)
            list.Add(new Entry(Level.Warning, "未找到 StoryAreaPresenceVolume2D.presenceAreaId = story_backyard（占位或缺失）。"));
    }

    static void ValidateZoneTeleports(Scene scene, List<Entry> list)
    {
        var zones = UnityEngine.Object.FindObjectsByType<ZoneTeleportTrigger2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int gated = 0;
        foreach (var z in zones)
        {
            if (z != null && z.linearStoryTravelGate != ZoneTeleportTrigger2D.LinearStoryTravelGate.None)
                gated++;
        }

        if (gated == 0)
            list.Add(new Entry(Level.Warning, "未找到任何 linearStoryTravelGate != None 的 ZoneTeleportTrigger2D（前院/小镇门控可能未配置）。"));
    }

    static void ValidatePit(Scene scene, List<Entry> list)
    {
        var pit = UnityEngine.Object.FindFirstObjectByType<StoryDay2PitInteractable>(FindObjectsInactive.Include);
        if (pit == null)
            list.Add(new Entry(Level.Warning, "未找到 StoryDay2PitInteractable（占位或缺失）。"));
    }

    static void ValidateDialogue(Scene scene, List<Entry> list)
    {
        var hud = UnityEngine.Object.FindFirstObjectByType<NpcDialoguePanelHUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            list.Add(new Entry(Level.Error, "场景中未找到 NpcDialoguePanelHUD。"));
            return;
        }

        if (hud.uiToHideDuringDialogue == null || hud.uiToHideDuringDialogue.Length == 0)
            list.Add(new Entry(Level.Warning, "NpcDialoguePanelHUD.uiToHideDuringDialogue 为空。"));

        if (hud.npcNameText != null && hud.npcNameText.font != null)
        {
            if (!hud.npcNameText.font.name.Contains("typewriter", StringComparison.OrdinalIgnoreCase))
                list.Add(new Entry(Level.Info, $"NpcDialoguePanelHUD.npcNameText 字体为「{hud.npcNameText.font.name}」，若不是 AA_typewriter 请替换。"));
        }
    }

    static void ValidateDialogueSteps(Scene scene, List<Entry> list)
    {
        var dir = UnityEngine.Object.FindFirstObjectByType<LinearStoryDirector>(FindObjectsInactive.Include);
        if (dir == null)
            return;

        if (dir.dialogueSteps == null || dir.dialogueSteps.Count == 0)
        {
            list.Add(new Entry(Level.Error, "LinearStoryDirector.dialogueSteps 为空。请运行 Setup 或手动填充。"));
            return;
        }

        var map = new Dictionary<string, StoryDialogueStepDefinition>(StringComparer.Ordinal);
        foreach (var step in dir.dialogueSteps)
        {
            if (step == null || string.IsNullOrWhiteSpace(step.stepId))
                continue;
            map[step.stepId.Trim()] = step;
        }

        foreach (var required in LinearStoryDirector.RequiredDialogueStepIds)
        {
            if (!map.TryGetValue(required, out var step) || step == null || step.lines == null || step.lines.Count == 0)
            {
                list.Add(new Entry(Level.Error, $"缺少剧情对话 stepId 或无内容：{required}"));
                continue;
            }

            for (int i = 0; i < step.lines.Count; i++)
            {
                var line = step.lines[i];
                if (line == null)
                {
                    list.Add(new Entry(Level.Warning, $"{required} 第 {i + 1} 句为空引用。"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line.text))
                    list.Add(new Entry(Level.Warning, $"{required} 第 {i + 1} 句 text 为空。"));
                if (string.IsNullOrWhiteSpace(line.speaker))
                    list.Add(new Entry(Level.Warning, $"{required} 第 {i + 1} 句 speaker 为空。"));
            }
        }

        foreach (var kv in map)
        {
            var stepId = kv.Key;
            var step = kv.Value;
            if (step?.lines == null) continue;
            for (int i = 0; i < step.lines.Count; i++)
            {
                var line = step.lines[i];
                if (line == null) continue;
                if (!line.playSfxOnLineStart || line.onLineStartSfxId != SfxId.Story_DistantGrowl) continue;
                if (!string.Equals(stepId, "step2_growl_line_only", StringComparison.Ordinal))
                    list.Add(new Entry(Level.Warning, $"Story_DistantGrowl 建议仅绑定 step2_growl_line_only，当前在 {stepId} 第 {i + 1} 句。"));
            }
        }
    }

    static void ValidateSfxLibrary(List<Entry> list)
    {
        var lib = AssetDatabase.LoadAssetAtPath<SfxLibrarySO>("Assets/Audio/SFXLibrary.asset");
        if (lib == null)
        {
            list.Add(new Entry(Level.Error, "未找到 Assets/Audio/SFXLibrary.asset。"));
            return;
        }

        if (!lib.TryGet(SfxId.Story_DistantGrowl, out var entry) || entry.clips == null || entry.clips.Length == 0 || entry.clips[0] == null)
            list.Add(new Entry(Level.Warning, "SfxLibrary 中 Story_DistantGrowl 未配置有效 AudioClip。"));
    }

    static void ValidateBackpackRules(List<Entry> list)
    {
        var guids = AssetDatabase.FindAssets("t:BackpackRulesSO");
        if (guids.Length == 0)
        {
            list.Add(new Entry(Level.Warning, "项目中未找到 BackpackRulesSO。"));
            return;
        }

        bool any = false;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var rules = AssetDatabase.LoadAssetAtPath<BackpackRulesSO>(path);
            if (rules == null || rules.rules == null) continue;
            foreach (var r in rules.rules)
            {
                if (r != null && r.type == ResourceType.AntiInflammatory)
                    any = true;
            }
        }

        if (!any)
            list.Add(new Entry(Level.Warning, "所有 BackpackRulesSO 中均未找到 ResourceType.AntiInflammatory 规则。"));

    }

    static void ValidateWaterCollectors(Scene scene, List<Entry> list)
    {
        var spots = UnityEngine.Object.FindObjectsByType<WaterCollectorBuildSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (spots.Length == 0)
        {
            list.Add(new Entry(Level.Info, "场景中未找到 WaterCollectorBuildSpot（若水井在 prefab 中可忽略）。"));
            return;
        }

        foreach (var s in spots)
        {
            if (s == null) continue;
            if (string.IsNullOrWhiteSpace(s.questStructureId))
                list.Add(new Entry(Level.Warning, $"WaterCollectorBuildSpot「{s.name}」questStructureId 为空。"));
            else if (!string.Equals(s.questStructureId, WaterCollectorQuestIds.StructureId, StringComparison.Ordinal))
                list.Add(new Entry(Level.Info, $"WaterCollectorBuildSpot「{s.name}」questStructureId={s.questStructureId}（任务目标需一致）。"));
        }
    }

    static void ValidatePhaseClock(Scene scene, List<Entry> list)
    {
        var c = UnityEngine.Object.FindFirstObjectByType<PhaseClockHUD>(FindObjectsInactive.Include);
        if (c == null)
        {
            list.Add(new Entry(Level.Warning, "场景中未找到 PhaseClockHUD。"));
            return;
        }

        if (!PhaseClockScriptReferencesStoryClockFrozen())
            list.Add(new Entry(Level.Error, "PhaseClockHUD 脚本源码中未检测到 StoryClockFrozen 分支（时钟冻结可能未实现）。"));
    }

    /// <summary>通过源码文本确认 PhaseClockHUD 已接入昼夜冻结（不依赖运行态）。</summary>
    static bool PhaseClockScriptReferencesStoryClockFrozen()
    {
        foreach (var guid in AssetDatabase.FindAssets("PhaseClockHUD t:MonoScript"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith("PhaseClockHUD.cs", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var txt = File.ReadAllText(path);
                return txt.Contains("StoryClockFrozen", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        return false;
    }
}
#endif
