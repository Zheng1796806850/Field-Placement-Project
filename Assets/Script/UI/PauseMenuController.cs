using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;

public class PauseMenuController : MonoBehaviour
{
    [Serializable]
    private class ButtonHoverVisual
    {
        public Button button;
        public Image imageTarget;
        public Sprite normalSprite;
        public Sprite hoverSprite;
    }

    [Serializable]
    private class PanelAnimationTarget
    {
        public GameObject panel;
        public bool useAnimation = true;
    }

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
    private bool _externalPauseBlocked;

    [Header("Panel Pop Animation")]
    [SerializeField] private bool usePanelPopAnimation = true;
    [Tooltip("Configure which panels should use pop animation. You can add/remove entries per scene/prefab.")]
    [SerializeField] private List<PanelAnimationTarget> panelAnimationTargets = new List<PanelAnimationTarget>();
    [SerializeField, Min(0.01f)] private float panelShowDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float panelHideDuration = 0.16f;
    [SerializeField, Range(0.01f, 1f)] private float panelHiddenScaleMultiplier = 0.75f;

    [Header("Button Hover Sprite Swap")]
    [SerializeField] private bool enableHoverSpriteSwap = true;
    [SerializeField] private List<ButtonHoverVisual> buttonHoverVisuals = new List<ButtonHoverVisual>();

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
    private readonly Dictionary<GameObject, Coroutine> _panelAnimRoutines = new Dictionary<GameObject, Coroutine>();
    private readonly Dictionary<GameObject, Vector3> _panelBaseScales = new Dictionary<GameObject, Vector3>();
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(16);
    private readonly Dictionary<Image, Sprite> _buttonNormalSprites = new Dictionary<Image, Sprite>();

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

        CachePanelBaseScale(pauseMenuRoot);
        CachePanelBaseScale(settingsPanel);
        EnsurePanelAnimationEntry(pauseMenuRoot, true);
        EnsurePanelAnimationEntry(settingsPanel, true);
        CacheButtonSprites();

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

    private void ApplyCanvasGroupInteraction(bool interactableAndRaycast)
    {
        if (_canvasGroup == null)
            return;

        _canvasGroup.interactable = interactableAndRaycast;
        _canvasGroup.blocksRaycasts = interactableAndRaycast;
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
        if (!enableEscToggle || _externalPauseBlocked)
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

    public void SetExternalPauseBlocked(bool blocked)
    {
        _externalPauseBlocked = blocked;
        if (_externalPauseBlocked && _isOpen)
            ClosePauseMenu();
    }

    private void LateUpdate()
    {
        if (!enableHoverSpriteSwap)
            return;

        RefreshHoveredButtonSpriteSwap();
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

        bool pauseRootWillAnimateHide = usePanelPopAnimation && IsPanelAnimationEnabled(pauseMenuRoot);
        if (pauseRootWillAnimateHide)
            ApplyCanvasGroupInteraction(false);
        else
            ApplyCanvasGroupVisible(false);
        if (pauseMenuRoot != null)
            SetPanelVisible(pauseMenuRoot, false, instant: false);

        if (settingsPanel != null)
        {
            ApplySettingsPanelVisible(false);
            SetPanelVisible(settingsPanel, false, instant: false);
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
            SetPanelVisible(pauseMenuRoot, false, instant: false);
        }

        if (settingsPanel != null)
        {
            SetPanelVisible(settingsPanel, true, instant: false);
            ApplySettingsPanelVisible(true);
        }
    }

    private void ShowTutorial()
    {
        if (pauseMenuRoot != null)
        {
            ApplyCanvasGroupVisible(false);
            SetPanelVisible(pauseMenuRoot, false, instant: false);
        }

        if (settingsPanel != null)
        {
            ApplySettingsPanelVisible(false);
            SetPanelVisible(settingsPanel, false, instant: false);
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
            SetPanelVisible(settingsPanel, false, instant: false);
        }

        if (pauseMenuRoot != null)
        {
            SetPanelVisible(pauseMenuRoot, true, instant: false);
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
        _interactor = _interactor != null ? _interactor : UnityEngine.Object.FindFirstObjectByType<PlayerInteractor2D>(FindObjectsInactive.Include);
        if (_interactor != null)
        {
            _prevInteractorInputEnabled = _interactor.InputEnabled;
            _interactor.SetInputEnabled(false);
        }

        _combat = _combat != null ? _combat : UnityEngine.Object.FindFirstObjectByType<PlayerCombat2D>(FindObjectsInactive.Include);
        if (_combat != null)
            _combat.SetInputEnabled(false);

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

    private void CachePanelBaseScale(GameObject panel)
    {
        if (panel == null || _panelBaseScales.ContainsKey(panel))
            return;

        _panelBaseScales[panel] = ResolveBaseScale(panel);
    }

    private void SetPanelVisible(GameObject panel, bool visible, bool instant)
    {
        if (panel == null)
            return;

        CachePanelBaseScale(panel);
        StopPanelAnimation(panel);

        bool animateThisPanel = usePanelPopAnimation && IsPanelAnimationEnabled(panel);
        if (instant || !animateThisPanel)
        {
            panel.SetActive(visible);
            if (visible)
                panel.transform.localScale = GetCachedBaseScale(panel);
            return;
        }

        _panelAnimRoutines[panel] = StartCoroutine(AnimatePanelScale(panel, visible));
    }

    private void EnsurePanelAnimationEntry(GameObject panel, bool enabledByDefault)
    {
        if (panel == null)
            return;

        for (int i = 0; i < panelAnimationTargets.Count; i++)
        {
            PanelAnimationTarget entry = panelAnimationTargets[i];
            if (entry != null && entry.panel == panel)
                return;
        }

        panelAnimationTargets.Add(new PanelAnimationTarget
        {
            panel = panel,
            useAnimation = enabledByDefault
        });
    }

    private bool IsPanelAnimationEnabled(GameObject panel)
    {
        if (panel == null)
            return false;

        for (int i = 0; i < panelAnimationTargets.Count; i++)
        {
            PanelAnimationTarget entry = panelAnimationTargets[i];
            if (entry != null && entry.panel == panel)
                return entry.useAnimation;
        }

        return false;
    }

    private IEnumerator AnimatePanelScale(GameObject panel, bool show)
    {
        if (panel == null)
            yield break;

        Vector3 baseScale = GetCachedBaseScale(panel);

        Vector3 hiddenScale = baseScale * panelHiddenScaleMultiplier;
        float duration = Mathf.Max(0.01f, show ? panelShowDuration : panelHideDuration);

        if (show)
        {
            panel.SetActive(true);
            panel.transform.localScale = hiddenScale;
        }

        Vector3 from = show ? hiddenScale : panel.transform.localScale;
        Vector3 to = show ? baseScale : hiddenScale;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = show ? 1f - Mathf.Pow(1f - k, 3f) : k * k;
            panel.transform.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        panel.transform.localScale = to;
        if (!show)
        {
            panel.SetActive(false);
            if (panel == pauseMenuRoot)
                ApplyCanvasGroupVisible(false);
        }

        _panelAnimRoutines.Remove(panel);
    }

    private Vector3 ResolveBaseScale(GameObject panel)
    {
        if (panel != null)
            return panel.transform.localScale;

        return Vector3.one;
    }

    private Vector3 GetCachedBaseScale(GameObject panel)
    {
        if (panel == null)
            return Vector3.one;

        if (_panelBaseScales.TryGetValue(panel, out Vector3 baseScale))
            return baseScale;

        baseScale = ResolveBaseScale(panel);
        _panelBaseScales[panel] = baseScale;
        return baseScale;
    }

    private void StopPanelAnimation(GameObject panel)
    {
        if (panel == null)
            return;

        if (_panelAnimRoutines.TryGetValue(panel, out Coroutine co) && co != null)
            StopCoroutine(co);
        _panelAnimRoutines.Remove(panel);
    }

    private void CacheButtonSprites()
    {
        _buttonNormalSprites.Clear();

        EnsureHoverVisualEntry(resumeButton);
        EnsureHoverVisualEntry(tutorialButton);
        EnsureHoverVisualEntry(settingsButton);
        EnsureHoverVisualEntry(mainMenuButton);

        for (int i = 0; i < buttonHoverVisuals.Count; i++)
        {
            ButtonHoverVisual entry = buttonHoverVisuals[i];
            if (entry == null || entry.button == null)
                continue;

            if (entry.imageTarget == null)
                entry.imageTarget = entry.button.targetGraphic as Image;

            if (entry.imageTarget == null)
                continue;

            if (entry.normalSprite == null)
                entry.normalSprite = entry.imageTarget.sprite;

            if (!_buttonNormalSprites.ContainsKey(entry.imageTarget))
                _buttonNormalSprites.Add(entry.imageTarget, entry.imageTarget.sprite);
        }
    }

    private void EnsureHoverVisualEntry(Button button)
    {
        if (button == null)
            return;

        for (int i = 0; i < buttonHoverVisuals.Count; i++)
        {
            ButtonHoverVisual entry = buttonHoverVisuals[i];
            if (entry != null && entry.button == button)
                return;
        }

        buttonHoverVisuals.Add(new ButtonHoverVisual
        {
            button = button,
            imageTarget = button.targetGraphic as Image
        });
    }

    private void RefreshHoveredButtonSpriteSwap()
    {
        if (EventSystem.current == null)
            return;

        Button hovered = GetHoveredTrackedButton();
        for (int i = 0; i < buttonHoverVisuals.Count; i++)
        {
            ButtonHoverVisual entry = buttonHoverVisuals[i];
            if (entry == null || entry.button == null)
                continue;

            Image img = entry.imageTarget;
            if (img == null)
                continue;

            Sprite normal = entry.normalSprite;
            if (normal == null)
                normal = _buttonNormalSprites.TryGetValue(img, out Sprite cached) ? cached : img.sprite;

            Sprite desired = (entry.button == hovered && entry.hoverSprite != null)
                ? entry.hoverSprite
                : normal;

            if (desired != null && img.sprite != desired)
                img.sprite = desired;
        }
    }

    private Button GetHoveredTrackedButton()
    {
        _raycastResults.Clear();
        var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        EventSystem.current.RaycastAll(data, _raycastResults);

        for (int i = 0; i < _raycastResults.Count; i++)
        {
            var go = _raycastResults[i].gameObject;
            if (go == null) continue;

            Button b = go.GetComponentInParent<Button>();
            if (b == null || !b.isActiveAndEnabled || !b.interactable)
                continue;

            for (int j = 0; j < buttonHoverVisuals.Count; j++)
            {
                ButtonHoverVisual entry = buttonHoverVisuals[j];
                if (entry != null && entry.button == b)
                    return b;
            }
        }

        return null;
    }
}

