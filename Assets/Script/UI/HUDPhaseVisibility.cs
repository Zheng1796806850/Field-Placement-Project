using UnityEngine;

public class HUDPhaseVisibility : MonoBehaviour
{
    [Header("Refs")]
    public GameStateManager gameState;
    public CanvasGroup canvasGroup;

    [Header("Visibility")]
    public bool showInDay = false;
    public bool showInNight = true;
    public bool useSetActiveFallback = false;

    private void Awake()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null && !useSetActiveFallback) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        Apply(gameState != null ? gameState.CurrentPhase : DayNightPhase.Day);
    }

    private void OnEnable()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (gameState != null) gameState.OnPhaseChanged += HandlePhaseChanged;
        Apply(gameState != null ? gameState.CurrentPhase : DayNightPhase.Day);
    }

    private void OnDisable()
    {
        if (gameState != null) gameState.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(DayNightPhase phase)
    {
        Apply(phase);
    }

    private void Apply(DayNightPhase phase)
    {
        bool visible = phase == DayNightPhase.Day ? showInDay : showInNight;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (useSetActiveFallback)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}