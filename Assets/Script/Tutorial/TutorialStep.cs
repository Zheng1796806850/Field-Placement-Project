using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TutorialStep : MonoBehaviour
{
    [Header("Info")]
    public string stepId = "step";
    public string title = "Tutorial Step";
    [TextArea] public string description;

    [Header("Abilities")]
    public bool allowMovement = true;
    public bool allowCombat = true;
    public bool allowInteraction = true;
    public bool allowQuickSlotUse = true;
    public bool allowPlantingMode = false;
    public bool allowWallPlacementMode = false;
    [Tooltip("While this step is active, PlayerWallPlacementController uses this Grid (e.g. this area's tilemap grid). Leave null to keep the player's default grid.")]
    public Grid wallPlacementGrid;

    [Header("Transition")]
    [Min(0f)] public float completeDelaySeconds = 0.8f;
    public Transform teleportTarget;

    [Header("Camera (optional)")]
    [Tooltip("Tutorial snaps Main Camera to this transform's position each time this step becomes active (after teleport). Leave null to keep CameraFollowBounds2D following the player.")]
    public Transform cameraPoint;

    [Header("Progress UI (optional)")]
    [Tooltip("First line after [done/total] when using Step Header layout. Empty = use Title.")]
    public string progressHeadline;
    [Tooltip("Each objective line: {0} = status marker, {1} = GetProgressText(). Example: \"{0}{1}\" or \"{1}\" to hide markers.")]
    public string progressLineFormat = "{0}{1}";
    public string progressCompletedMarker = "[Done] ";
    public string progressIncompleteMarker = "[ ] ";
    public string progressBetweenObjectives = "\n";
    [Tooltip("Shown when this step has no objectives.")]
    public string progressWhenEmptyObjectives = "Complete";

    [Header("Optional UI Override")]
    public TextMeshProUGUI objectiveHintLabel;

    private TutorialObjective[] _cachedObjectives;

    public IReadOnlyList<TutorialObjective> Objectives
    {
        get
        {
            if (_cachedObjectives == null || _cachedObjectives.Length == 0)
                _cachedObjectives = GetComponentsInChildren<TutorialObjective>(true);
            return _cachedObjectives;
        }
    }

    public void InvalidateObjectiveCache()
    {
        _cachedObjectives = null;
    }
}
