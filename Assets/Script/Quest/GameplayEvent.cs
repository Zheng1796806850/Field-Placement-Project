using UnityEngine;

/// <summary>Normalized payload from <see cref="GameplayEventHub"/> into <see cref="QuestManager"/>.</summary>
public readonly struct GameplayEvent
{
    public GameplayEventKind Kind { get; }
    public ResourceType ResourceType { get; }
    public int IntValue { get; }
    public string StringId { get; }
    /// <summary>For <see cref="GameplayEventKind.CropPlantedAndWatered"/>: <see cref="CropConfigSO.cropId"/>.</summary>
    public string StringId2 { get; }

    public GameplayEvent(GameplayEventKind kind, ResourceType resourceType = default, int intValue = 0, string stringId = null, string stringId2 = null)
    {
        Kind = kind;
        ResourceType = resourceType;
        IntValue = intValue;
        StringId = stringId ?? string.Empty;
        StringId2 = stringId2 ?? string.Empty;
    }
}
