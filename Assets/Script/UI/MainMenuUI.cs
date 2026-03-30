using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Serializable]
    public class ButtonVisual
    {
        public Button button;
        public GameObject selectedRoot;
        public RectTransform scaleTarget;
    }

    [Header("Gameplay Target")]
    public string gameplaySceneName = "";
    public int gameplaySceneBuildIndex = -1;
    public LoadSceneMode loadSceneMode = LoadSceneMode.Single;

    [Header("Loading Scene")]
    public bool useLoadingScene = true;
    public string loadingSceneName = "";
    public int loadingSceneBuildIndex = -1;
    public string loadingTitle = "Loading";
    public string readyPrompt = "Click Anywhere To Start";
    [Min(0f)] public float minimumLoadingScreenTime = 0.25f;

    [Header("Buttons")]
    public Button startButton;
    public Button settingsButton;
    public Button creditsButton;
    public Button quitButton;
    public Button settingsCloseButton;
    public Button creditsCloseButton;
    public List<ButtonVisual> buttonVisuals = new List<ButtonVisual>();

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;
    public bool hideMainMenuWhenSubPanelOpen = false;

    [Header("Selection")]
    public bool selectDefaultOnEnable = true;
    public Button defaultSelectedButton;
    public bool hoverOnlySelection = true;
    public bool useScaleFeedback = true;
    [Min(1f)] public float selectedScale = 1.06f;
    [Min(0.01f)] public float normalScale = 1f;
    [Min(0.01f)] public float scaleLerpSpeed = 12f;

    [Header("Start Game")]
    public bool resetTimeScaleOnOpen = true;
    public bool resetAudioPauseOnOpen = true;
    public bool resetPersistentInventoryToDefaults = false;
    public bool clearInventorySaveOnStart = false;
    [Tooltip("Increment Base world run id so PlayerPrefs for wells/collectors use a fresh key (recommended for new game from menu). Disable to keep same Base run across menu restarts.")]
    public bool advanceBaseWorldRunOnStart = true;
    public UnityEvent onBeforeStartLoad;
    public UnityEvent onAfterButtonsLocked;

    [Header("Tutorial")]
    [Tooltip("If assigned, Start opens this panel first (Yes = tutorial scene, No = gameplay).")]
    public GameObject tutorialChoicePanel;
    public Button tutorialChoiceYesButton;
    public Button tutorialChoiceNoButton;
    [Tooltip("Child object shown on hover (same role as Button Visuals → Selected Root on Start/Settings).")]
    public GameObject tutorialChoiceYesSelectedRoot;
    [Tooltip("Child object shown on hover for the No button.")]
    public GameObject tutorialChoiceNoSelectedRoot;
    [Tooltip("Scene to load when the player chooses tutorial (e.g. TutorialScene).")]
    public string tutorialSceneName = "TutorialScene";
    public int tutorialSceneBuildIndex = -1;
    [Tooltip("Local collectorSaveKey values from WaterCollectorBuildSpot prefabs (e.g. wc_base_01). Cleared on every new game start.")]
    public List<string> waterCollectorLocalSaveKeysToClear = new List<string> { "wc_base_01" };
    [Tooltip("Legacy: in-menu slideshow before load when no choice panel is used.")]
    public bool useTutorialOnStart = false;
    public TutorialPanelController tutorialPanelController;

    [Header("Settings")]
    public UnityEvent onOpenSettings;
    public UnityEvent onCloseSettings;

    [Header("Credits")]
    public UnityEvent onOpenCredits;
    public UnityEvent onCloseCredits;

    [Header("Quit")]
    public bool quitOnEscape = true;
    public KeyCode quitKey = KeyCode.Escape;

    [Header("Runtime")]
    public bool isBusy;

    private bool _tutorialChoiceOpen;

    private readonly Dictionary<RectTransform, Vector3> baseScales = new Dictionary<RectTransform, Vector3>();
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(16);
    private Coroutine selectRoutine;

    private void Awake()
    {
        EnsureEventSystem();
        CacheButtonVisuals();
        WireButtons();
        ApplyOpenState();
        ApplyInitialPanelState();
        RefreshSelectionVisuals(true);
    }

    private void OnEnable()
    {
        ApplyOpenState();
        ApplyInitialPanelState();

        if (selectDefaultOnEnable)
        {
            if (selectRoutine != null)
                StopCoroutine(selectRoutine);

            selectRoutine = StartCoroutine(SelectDefaultNextFrame());
        }

        RefreshSelectionVisuals(true);
    }

    private void OnDisable()
    {
        if (selectRoutine != null)
        {
            StopCoroutine(selectRoutine);
            selectRoutine = null;
        }
    }

    private void Update()
    {
        if (!isBusy && quitOnEscape && quitKey != KeyCode.None && Input.GetKeyDown(quitKey))
        {
            if (tutorialChoicePanel != null && tutorialChoicePanel.activeSelf)
            {
                CloseTutorialChoicePanel();
                return;
            }

            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
                return;
            }

            if (creditsPanel != null && creditsPanel.activeSelf)
            {
                CloseCredits();
                return;
            }

            QuitGame();
            return;
        }

        RefreshSelectionVisuals(false);
    }

    public void StartGame()
    {
        if (isBusy || _tutorialChoiceOpen)
            return;

        if (!CanLoadGameplayScene())
        {
            Debug.LogError("[MainMenuUI] Gameplay scene is not configured or not found in Build Settings.");
            return;
        }

        if (useLoadingScene && !CanLoadLoadingScene())
        {
            Debug.LogError("[MainMenuUI] Loading scene is not configured or not found in Build Settings.");
            return;
        }

        if (tutorialChoicePanel != null)
        {
            OpenTutorialChoicePanel();
            return;
        }

        StartCoroutine(StartGameRoutine(enterTutorialLevel: false));
    }

    public void OpenTutorialChoicePanel()
    {
        if (tutorialChoicePanel == null || _tutorialChoiceOpen)
            return;

        _tutorialChoiceOpen = true;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        if (mainMenuPanel != null && hideMainMenuWhenSubPanelOpen)
            mainMenuPanel.SetActive(false);

        tutorialChoicePanel.SetActive(true);

        if (selectDefaultOnEnable && EventSystem.current != null && tutorialChoiceYesButton != null)
            EventSystem.current.SetSelectedGameObject(tutorialChoiceYesButton.gameObject);
    }

    public void CloseTutorialChoicePanel()
    {
        if (tutorialChoicePanel != null)
            tutorialChoicePanel.SetActive(false);

        if (mainMenuPanel != null && hideMainMenuWhenSubPanelOpen)
            mainMenuPanel.SetActive(true);

        _tutorialChoiceOpen = false;
    }

    public void OnTutorialChoiceYes()
    {
        if (!_tutorialChoiceOpen || isBusy)
            return;

        if (!CanLoadTutorialScene())
        {
            Debug.LogError("[MainMenuUI] Tutorial scene is not configured or not found in Build Settings.");
            return;
        }

        CloseTutorialChoicePanel();
        StartCoroutine(StartGameRoutine(enterTutorialLevel: true));
    }

    public void OnTutorialChoiceNo()
    {
        if (!_tutorialChoiceOpen || isBusy)
            return;

        CloseTutorialChoicePanel();
        StartCoroutine(StartGameRoutine(enterTutorialLevel: false));
    }

    public void OpenSettings()
    {
        if (isBusy)
            return;

        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        if (mainMenuPanel != null && hideMainMenuWhenSubPanelOpen)
            mainMenuPanel.SetActive(false);

        onOpenSettings?.Invoke();
        RefreshSelectionVisuals(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (mainMenuPanel != null && hideMainMenuWhenSubPanelOpen)
            mainMenuPanel.SetActive(true);

        onCloseSettings?.Invoke();
        RefreshSelectionVisuals(true);
    }

    public void OpenCredits()
    {
        if (isBusy)
            return;

        if (creditsPanel != null)
            creditsPanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (mainMenuPanel != null && hideMainMenuWhenSubPanelOpen)
            mainMenuPanel.SetActive(false);

        onOpenCredits?.Invoke();
        RefreshSelectionVisuals(true);
    }

    public void CloseCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        if (mainMenuPanel != null && hideMainMenuWhenSubPanelOpen)
            mainMenuPanel.SetActive(true);

        onCloseCredits?.Invoke();
        RefreshSelectionVisuals(true);
    }

    public void QuitGame()
    {
        if (isBusy)
            return;

        ApplyOpenState();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator StartGameRoutine(bool enterTutorialLevel)
    {
        isBusy = true;
        SetButtonsInteractable(false);
        onAfterButtonsLocked?.Invoke();

        ApplyOpenState();

        BaseWorldSession.DeleteWaterCollectorKeysForAllRuns(waterCollectorLocalSaveKeysToClear);

        // If we use the Yes/No tutorial panel, never run the legacy in-menu slideshow on "No" — it can stay incomplete forever and block loading.
        bool skipMenuTutorialSlideshow = tutorialChoicePanel != null;
        if (!skipMenuTutorialSlideshow && !enterTutorialLevel && useTutorialOnStart && tutorialPanelController != null)
        {
            tutorialPanelController.BeginTutorial();
            while (tutorialPanelController != null && !tutorialPanelController.IsCompleted)
                yield return null;
        }

        RunStartInitialization();
        onBeforeStartLoad?.Invoke();

        yield return null;

        AsyncOperation op = useLoadingScene
            ? CreateLoadingFlowOperation(enterTutorialLevel)
            : CreateGameplayLoadOperation(enterTutorialLevel);
        if (op == null)
        {
            isBusy = false;
            SetButtonsInteractable(true);
            yield break;
        }

        while (!op.isDone)
            yield return null;
    }

    private void RunStartInitialization()
    {
        if (advanceBaseWorldRunOnStart)
            BaseWorldSession.AdvanceRunGeneration();

        var inventory = PlayerResourceInventory.Instance;
        if (inventory == null)
            return;

        if (resetPersistentInventoryToDefaults)
        {
            inventory.ResetToDefaults(clearInventorySaveOnStart);
            return;
        }

        if (clearInventorySaveOnStart)
            inventory.ClearSave();
    }

    private AsyncOperation CreateLoadingFlowOperation(bool enterTutorialLevel)
    {
        string targetName = enterTutorialLevel ? tutorialSceneName : gameplaySceneName;
        int targetIndex = enterTutorialLevel ? tutorialSceneBuildIndex : gameplaySceneBuildIndex;

        SceneLoadRequest.SetRequest(
            targetName,
            targetIndex,
            loadSceneMode,
            loadingTitle,
            readyPrompt,
            minimumLoadingScreenTime
        );

        AsyncOperation op = CreateLoadingSceneOperation();
        if (op == null)
            SceneLoadRequest.Clear();

        return op;
    }

    private AsyncOperation CreateLoadingSceneOperation()
    {
        if (loadingSceneBuildIndex >= 0)
        {
            if (loadingSceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"[MainMenuUI] Loading scene build index {loadingSceneBuildIndex} is out of range.");
                return null;
            }

            return SceneManager.LoadSceneAsync(loadingSceneBuildIndex, LoadSceneMode.Single);
        }

        if (!string.IsNullOrWhiteSpace(loadingSceneName))
        {
            if (!Application.CanStreamedLevelBeLoaded(loadingSceneName))
            {
                Debug.LogError($"[MainMenuUI] Loading scene '{loadingSceneName}' is not available in Build Settings.");
                return null;
            }

            return SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);
        }

        Debug.LogError("[MainMenuUI] No loading scene name or build index configured.");
        return null;
    }

    private AsyncOperation CreateGameplayLoadOperation(bool enterTutorialLevel)
    {
        string name = enterTutorialLevel ? tutorialSceneName : gameplaySceneName;
        int index = enterTutorialLevel ? tutorialSceneBuildIndex : gameplaySceneBuildIndex;

        if (index >= 0)
        {
            if (index >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"[MainMenuUI] Target scene build index {index} is out of range.");
                return null;
            }

            return SceneManager.LoadSceneAsync(index, loadSceneMode);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            if (!Application.CanStreamedLevelBeLoaded(name))
            {
                Debug.LogError($"[MainMenuUI] Target scene '{name}' is not available in Build Settings.");
                return null;
            }

            return SceneManager.LoadSceneAsync(name, loadSceneMode);
        }

        Debug.LogError("[MainMenuUI] No target scene name or build index configured.");
        return null;
    }

    private bool CanLoadGameplayScene()
    {
        if (gameplaySceneBuildIndex >= 0)
            return gameplaySceneBuildIndex < SceneManager.sceneCountInBuildSettings;

        if (!string.IsNullOrWhiteSpace(gameplaySceneName))
            return Application.CanStreamedLevelBeLoaded(gameplaySceneName);

        return false;
    }

    private bool CanLoadTutorialScene()
    {
        if (tutorialSceneBuildIndex >= 0)
            return tutorialSceneBuildIndex < SceneManager.sceneCountInBuildSettings;

        if (!string.IsNullOrWhiteSpace(tutorialSceneName))
            return Application.CanStreamedLevelBeLoaded(tutorialSceneName);

        return false;
    }

    private bool CanLoadLoadingScene()
    {
        if (loadingSceneBuildIndex >= 0)
            return loadingSceneBuildIndex < SceneManager.sceneCountInBuildSettings;

        if (!string.IsNullOrWhiteSpace(loadingSceneName))
            return Application.CanStreamedLevelBeLoaded(loadingSceneName);

        return false;
    }

    private void WireButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
            startButton.onClick.AddListener(StartGame);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettings);
            settingsButton.onClick.AddListener(OpenSettings);
        }

        if (creditsButton != null)
        {
            creditsButton.onClick.RemoveListener(OpenCredits);
            creditsButton.onClick.AddListener(OpenCredits);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
            quitButton.onClick.AddListener(QuitGame);
        }

        if (settingsCloseButton != null)
        {
            settingsCloseButton.onClick.RemoveListener(CloseSettings);
            settingsCloseButton.onClick.AddListener(CloseSettings);
        }

        if (creditsCloseButton != null)
        {
            creditsCloseButton.onClick.RemoveListener(CloseCredits);
            creditsCloseButton.onClick.AddListener(CloseCredits);
        }

        if (tutorialChoiceYesButton != null)
        {
            tutorialChoiceYesButton.onClick.RemoveListener(OnTutorialChoiceYes);
            tutorialChoiceYesButton.onClick.AddListener(OnTutorialChoiceYes);
        }

        if (tutorialChoiceNoButton != null)
        {
            tutorialChoiceNoButton.onClick.RemoveListener(OnTutorialChoiceNo);
            tutorialChoiceNoButton.onClick.AddListener(OnTutorialChoiceNo);
        }
    }

    private void CacheButtonVisuals()
    {
        EnsureButtonVisualEntry(startButton);
        EnsureButtonVisualEntry(settingsButton);
        EnsureButtonVisualEntry(creditsButton);
        EnsureButtonVisualEntry(quitButton);

        EnsureButtonVisualEntry(tutorialChoiceYesButton);
        EnsureButtonVisualEntry(tutorialChoiceNoButton);
        AssignSelectedRootForButtonVisual(tutorialChoiceYesButton, tutorialChoiceYesSelectedRoot);
        AssignSelectedRootForButtonVisual(tutorialChoiceNoButton, tutorialChoiceNoSelectedRoot);

        if (defaultSelectedButton == null)
            defaultSelectedButton = startButton != null ? startButton : settingsButton;

        baseScales.Clear();

        for (int i = 0; i < buttonVisuals.Count; i++)
        {
            var entry = buttonVisuals[i];
            if (entry == null || entry.button == null)
                continue;

            if (entry.scaleTarget == null)
                entry.scaleTarget = entry.button.transform as RectTransform;

            if (entry.scaleTarget != null && !baseScales.ContainsKey(entry.scaleTarget))
                baseScales.Add(entry.scaleTarget, entry.scaleTarget.localScale);
        }
    }

    private void EnsureButtonVisualEntry(Button button)
    {
        if (button == null)
            return;

        for (int i = 0; i < buttonVisuals.Count; i++)
        {
            var entry = buttonVisuals[i];
            if (entry != null && entry.button == button)
                return;
        }

        buttonVisuals.Add(new ButtonVisual
        {
            button = button,
            scaleTarget = button.transform as RectTransform
        });
    }

    private void AssignSelectedRootForButtonVisual(Button button, GameObject selectedRoot)
    {
        if (button == null || selectedRoot == null) return;

        for (int i = 0; i < buttonVisuals.Count; i++)
        {
            ButtonVisual entry = buttonVisuals[i];
            if (entry == null || entry.button != button) continue;
            entry.selectedRoot = selectedRoot;
            return;
        }
    }

    private void ApplyOpenState()
    {
        if (resetTimeScaleOnOpen)
            Time.timeScale = 1f;

        if (resetAudioPauseOnOpen)
            AudioListener.pause = false;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ApplyInitialPanelState()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        if (tutorialChoicePanel != null)
            tutorialChoicePanel.SetActive(false);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private IEnumerator SelectDefaultNextFrame()
    {
        yield return null;

        EnsureEventSystem();

        if (defaultSelectedButton != null && defaultSelectedButton.isActiveAndEnabled && defaultSelectedButton.interactable && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(defaultSelectedButton.gameObject);

        selectRoutine = null;
    }

    private void RefreshSelectionVisuals(bool instant)
    {
        Button target = ResolveVisualTarget();

        for (int i = 0; i < buttonVisuals.Count; i++)
        {
            var entry = buttonVisuals[i];
            if (entry == null || entry.button == null)
                continue;

            bool selected = entry.button == target;

            if (entry.selectedRoot != null)
                entry.selectedRoot.SetActive(selected);

            if (!useScaleFeedback || entry.scaleTarget == null)
                continue;

            if (!baseScales.TryGetValue(entry.scaleTarget, out Vector3 baseScale))
                baseScale = entry.scaleTarget.localScale;

            Vector3 desired = baseScale * (selected ? selectedScale : normalScale);

            if (instant)
                entry.scaleTarget.localScale = desired;
            else
                entry.scaleTarget.localScale = Vector3.Lerp(entry.scaleTarget.localScale, desired, Time.unscaledDeltaTime * scaleLerpSpeed);
        }
    }

    private Button ResolveVisualTarget()
    {
        Button hovered = GetHoveredButton();
        if (hovered != null)
            return hovered;

        if (hoverOnlySelection)
            return null;

        return GetSelectedButton();
    }

    private Button GetSelectedButton()
    {
        if (EventSystem.current == null)
            return null;

        GameObject go = EventSystem.current.currentSelectedGameObject;
        if (go == null)
            return null;

        return go.GetComponentInParent<Button>();
    }

    private Button GetHoveredButton()
    {
        if (!Input.mousePresent || EventSystem.current == null)
            return null;

        raycastResults.Clear();

        var data = new PointerEventData(EventSystem.current);
        data.position = Input.mousePosition;

        EventSystem.current.RaycastAll(data, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            var result = raycastResults[i];
            if (result.gameObject == null)
                continue;

            Button button = result.gameObject.GetComponentInParent<Button>();
            if (button == null)
                continue;

            if (!IsTrackedButton(button))
                continue;

            if (!button.isActiveAndEnabled || !button.interactable)
                continue;

            return button;
        }

        return null;
    }

    private bool IsTrackedButton(Button button)
    {
        if (button == null)
            return false;

        if (button == startButton || button == settingsButton || button == creditsButton || button == quitButton)
            return true;

        for (int i = 0; i < buttonVisuals.Count; i++)
        {
            var entry = buttonVisuals[i];
            if (entry != null && entry.button == button)
                return true;
        }

        return false;
    }

    private void SetButtonsInteractable(bool value)
    {
        if (startButton != null)
            startButton.interactable = value;

        if (settingsButton != null)
            settingsButton.interactable = value;

        if (creditsButton != null)
            creditsButton.interactable = value;

        if (quitButton != null)
            quitButton.interactable = value;

        if (settingsCloseButton != null)
            settingsCloseButton.interactable = value;

        if (creditsCloseButton != null)
            creditsCloseButton.interactable = value;

        for (int i = 0; i < buttonVisuals.Count; i++)
        {
            var entry = buttonVisuals[i];
            if (entry == null || entry.button == null)
                continue;

            entry.button.interactable = value;
        }
    }
}