using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StoryEndingController : MonoBehaviour
{
    [Header("General")]
    public CanvasGroup bad01PanelGroup;
    public CanvasGroup bad02PanelGroup;
    public GameObject[] uiToHideDuringEnding = System.Array.Empty<GameObject>();
    [Min(0.05f)] public float panelFadeDuration = 0.8f;
    public string mainMenuSceneName = "MainMenu";
    public int mainMenuSceneBuildIndex = -1;
    public string loadingSceneName = "Loading";
    public int loadingSceneBuildIndex = -1;
    public bool useLoadingScene = true;
    public string loadingTitle = "Returning to Main Menu";
    public string readyPrompt = "Click Anywhere To Start";
    [Min(0f)] public float minimumLoadingScreenTime = 0.15f;

    [Header("Bad01")]
    public List<Image> bad01ComicImages = new List<Image>();
    [Min(0.05f)] public float bad01PanelRevealInterval = 1f;
    public CanvasGroup bad01PressEnterGroup;
    public TMP_Text bad01PressEnterText;

    [Header("Bad02")]
    public TMP_Text bad02Text;
    public CanvasGroup bad02TextGroup;
    [TextArea] public string bad02Line1 = "These days... how much longer will they last?";
    [TextArea] public string bad02Line2 = "There's no answer-this world only responds with the sound of snarling.";
    [Min(0.05f)] public float bad02TextFadeDuration = 0.6f;
    [Min(0f)] public float bad02TextHoldDuration = 2f;
    public CanvasGroup bad02PressEnterGroup;
    public TMP_Text bad02PressEnterText;

    [Header("Input")]
    public KeyCode continueKey = KeyCode.Return;

    [Header("Refs (auto-find if null)")]
    public GameStateManager gameStateManager;
    public PlayerMovementController playerMovement;
    public PlayerCombat2D playerCombat;
    public PlayerInteractor2D playerInteractor;
    public PauseMenuController pauseMenuController;
    public BackpackPanelHUD backpackHud;
    public NpcDialoguePanelHUD dialogueHud;

    bool _isPlaying;
    bool _isMainMenuLoading;

    public bool IsPlaying => _isPlaying;

    void Awake()
    {
        ResolveRefs();
        ResetInitialState();
    }

    void Update()
    {
        if (!_isPlaying || _isMainMenuLoading)
            return;

        if (Input.GetKeyDown(continueKey))
            ReturnToMainMenu();
    }

    public bool PlayEnding(EndingType endingType)
    {
        if (_isPlaying)
            return false;

        StartCoroutine(PlayEndingRoutine(endingType));
        return true;
    }

    IEnumerator PlayEndingRoutine(EndingType endingType)
    {
        _isPlaying = true;
        ResolveRefs();
        ForceStopDialogueIfAny();
        LockGameplayForEnding();
        HideNonEndingUi();

        if (endingType == EndingType.Bad01CompletedPitQuest)
            yield return PlayBad01Routine();
        else
            yield return PlayBad02Routine();
    }

    IEnumerator PlayBad01Routine()
    {
        if (bad01PanelGroup != null)
        {
            bad01PanelGroup.gameObject.SetActive(true);
            yield return FadeCanvasGroup(bad01PanelGroup, 0f, 1f, panelFadeDuration);
        }

        for (int i = 0; i < bad01ComicImages.Count; i++)
        {
            var img = bad01ComicImages[i];
            if (img == null)
                continue;
            img.gameObject.SetActive(true);
            SetGraphicAlpha(img, 1f);
            yield return WaitUnscaled(bad01PanelRevealInterval);
        }

        if (bad01PressEnterText != null)
            bad01PressEnterText.text = "Press Enter to Main Menu";
        if (bad01PressEnterGroup != null)
        {
            bad01PressEnterGroup.gameObject.SetActive(true);
            yield return FadeCanvasGroup(bad01PressEnterGroup, 0f, 1f, panelFadeDuration);
        }
    }

    IEnumerator PlayBad02Routine()
    {
        if (bad02PanelGroup != null)
        {
            bad02PanelGroup.gameObject.SetActive(true);
            yield return FadeCanvasGroup(bad02PanelGroup, 0f, 1f, panelFadeDuration);
        }

        if (bad02Text != null && bad02TextGroup != null)
        {
            bad02Text.text = bad02Line1;
            yield return FadeCanvasGroup(bad02TextGroup, 0f, 1f, bad02TextFadeDuration);
            yield return WaitUnscaled(bad02TextHoldDuration);
            yield return FadeCanvasGroup(bad02TextGroup, 1f, 0f, bad02TextFadeDuration);

            bad02Text.text = bad02Line2;
            yield return FadeCanvasGroup(bad02TextGroup, 0f, 1f, bad02TextFadeDuration);
            yield return WaitUnscaled(bad02TextHoldDuration);
            yield return FadeCanvasGroup(bad02TextGroup, 1f, 0f, bad02TextFadeDuration);
        }

        if (bad02PressEnterText != null)
            bad02PressEnterText.text = "Press Enter to Main Menu";
        if (bad02PressEnterGroup != null)
        {
            bad02PressEnterGroup.gameObject.SetActive(true);
            yield return FadeCanvasGroup(bad02PressEnterGroup, 0f, 1f, panelFadeDuration);
        }
    }

    void ResolveRefs()
    {
        if (gameStateManager == null) gameStateManager = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovementController>(FindObjectsInactive.Include);
        if (playerCombat == null) playerCombat = FindFirstObjectByType<PlayerCombat2D>(FindObjectsInactive.Include);
        if (playerInteractor == null) playerInteractor = FindFirstObjectByType<PlayerInteractor2D>(FindObjectsInactive.Include);
        if (pauseMenuController == null) pauseMenuController = FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);
        if (backpackHud == null) backpackHud = FindFirstObjectByType<BackpackPanelHUD>(FindObjectsInactive.Include);
        if (dialogueHud == null) dialogueHud = NpcDialoguePanelHUD.Instance != null ? NpcDialoguePanelHUD.Instance : FindFirstObjectByType<NpcDialoguePanelHUD>(FindObjectsInactive.Include);
    }

    void ForceStopDialogueIfAny()
    {
        if (dialogueHud != null && dialogueHud.IsRunning)
            dialogueHud.ForceCloseDialogue();
    }

    void LockGameplayForEnding()
    {
        if (gameStateManager != null)
            gameStateManager.SetPaused(true);
        else
            Time.timeScale = 0f;

        if (playerMovement != null) playerMovement.SetCanMove(false);
        if (playerInteractor != null) playerInteractor.SetInputEnabled(false);
        if (playerCombat != null) playerCombat.PushExternalInputBlock();
        if (pauseMenuController != null) pauseMenuController.PushExternalPauseBlock();
        if (backpackHud != null) backpackHud.enableToggleKey = false;
    }

    void HideNonEndingUi()
    {
        for (int i = 0; i < uiToHideDuringEnding.Length; i++)
        {
            var go = uiToHideDuringEnding[i];
            if (go == null)
            {
                Debug.LogWarning($"[StoryEndingController] uiToHideDuringEnding[{i}] is null.");
                continue;
            }

            if ((bad01PanelGroup != null && go == bad01PanelGroup.gameObject) ||
                (bad02PanelGroup != null && go == bad02PanelGroup.gameObject))
                continue;

            go.SetActive(false);
        }
    }

    void ResetInitialState()
    {
        SetGroupHidden(bad01PanelGroup);
        SetGroupHidden(bad02PanelGroup);
        SetGroupHidden(bad01PressEnterGroup);
        SetGroupHidden(bad02PressEnterGroup);
        SetGroupHidden(bad02TextGroup);

        for (int i = 0; i < bad01ComicImages.Count; i++)
            SetGraphicHidden(bad01ComicImages[i]);
    }

    void SetGroupHidden(CanvasGroup g)
    {
        if (g == null) return;
        g.alpha = 0f;
        g.interactable = false;
        g.blocksRaycasts = false;
        g.gameObject.SetActive(false);
    }

    void SetGraphicHidden(Graphic g)
    {
        if (g == null) return;
        SetGraphicAlpha(g, 0f);
        g.gameObject.SetActive(false);
    }

    static void SetGraphicAlpha(Graphic g, float a)
    {
        if (g == null) return;
        var c = g.color;
        g.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
    }

    IEnumerator FadeCanvasGroup(CanvasGroup g, float from, float to, float duration)
    {
        if (g == null)
            yield break;

        g.gameObject.SetActive(true);
        g.interactable = false;
        g.blocksRaycasts = false;
        g.alpha = from;
        float dur = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }

        g.alpha = to;
    }

    IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        float dur = Mathf.Max(0f, seconds);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    void ReturnToMainMenu()
    {
        if (_isMainMenuLoading)
            return;
        _isMainMenuLoading = true;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (useLoadingScene)
        {
            SceneLoadRequest.SetRequest(
                mainMenuSceneName,
                mainMenuSceneBuildIndex,
                LoadSceneMode.Single,
                loadingTitle,
                readyPrompt,
                minimumLoadingScreenTime
            );

            AsyncOperation op = null;
            if (loadingSceneBuildIndex >= 0)
                op = SceneManager.LoadSceneAsync(loadingSceneBuildIndex, LoadSceneMode.Single);
            else if (!string.IsNullOrWhiteSpace(loadingSceneName))
                op = SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);

            if (op == null)
            {
                SceneLoadRequest.Clear();
                DirectLoadMainMenu();
            }

            return;
        }

        SceneLoadRequest.Clear();
        DirectLoadMainMenu();
    }

    void DirectLoadMainMenu()
    {
        if (mainMenuSceneBuildIndex >= 0)
            SceneManager.LoadScene(mainMenuSceneBuildIndex, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }
}

