using TMPro;
using UnityEngine;

public class ResourceCountersHUD : MonoBehaviour
{
    [Header("Refs")]
    public PlayerResourceInventory inventory;

    [Header("UI")]
    public TextMeshProUGUI waterLabel;
    public TextMeshProUGUI foodLabel;
    public TextMeshProUGUI planksLabel;
    public TextMeshProUGUI seedsLabel;

    [Header("Behavior")]
    public bool showSeeds = false;
    public string waterPrefix = "";
    public string foodPrefix = "";
    public string planksPrefix = "";
    public string seedsPrefix = "";

    private void Awake()
    {
        if (inventory == null) inventory = PlayerResourceInventory.Instance != null ? PlayerResourceInventory.Instance : FindFirstObjectByType<PlayerResourceInventory>(FindObjectsInactive.Include);
        RefreshAll();
    }

    private void OnEnable()
    {
        if (inventory == null) inventory = PlayerResourceInventory.Instance != null ? PlayerResourceInventory.Instance : FindFirstObjectByType<PlayerResourceInventory>(FindObjectsInactive.Include);
        if (inventory != null) inventory.OnAnyResourceChanged += RefreshAll;
        RefreshAll();
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.OnAnyResourceChanged -= RefreshAll;
    }

    private void RefreshAll()
    {
        if (inventory == null) return;

        int water = inventory.Get(ResourceType.Water);
        int food = inventory.Get(ResourceType.Food);
        int planks = inventory.Get(ResourceType.Planks);
        int seeds = inventory.Get(ResourceType.Seeds);

        if (waterLabel != null) waterLabel.text = $"{waterPrefix}{water}";
        if (foodLabel != null) foodLabel.text = $"{foodPrefix}{food}";
        if (planksLabel != null) planksLabel.text = $"{planksPrefix}{planks}";

        if (seedsLabel != null)
        {
            if (!showSeeds) seedsLabel.text = "";
            else seedsLabel.text = $"{seedsPrefix}{seeds}";
        }
    }
}