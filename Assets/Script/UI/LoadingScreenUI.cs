using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreenUI : MonoBehaviour
{
    [Header("Fallback Target")]
    public string fallbackTargetSceneName = "";
    public int fallbackTargetSceneBuildIndex = -1;
    public LoadSceneMode fallbackLoadSceneMode = LoadSceneMode.Single;

    [Header("UI")]
    public TMP_Text titleLabel;
    public TMP_Text statusLabel;
    public TMP_Text progressLabel;
    public TMP_Text readyLabel;
    public Slider progressSlider;
    public Image progressFill;
    public GameObject readyRoot;

    [Header("Text")]
    public string loadingStatusText = "Loading...";
    public string readyStatusText = "Ready";
    public string startingStatusText = "Starting...";

    [Header("Progress")]
    public bool showPercentText = true;
    [Min(0.01f)] public float progressSmoothSpeed = 2.5f;

    [Header("Input")]
    public bool allowAnyKeyDown = true;
    public bool allowTouch = true;
    [Min(0f)] public float readyInputBlockDuration = 0.15f;

    private float displayedProgress;
    private bool readyShown;
    private float readyShownTime;

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (readyRoot != null)
            readyRoot.SetActive(false);

        if (progressSlider != null)
            progressSlider.value = 0f;

        if (progressFill != null)
            progressFill.fillAmount = 0f;

        if (progressLabel != null)
            progressLabel.text = showPercentText ? "0%" : "";

        if (statusLabel != null)
            statusLabel.text = loadingStatusText;

        if (titleLabel != null)
            titleLabel.text = SceneLoadRequest.HasPendingRequest ? SceneLoadRequest.LoadingTitle : "Loading";

        if (readyLabel != null)
            readyLabel.text = SceneLoadRequest.HasPendingRequest ? SceneLoadRequest.ReadyPrompt : "Click Anywhere To Start";
    }

    private void Start()
    {
        StartCoroutine(LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        string targetSceneName;
        int targetSceneBuildIndex;
        LoadSceneMode mode;
        float minimumLoadingScreenTime;
        string prompt;

        ResolveRequest(out targetSceneName, out targetSceneBuildIndex, out mode, out minimumLoadingScreenTime, out prompt);

        if (titleLabel != null && SceneLoadRequest.HasPendingRequest)
            titleLabel.text = SceneLoadRequest.LoadingTitle;

        if (readyLabel != null)
            readyLabel.text = prompt;

        if (!HasValidTarget(targetSceneName, targetSceneBuildIndex))
        {
            if (statusLabel != null)
                statusLabel.text = "Target Scene Missing";
            yield break;
        }

        AsyncOperation op = CreateLoadOperation(targetSceneName, targetSceneBuildIndex, mode);
        if (op == null)
        {
            if (statusLabel != null)
                statusLabel.text = "Load Failed";
            yield break;
        }

        op.allowSceneActivation = false;

        float elapsed = 0f;
        displayedProgress = 0f;
        readyShown = false;
        readyShownTime = -999f;

        while (!op.isDone)
        {
            elapsed += Time.unscaledDeltaTime;

            float rawProgress = Mathf.Clamp01(op.progress / 0.9f);
            bool canShowReady = op.progress >= 0.9f && elapsed >= minimumLoadingScreenTime;
            float targetVisualProgress = canShowReady ? 1f : rawProgress;

            displayedProgress = Mathf.MoveTowards(displayedProgress, targetVisualProgress, Time.unscaledDeltaTime * progressSmoothSpeed);
            ApplyProgress(displayedProgress);

            if (canShowReady)
            {
                if (!readyShown)
                {
                    readyShown = true;
                    readyShownTime = Time.unscaledTime;

                    if (readyRoot != null)
                        readyRoot.SetActive(true);

                    if (statusLabel != null)
                        statusLabel.text = readyStatusText;
                }

                if (Time.unscaledTime - readyShownTime >= readyInputBlockDuration && HasStartInput())
                {
                    if (statusLabel != null)
                        statusLabel.text = startingStatusText;

                    SceneLoadRequest.Clear();
                    op.allowSceneActivation = true;
                }
            }
            else
            {
                if (statusLabel != null)
                    statusLabel.text = loadingStatusText;
            }

            yield return null;
        }
    }

    private void ResolveRequest(out string sceneName, out int buildIndex, out LoadSceneMode mode, out float minimumLoadingScreenTime, out string prompt)
    {
        if (SceneLoadRequest.HasPendingRequest)
        {
            sceneName = SceneLoadRequest.TargetSceneName;
            buildIndex = SceneLoadRequest.TargetSceneBuildIndex;
            mode = SceneLoadRequest.TargetLoadSceneMode;
            minimumLoadingScreenTime = SceneLoadRequest.MinimumLoadingScreenTime;
            prompt = SceneLoadRequest.ReadyPrompt;
            return;
        }

        sceneName = fallbackTargetSceneName;
        buildIndex = fallbackTargetSceneBuildIndex;
        mode = fallbackLoadSceneMode;
        minimumLoadingScreenTime = 0f;
        prompt = "Click Anywhere To Start";
    }

    private bool HasValidTarget(string sceneName, int buildIndex)
    {
        if (buildIndex >= 0)
            return buildIndex < SceneManager.sceneCountInBuildSettings;

        if (!string.IsNullOrWhiteSpace(sceneName))
            return Application.CanStreamedLevelBeLoaded(sceneName);

        return false;
    }

    private AsyncOperation CreateLoadOperation(string sceneName, int buildIndex, LoadSceneMode mode)
    {
        if (buildIndex >= 0)
            return SceneManager.LoadSceneAsync(buildIndex, mode);

        if (!string.IsNullOrWhiteSpace(sceneName))
            return SceneManager.LoadSceneAsync(sceneName, mode);

        return null;
    }

    private void ApplyProgress(float value)
    {
        float v = Mathf.Clamp01(value);

        if (progressSlider != null)
            progressSlider.value = v;

        if (progressFill != null)
            progressFill.fillAmount = v;

        if (progressLabel != null)
            progressLabel.text = showPercentText ? $"{Mathf.RoundToInt(v * 100f)}%" : "";
    }

    private bool HasStartInput()
    {
        if (allowAnyKeyDown && Input.anyKeyDown)
            return true;

        if (allowTouch)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (Input.GetTouch(i).phase == TouchPhase.Began)
                    return true;
            }
        }

        return false;
    }
}