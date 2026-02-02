using TMPro;
using UnityEngine;

public class ResourceDebugUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI label;

    [Header("Refs")]
    public PlayerResourceInventory inventory;

    [Header("Hotkeys")]
    public bool enableHotkeys = true;
    public KeyCode addPlanksKey = KeyCode.Keypad1;
    public KeyCode addSeedsKey = KeyCode.Keypad2;
    public KeyCode addWaterKey = KeyCode.Keypad3;
    public KeyCode addFoodKey = KeyCode.Keypad4;

    public int addAmountPerKey = 5;

    public KeyCode saveKey = KeyCode.F6;
    public KeyCode loadKey = KeyCode.F7;
    public KeyCode clearSaveKey = KeyCode.F8;
    public KeyCode resetToDefaultKey = KeyCode.F9;

    private void Reset()
    {
        label = GetComponent<TextMeshProUGUI>();
    }

    private void Awake()
    {
        if (inventory == null)
            inventory = PlayerResourceInventory.Instance;
    }

    private void OnEnable()
    {
        if (inventory == null)
            inventory = PlayerResourceInventory.Instance;

        if (inventory != null)
            inventory.OnAnyResourceChanged += Refresh;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.OnAnyResourceChanged -= Refresh;
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (!enableHotkeys || inventory == null) return;

        if (Input.GetKeyDown(addPlanksKey)) inventory.Add(ResourceType.Planks, addAmountPerKey);
        if (Input.GetKeyDown(addSeedsKey)) inventory.Add(ResourceType.Seeds, addAmountPerKey);
        if (Input.GetKeyDown(addWaterKey)) inventory.Add(ResourceType.Water, addAmountPerKey);
        if (Input.GetKeyDown(addFoodKey)) inventory.Add(ResourceType.Food, addAmountPerKey);

        if (Input.GetKeyDown(saveKey)) inventory.SaveInMemory();
        if (Input.GetKeyDown(loadKey)) inventory.LoadFromMemory();
        if (Input.GetKeyDown(clearSaveKey)) inventory.ClearSave();
        if (Input.GetKeyDown(resetToDefaultKey)) inventory.ResetToDefaults(alsoClearSave: false);
        Refresh();
    }

    private void Refresh()
    {
        if (label == null || inventory == null) return;

        int planks = inventory.Get(ResourceType.Planks);
        int seeds = inventory.Get(ResourceType.Seeds);
        int water = inventory.Get(ResourceType.Water);
        int food = inventory.Get(ResourceType.Food);

        label.text =
            $"<b>Resources</b>\n" +
            $"Planks: {planks}\n" +
            $"Seeds : {seeds}\n" +
            $"Water : {water}\n" +
            $"Food  : {food}\n\n" +
            $"<b>Hotkeys</b>\n" +
            $"[Num1-4] +{addAmountPerKey}\n" +
            $"[F6] Save  [F7] Load\n" +
            $"[F8] Clear Save\n" +
            $"[F9] Reset Defaults (no clear)";
    }
}
