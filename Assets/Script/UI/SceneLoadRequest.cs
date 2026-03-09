using UnityEngine.SceneManagement;

public static class SceneLoadRequest
{
    public static bool HasPendingRequest { get; private set; }
    public static string TargetSceneName { get; private set; } = "";
    public static int TargetSceneBuildIndex { get; private set; } = -1;
    public static LoadSceneMode TargetLoadSceneMode { get; private set; } = LoadSceneMode.Single;
    public static string LoadingTitle { get; private set; } = "Loading";
    public static string ReadyPrompt { get; private set; } = "Click Anywhere To Start";
    public static float MinimumLoadingScreenTime { get; private set; } = 0f;

    public static void SetRequest(string targetSceneName, int targetSceneBuildIndex, LoadSceneMode targetLoadSceneMode, string loadingTitle, string readyPrompt, float minimumLoadingScreenTime)
    {
        HasPendingRequest = true;
        TargetSceneName = targetSceneName ?? "";
        TargetSceneBuildIndex = targetSceneBuildIndex;
        TargetLoadSceneMode = targetLoadSceneMode;
        LoadingTitle = string.IsNullOrWhiteSpace(loadingTitle) ? "Loading" : loadingTitle;
        ReadyPrompt = string.IsNullOrWhiteSpace(readyPrompt) ? "Click Anywhere To Start" : readyPrompt;
        MinimumLoadingScreenTime = minimumLoadingScreenTime < 0f ? 0f : minimumLoadingScreenTime;
    }

    public static void Clear()
    {
        HasPendingRequest = false;
        TargetSceneName = "";
        TargetSceneBuildIndex = -1;
        TargetLoadSceneMode = LoadSceneMode.Single;
        LoadingTitle = "Loading";
        ReadyPrompt = "Click Anywhere To Start";
        MinimumLoadingScreenTime = 0f;
    }

    public static bool HasValidTarget()
    {
        if (TargetSceneBuildIndex >= 0)
            return TargetSceneBuildIndex < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;

        if (!string.IsNullOrWhiteSpace(TargetSceneName))
            return UnityEngine.Application.CanStreamedLevelBeLoaded(TargetSceneName);

        return false;
    }
}