using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// MENU-03a/b: tabbed settings shell — Audio / Controls / Gameplay panels, Apply & Back.
/// Back / ESC / panel disable: discards un-applied changes (reverts audio snapshot). Apply commits to disk.
/// </summary>
[DisallowMultipleComponent]
public class SettingsShellController : MonoBehaviour
{
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

    private int _currentTab;

    private void Awake()
    {
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
    }

    private void OnDisable()
    {
        SettingsManager.Instance?.EndSettingsSessionIfAborted();
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
}
