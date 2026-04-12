using System;
using UnityEngine;

[Serializable]
public class ObjectiveDefinition
{
    [Tooltip("Stable id for save/reconcile.")]
    public string objectiveId;

    public ObjectiveType type = ObjectiveType.Collect;

    [Tooltip("For Build/Repair/Kill/Reach: filter; empty = match any (where applicable).")]
    public string targetId;

    public ResourceType resourceType;
    [Min(1)] public int requiredAmount = 1;

    [TextArea] public string displayText;

    [Tooltip("Reserved for designers (e.g. optional branches later).")]
    public bool optional;

    [Tooltip("PlantAndWater: if set, event cropId must match this (CropConfigSO.cropId).")]
    public string filterCropId;
}
