using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// MENU-03a/b: tabbed settings shell — Audio / Controls / Gameplay panels, Apply & Back.
/// Back / ESC / panel disable: discards un-applied changes (reverts audio snapshot). Apply commits to disk.
/// </summary>
[DisallowMultipleComponent]
public class SettingsShellController : MonoBehaviour
{
    [Serializable]
    private class ButtonSpriteVisual
    {
        public Button button;
        public Image imageTarget;
        public Sprite normalSprite;
        public Sprite hoverSprite;
        public Sprite selectedSprite;
        [Tooltip("If >= 0, this entry is a tab button for that tab index and shows Selected Sprite while active.")]
        public int tabIndex = -1;
    }

    [Header("Tabs")]
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button controlsTabButton;
    [SerializeField] private Button gameplayTabButton;

    [SerializeField] private GameObject audioPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject gameplayPanel;

    [Header("Actions")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button backButton;

    [Header("Optional: legacy close (Main Menu)")]
    [Tooltip("If set, Back also invokes this (e.g. wire MainMenuUI.CloseSettings).")]
    [SerializeField] private UnityEvent onBackClosePanel;

    [Header("Button Hover Sprites (Lightweight)")]
    [Tooltip("Only swaps button image on mouse hover. No scale/size animation.")]
    [SerializeField] private List<ButtonSpriteVisual> buttonSpriteVisuals = new List<ButtonSpriteVisual>();

    private int _currentTab;
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);
    private readonly Dictionary<Image, Sprite> _baseSprites = new Dictionary<Image, Sprite>();

    private void Awake()
    {
        CacheSpriteVisuals();

        WireTab(audioTabButton, SelectAudioTab);
        WireTab(controlsTabButton, SelectControlsTab);
        WireTab(gameplayTabButton, SelectGameplayTab);

        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(OnApplyClicked);
            applyButton.onClick.AddListener(OnApplyClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnBackClicked);
            backButton.onClick.AddListener(OnBackClicked);
        }
    }

    private static void WireTab(Button b, UnityAction action)
    {
        if (b == null) return;
        b.onClick.RemoveListener(action);
        b.onClick.AddListener(action);
    }

    private void OnEnable()
    {
        SettingsManager.EnsureInHierarchy();
        SettingsManager.Instance?.BeginSettingsSession();
        SelectTab(_currentTab);
        RefreshButtonSprites();
    }

    private void OnDisable()
    {
        SettingsManager.Instance?.EndSettingsSessionIfAborted();
        RestoreAllNormalSprites();
    }

    private void Update()
    {
        RefreshButtonSprites();
    }

    private void SelectAudioTab() => SelectTab(0);
    private void SelectControlsTab() => SelectTab(1);
    private void SelectGameplayTab() => SelectTab(2);

    public void SelectTab(int index)
    {
        _currentTab = Mathf.Clamp(index, 0, 2);
        if (audioPanel != null) audioPanel.SetActive(_currentTab == 0);
        if (controlsPanel != null) controlsPanel.SetActive(_currentTab == 1);
        if (gameplayPanel != null) gameplayPanel.SetActive(_currentTab == 2);
    }

    public void OnApplyClicked()
    {
        SettingsManager.Instance?.ApplyAndSaveSettings();
    }

    /// <summary>
    /// Back: revert un-applied edits, end session, then optional close callback (main menu / pause wires differently).
    /// </summary>
    public void OnBackClicked()
    {
        BackWithoutCloseEvent();
        onBackClosePanel?.Invoke();
    }

    /// <summary>For pause menu: revert session state; caller then hides settings (e.g. ShowPausePanelOnly).</summary>
    public void BackWithoutCloseEvent()
    {
        var mgr = SettingsManager.Instance;
        if (mgr != null)
        {
            if (mgr.SessionDirty)
                mgr.RevertSettingsSession();
            mgr.CloseSettingsSessionCommitted();
        }
    }

    private void CacheSpriteVisuals()
    {
        EnsureSpriteVisualEntry(audioTabButton, tabIndex: 0);
        EnsureSpriteVisualEntry(controlsTabButton, tabIndex: 1);
        EnsureSpriteVisualEntry(gameplayTabButton, tabIndex: 2);
        EnsureSpriteVisualEntry(applyButton, tabIndex: -1);
        EnsureSpriteVisualEntry(backButton, tabIndex: -1);

        _baseSprites.Clear();

        for (int i = 0; i < buttonSpriteVisuals.Count; i++)
        {
            var entry = buttonSpriteVisuals[i];
            if (entry == null || entry.button == null)
                continue;

            if (entry.imageTarget == null)
                entry.imageTarget = entry.button.targetGraphic as Image;

            if (entry.imageTarget == null)
                continue;

            if (entry.normalSprite == null)
                entry.normalSprite = entry.imageTarget.sprite;

            if (!_baseSprites.ContainsKey(entry.imageTarget))
                _baseSprites.Add(entry.imageTarget, entry.imageTarget.sprite);
        }
    }

    private void EnsureSpriteVisualEntry(Button button, int tabIndex)
    {
        if (button == null)
            return;

        for (int i = 0; i < buttonSpriteVisuals.Count; i++)
        {
            var entry = buttonSpriteVisuals[i];
            if (entry != null && entry.button == button)
            {
                if (entry.tabIndex < 0 && tabIndex >= 0)
                    entry.tabIndex = tabIndex;
                return;
            }
        }

        buttonSpriteVisuals.Add(new ButtonSpriteVisual
        {
            button = button,
            imageTarget = button.targetGraphic as Image,
            tabIndex = tabIndex
        });
    }

    private void RefreshButtonSprites()
    {
        Button hovered = GetHoveredButton();

        for (int i = 0; i < buttonSpriteVisuals.Count; i++)
        {
            var entry = buttonSpriteVisuals[i];
            if (entry == null || entry.button == null || entry.imageTarget == null)
                continue;

            Sprite normal = entry.normalSprite;
            if (normal == null)
                _baseSprites.TryGetValue(entry.imageTarget, out normal);

            bool isActiveTab = entry.tabIndex >= 0 && entry.tabIndex == _currentTab;

            Sprite selected = entry.selectedSprite != null ? entry.selectedSprite : normal;
            Sprite baseSprite = isActiveTab ? selected : normal;

            Sprite desired = (entry.button == hovered && entry.hoverSprite != null)
                ? entry.hoverSprite
                : baseSprite;

            if (desired != null && entry.imageTarget.sprite != desired)
                entry.imageTarget.sprite = desired;
        }
    }

    private void RestoreAllNormalSprites()
    {
        for (int i = 0; i < buttonSpriteVisuals.Count; i++)
        {
            var entry = buttonSpriteVisuals[i];
            if (entry == null || entry.imageTarget == null)
                continue;

            Sprite normal = entry.normalSprite;
            if (normal == null)
                _baseSprites.TryGetValue(entry.imageTarget, out normal);

            if (normal != null)
                entry.imageTarget.sprite = normal;
        }
    }

    private Button GetHoveredButton()
    {
        if (!Input.mousePresent || EventSystem.current == null)
            return null;

        _raycastResults.Clear();

        var data = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        EventSystem.current.RaycastAll(data, _raycastResults);

        for (int i = 0; i < _raycastResults.Count; i++)
        {
            var result = _raycastResults[i];
            if (result.gameObject == null)
                continue;

            Button button = result.gameObject.GetComponentInParent<Button>();
            if (button == null || !button.isActiveAndEnabled || !button.interactable)
                continue;

            for (int j = 0; j < buttonSpriteVisuals.Count; j++)
            {
                var entry = buttonSpriteVisuals[j];
                if (entry != null && entry.button == button)
                    return button;
            }
        }

        return null;
    }
}
