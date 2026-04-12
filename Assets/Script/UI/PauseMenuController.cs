using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [Header("Pause UI")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private GameObject settingsPanel;

    [SerializeField] private Button resumeButton;
    [SerializeField] private Button tutorialButton; // disabled for now
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private TutorialPanelController tutorialPanelController;

    [Header("ESC Toggle")]
    [SerializeField] private bool enableEscToggle = true;
    [SerializeField] private KeyCode escKey = KeyCode.Escape;

    [Header("Main Menu Return (Loading Flow)")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private int mainMenuSceneBuildIndex = -1;

    [SerializeField] private string loadingSceneName = "Loading";
    [SerializeField] private int loadingSceneBuildIndex = -1;

    [SerializeField] private LoadSceneMode targetLoadSceneMode = LoadSceneMode.Single;
    [SerializeField] private string loadingTitle = "Loading";
    [SerializeField] private string readyPrompt = "Click Anywhere To Start";
    [SerializeField] private float minimumLoadingScreenTime = 0.25f;

    private bool _isOpen;
    private bool _pausedByThisMenu;

    private bool _prevCursorVisible;
    private CursorLockMode _prevCursorLock;

    private CanvasGroup _canvasGroup;
    private CanvasGroup _settingsCanvasGroup;

    private PlayerInteractor2D _interactor;
    private bool _prevInteractorInputEnabled;

    private PlayerCombat2D _combat;

    private void Awake()
    {
        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(false);
            _canvasGroup = pauseMenuRoot.GetComponent<CanvasGroup>();
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            _settingsCanvasGroup = settingsPanel.GetComponent<CanvasGroup>();
        }

        if (tutorialButton != null)
            tutorialButton.interactable = true;

        // Ensure initial visual + interaction state.
        ApplyCanvasGroupVisible(false);

        WireButtons();

        if (tutorialPanelController != null)
            tutorialPanelController.OnTutorialCompleted += HandleTutorialCompleted;
    }

    private void OnDisable()
    {
        // Safety: if this pause menu gets disabled during a phase/UI swap while it paused the game,
        // we must restore time and input. Otherwise the game can remain frozen with no visible menu.
        ForceUnpauseIfNeeded();
    }

    private void OnDestroy()
    {
        ForceUnpauseIfNeeded();
        if (tutorialPanelController != null)
            tutorialPanelController.OnTutorialCompleted -= HandleTutorialCompleted;
    }

    private void ForceUnpauseIfNeeded()
    {
        if (!_pausedByThisMenu)
            return;

        _pausedByThisMenu = false;
        _isOpen = false;

        var gsm = GameStateManager.Instance;
        if (gsm != null)
            gsm.SetPaused(false);
        else
            Time.timeScale = 1f;

        // Best-effort restore of input/cursor in case we paused.
        RestorePlayerInput();
        ApplyCursorForPause(false);
    }

    private void ApplyCanvasGroupVisible(bool visible)
    {
        if (_canvasGroup == null)
            return;

        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
    }

    private void WireButtons()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(Resume);
            resumeButton.onClick.AddListener(Resume);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(ShowSettings);
            settingsButton.onClick.AddListener(ShowSettings);
        }

        if (tutorialButton != null)
        {
            tutorialButton.onClick.RemoveListener(ShowTutorial);
            tutorialButton.onClick.AddListener(ShowTutorial);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(GoToMainMenu);
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }
    }

    private void Update()
    {
        if (!enableEscToggle)
            return;

        if (!Input.GetKeyDown(escKey))
            return;

        if (!_isOpen)
        {
            OpenPauseMenu();
            return;
        }

        // If tutorial is open, ESC should go back to pause panel first.
        if (IsTutorialOpen())
        {
            ShowPausePanelOnly();
            return;
        }

        // If settings is open, ESC should go back to pause panel.
        if (IsSettingsOpen())
        {
            ShowPausePanelOnly();
            return;
        }

        // If pause panel is open, ESC closes all pause UI.
        ClosePauseMenu();
    }

    public void OpenPauseMenu()
    {
        if (_isOpen) return;

        var gsm = GameStateManager.Instance;
        _isOpen = true;
        _pausedByThisMenu = true;

        // Pause first to freeze gameplay time.
        if (gsm != null)
            gsm.SetPaused(true);
        else
            Time.timeScale = 0f;

        CacheAndDisablePlayerInput();
        ApplyCursorForPause(true);

        ShowPausePanelOnly();
    }

    public void ClosePauseMenu()
    {
        if (!_isOpen) return;

        _isOpen = false;
        _pausedByThisMenu = false;

        var gsm = GameStateManager.Instance;
        // Restore time.
        if (gsm != null)
            gsm.SetPaused(false);
        else
            Time.timeScale = 1f;

        RestorePlayerInput();
        ApplyCursorForPause(false);

        ApplyCanvasGroupVisible(false);
        if (pauseMenuRoot != null)
            pauseMenuRoot.SetActive(false);

        if (settingsPanel != null)
        {
            ApplySettingsPanelVisible(false);
            settingsPanel.SetActive(false);
        }
    }

    public void Resume()
    {
        ClosePauseMenu();
    }

    /// <summary>
    /// Wire the settings Back button to this (not only ESC): reverts un-applied changes and returns to pause root.
    /// </summary>
    public void BackFromSettings()
    {
        if (settingsPanel != null)
        {
            var shell = settingsPanel.GetComponentInChildren<SettingsShellController>(true);
            if (shell != null)
                shell.BackWithoutCloseEvent();
            else
                SettingsManager.Instance?.EndSettingsSessionIfAborted();
        }

        ShowPausePanelOnly();
    }

    private void ShowSettings()
    {
        // Open settings and hide pause panel visuals.
        if (pauseMenuRoot != null)
        {
            ApplyCanvasGroupVisible(false);
            pauseMenuRoot.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            ApplySettingsPanelVisible(true);
        }
    }

    private void ShowTutorial()
    {
        if (pauseMenuRoot != null)
        {
            ApplyCanvasGroupVisible(false);
            pauseMenuRoot.SetActive(false);
        }

        if (settingsPanel != null)
        {
            ApplySettingsPanelVisible(false);
            settingsPanel.SetActive(false);
        }

        if (tutorialPanelController != null)
            tutorialPanelController.BeginTutorial();
    }

    private void ShowPausePanelOnly()
    {
        if (tutorialPanelController != null && tutorialPanelController.IsOpen)
            tutorialPanelController.HidePanelOnly();

        if (settingsPanel != null)
        {
            ApplySettingsPanelVisible(false);
            settingsPanel.SetActive(false);
        }

        if (pauseMenuRoot != null)
        {
            pauseMenuRoot.SetActive(true);
            ApplyCanvasGroupVisible(true);
        }
    }

    private bool IsSettingsOpen()
    {
        return settingsPanel != null && settingsPanel.activeSelf;
    }

    private bool IsTutorialOpen()
    {
        return tutorialPanelController != null && tutorialPanelController.IsOpen;
    }

    private void HandleTutorialCompleted()
    {
        if (_isOpen)
            ShowPausePanelOnly();
    }

    private void ApplySettingsPanelVisible(bool visible)
    {
        if (_settingsCanvasGroup == null)
            return;

        _settingsCanvasGroup.alpha = visible ? 1f : 0f;
        _settingsCanvasGroup.interactable = visible;
        _settingsCanvasGroup.blocksRaycasts = visible;
    }

    private void CacheAndDisablePlayerInput()
    {
        _interactor = _interactor != null ? _interactor : Object.FindFirstObjectByType<PlayerInteractor2D>(FindObjectsInactive.Include);
        if (_interactor != null)
        {
            _prevInteractorInputEnabled = _interactor.InputEnabled;
            _interactor.SetInputEnabled(false);
        }

        _combat = _combat != null ? _combat : Object.FindFirstObjectByType<PlayerCombat2D>(FindObjectsInactive.Include);
        if (_combat != null)
            _combat.SetInputEnabled(false);

        // Keep references for restoration; do not overwrite prev values after first pause open.
    }

    private void RestorePlayerInput()
    {
        if (_interactor != null)
            _interactor.SetInputEnabled(_prevInteractorInputEnabled);

        if (_combat != null)
            _combat.SetInputEnabled(true);
    }

    private void ApplyCursorForPause(bool paused)
    {
        if (paused)
        {
            _prevCursorVisible = Cursor.visible;
            _prevCursorLock = Cursor.lockState;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        Cursor.visible = _prevCursorVisible;
        Cursor.lockState = _prevCursorLock;
    }

    private void GoToMainMenu()
    {
        // Close pause immediately so state is consistent.
        ClosePauseMenu();

        SceneLoadRequest.SetRequest(
            mainMenuSceneName,
            mainMenuSceneBuildIndex,
            targetLoadSceneMode,
            loadingTitle,
            readyPrompt,
            minimumLoadingScreenTime
        );

        if (loadingSceneBuildIndex >= 0)
        {
            if (loadingSceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
                return;
            SceneManager.LoadSceneAsync(loadingSceneBuildIndex, LoadSceneMode.Single);
            return;
        }

        if (!string.IsNullOrWhiteSpace(loadingSceneName))
        {
            SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);
            return;
        }
    }
}

