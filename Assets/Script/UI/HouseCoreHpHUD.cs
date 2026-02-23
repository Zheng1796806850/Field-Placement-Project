using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HouseCoreHpHUD : MonoBehaviour
{
    [Header("Refs")]
    public GameStateManager gameState;
    public HouseObjective house;

    [Header("UI")]
    public CanvasGroup canvasGroup;
    public Image fillImage;
    public TextMeshProUGUI label;

    [Header("Behavior")]
    public bool showOnlyAtNight = true;
    public bool showValueText = false;
    public string titleText = "House HP";

    private bool _subscribed;

    private void Awake()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (house == null) house = HouseObjective.Instance != null ? HouseObjective.Instance : FindFirstObjectByType<HouseObjective>(FindObjectsInactive.Include);
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        ApplyVisibility();
        Push();
    }

    private void OnEnable()
    {
        if (gameState != null) gameState.OnPhaseChanged += HandlePhaseChanged;
        SubscribeHouse();
        ApplyVisibility();
        Push();
    }

    private void OnDisable()
    {
        if (gameState != null) gameState.OnPhaseChanged -= HandlePhaseChanged;
        UnsubscribeHouse();
    }

    private void SubscribeHouse()
    {
        if (_subscribed) return;
        if (house == null) return;
        house.OnCoreHealthChanged += HandleHealthChanged;
        _subscribed = true;
    }

    private void UnsubscribeHouse()
    {
        if (!_subscribed) return;
        if (house != null) house.OnCoreHealthChanged -= HandleHealthChanged;
        _subscribed = false;
    }

    private void HandlePhaseChanged(DayNightPhase _) => ApplyVisibility();

    private void HandleHealthChanged(int current, int max) => Set(current, max);

    private void Push()
    {
        if (house == null || house.coreHealth == null) return;
        Set(house.coreHealth.currentHP, house.coreHealth.maxHP);
    }

    private void Set(int current, int max)
    {
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);

        if (fillImage != null) fillImage.fillAmount = current / (float)max;

        if (label != null)
        {
            if (showValueText) label.text = $"{titleText} {current}/{max}";
            else label.text = titleText;
        }
    }

    private void ApplyVisibility()
    {
        bool visible = true;

        if (showOnlyAtNight)
        {
            bool isNight = gameState != null && gameState.CurrentPhase == DayNightPhase.Night;
            visible &= isNight;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}