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
    public UnityEvent onBeforeStartLoad;
    public UnityEvent onAfterButtonsLocked;

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
        if (isBusy)
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

        StartCoroutine(StartGameRoutine());
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

    private IEnumerator StartGameRoutine()
    {
        isBusy = true;
        SetButtonsInteractable(false);
        onAfterButtonsLocked?.Invoke();

        ApplyOpenState();
        RunStartInitialization();
        onBeforeStartLoad?.Invoke();

        yield return null;

        AsyncOperation op = useLoadingScene ? CreateLoadingFlowOperation() : CreateGameplayLoadOperation();
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

    private AsyncOperation CreateLoadingFlowOperation()
    {
        SceneLoadRequest.SetRequest(
            gameplaySceneName,
            gameplaySceneBuildIndex,
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

    private AsyncOperation CreateGameplayLoadOperation()
    {
        if (gameplaySceneBuildIndex >= 0)
        {
            if (gameplaySceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"[MainMenuUI] Gameplay scene build index {gameplaySceneBuildIndex} is out of range.");
                return null;
            }

            return SceneManager.LoadSceneAsync(gameplaySceneBuildIndex, loadSceneMode);
        }

        if (!string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            {
                Debug.LogError($"[MainMenuUI] Gameplay scene '{gameplaySceneName}' is not available in Build Settings.");
                return null;
            }

            return SceneManager.LoadSceneAsync(gameplaySceneName, loadSceneMode);
        }

        Debug.LogError("[MainMenuUI] No gameplay scene name or build index configured.");
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
    }

    private void CacheButtonVisuals()
    {
        EnsureButtonVisualEntry(startButton);
        EnsureButtonVisualEntry(settingsButton);
        EnsureButtonVisualEntry(creditsButton);
        EnsureButtonVisualEntry(quitButton);

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