using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum StoryDialogueLineStyle
{
    Default = 0,
    Narration = 1,
    InnerThought = 2,
    Mystery = 3,
    Player = 4,
    Npc = 5
}

[Serializable]
public class StoryDialogueLineDefinition
{
    public string speaker = "Narrator";
    [TextArea] public string text = "";
    public StoryDialogueLineStyle style = StoryDialogueLineStyle.Default;
    public bool playSfxOnLineStart = false;
    public SfxId onLineStartSfxId = SfxId.Story_DistantGrowl;
}

[Serializable]
public class StoryDialogueStepDefinition
{
    public string stepId = "";
    public List<StoryDialogueLineDefinition> lines = new List<StoryDialogueLineDefinition>();
}
