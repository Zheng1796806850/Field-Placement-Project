using System;
using UnityEngine;

public class WaterCollectorBuildSpot : MonoBehaviour, IInteractable
{
    [Header("Build Mode")]
    [Tooltip("If true, player must build using Planks. If false, starts built at game start.")]
    public bool requireBuild = true;

    [Min(0)] public int planksCost = 10;

    [Header("State (Read Only)")]
    [SerializeField] private bool isBuilt = false;

    [Header("Interact")]
    [Tooltip("Higher means PlayerInteractor2D will prefer this when multiple interactables are in range.")]
    public int priority = 6;
    public bool debugLogs = false;

    [Header("Visual Placeholders")]
    public GameObject unbuiltVisual;
    public GameObject builtVisual;

    [Header("Persistence (Optional)")]
    [Tooltip("If true, after building it will call PlayerResourceInventory.SaveInMemory().")]
    public bool autoSaveInventoryOnBuild = true;

    public event Action OnBuilt;

    public int Priority => priority;

    private void Awake()
    {
        if (!requireBuild)
            isBuilt = true;

        ApplyVisuals();
    }

    public string GetPrompt()
    {
        if (isBuilt) return "Water Collector";
        return planksCost <= 0 ? "Build Water Collector" : $"Build Water Collector (-{planksCost} Planks)";
    }

    public bool CanInteract(GameObject interactor)
    {
        if (isBuilt) return false;

        var inv = ResolveInventory(interactor);
        if (inv == null) return false;

        return inv.CanSpend(ResourceType.Planks, planksCost);
    }

    public void Interact(GameObject interactor)
    {
        if (isBuilt) return;

        var inv = ResolveInventory(interactor);
        if (inv == null) return;

        if (!inv.Spend(ResourceType.Planks, planksCost))
        {
            if (debugLogs)
                Debug.Log($"[WaterCollectorBuildSpot] Not enough planks to build on {name}");
            return;
        }

        isBuilt = true;
        ApplyVisuals();

        if (autoSaveInventoryOnBuild)
            inv.SaveInMemory();

        if (debugLogs)
            Debug.Log($"[WaterCollectorBuildSpot] Built water collector on {name} (spent {planksCost} planks)");

        OnBuilt?.Invoke();
    }

    public bool IsBuilt => isBuilt;

    public void ForceSetBuilt(bool built)
    {
        isBuilt = built;
        ApplyVisuals();
    }

    private PlayerResourceInventory ResolveInventory(GameObject interactor)
    {
        var inv = interactor != null ? interactor.GetComponentInParent<PlayerResourceInventory>() : null;
        if (inv != null) return inv;
        return PlayerResourceInventory.Instance;
    }

    private void ApplyVisuals()
    {
        if (unbuiltVisual != null) unbuiltVisual.SetActive(!isBuilt);
        if (builtVisual != null) builtVisual.SetActive(isBuilt);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyVisuals();
    }
#endif
}
