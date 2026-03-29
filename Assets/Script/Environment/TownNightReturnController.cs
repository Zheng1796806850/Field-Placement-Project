using UnityEngine;
using UnityEngine.SceneManagement;

public class TownNightReturnController : MonoBehaviour
{
    [Header("Scene Identity")]
    [Tooltip("Current scene name treated as Town. Leave empty to use active scene name at runtime.")]
    public string townSceneName = "";

    [Header("Return Target (Base)")]
    public string returnSceneName = "";
    public int returnSceneBuildIndex = -1;
    public string returnEntryPointId = "";

    [Header("Load Presentation")]
    public bool useLoadingScene = true;
    public string loadingSceneName = "";
    public int loadingSceneBuildIndex = -1;
    public string loadingTitle = "Returning to Base";
    public string readyPrompt = "Click Anywhere To Continue";
    [Min(0f)] public float minimumLoadingScreenTime = 0.15f;

    [Header("Carryover")]
    public bool carryClock = true;
    public bool carryPlayerVitals = true;

    private GameStateManager _gsm;
    private bool _subscribed;
    private bool _returning;
    private string _resolvedTownSceneName = "";

    private void Awake()
    {
        _resolvedTownSceneName = string.IsNullOrWhiteSpace(townSceneName)
            ? SceneManager.GetActiveScene().name
            : townSceneName;
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();

        // If player loads into Town while already Night, return immediately.
        if (IsInTown() && _gsm != null && _gsm.CurrentPhase == DayNightPhase.Night)
            ForceReturnToBase();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;

        _gsm = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (_gsm == null) return;

        _gsm.OnNightStarted += HandleNightStarted;
        _subscribed = true;
    }

    private void TryUnsubscribe()
    {
        if (!_subscribed) return;

        if (_gsm != null)
            _gsm.OnNightStarted -= HandleNightStarted;

        _subscribed = false;
    }

    private void HandleNightStarted()
    {
        if (!IsInTown()) return;
        ForceReturnToBase();
    }

    private bool IsInTown()
    {
        string active = SceneManager.GetActiveScene().name;
        return string.Equals(active, _resolvedTownSceneName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void ForceReturnToBase()
    {
        if (_returning) return;
        if (!HasValidTargetScene()) return;

        _returning = true;
        SceneTransitionContext.Prepare(returnEntryPointId, carryClock, carryPlayerVitals);
        SceneTransitionContext.RequestPhaseStartEventOnRestore();

        if (useLoadingScene)
        {
            SceneLoadRequest.SetRequest(
                returnSceneName,
                returnSceneBuildIndex,
                LoadSceneMode.Single,
                loadingTitle,
                readyPrompt,
                minimumLoadingScreenTime
            );

            var op = CreateLoadingSceneOperation();
            if (op == null)
            {
                SceneLoadRequest.Clear();
                SceneTransitionContext.Clear();
                _returning = false;
            }

            return;
        }

        SceneLoadRequest.Clear();
        var direct = CreateTargetSceneOperation();
        if (direct == null)
        {
            SceneTransitionContext.Clear();
            _returning = false;
        }
    }

    private bool HasValidTargetScene()
    {
        if (returnSceneBuildIndex >= 0)
            return returnSceneBuildIndex < SceneManager.sceneCountInBuildSettings;

        if (!string.IsNullOrWhiteSpace(returnSceneName))
            return Application.CanStreamedLevelBeLoaded(returnSceneName);

        return false;
    }

    private AsyncOperation CreateLoadingSceneOperation()
    {
        if (loadingSceneBuildIndex >= 0)
            return SceneManager.LoadSceneAsync(loadingSceneBuildIndex, LoadSceneMode.Single);

        if (!string.IsNullOrWhiteSpace(loadingSceneName))
            return SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);

        return null;
    }

    private AsyncOperation CreateTargetSceneOperation()
    {
        if (returnSceneBuildIndex >= 0)
            return SceneManager.LoadSceneAsync(returnSceneBuildIndex, LoadSceneMode.Single);

        if (!string.IsNullOrWhiteSpace(returnSceneName))
            return SceneManager.LoadSceneAsync(returnSceneName, LoadSceneMode.Single);

        return null;
    }
}
