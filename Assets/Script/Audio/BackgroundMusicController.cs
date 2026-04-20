using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Global background music: Main Menu / Day / Night. Uses two AudioSources for crossfading,
/// routes through <see cref="GameAudioSettings"/> Music mixer group, survives scene loads via DontDestroyOnLoad.
/// </summary>
[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public class BackgroundMusicController : MonoBehaviour
{
    public static BackgroundMusicController Instance { get; private set; }

    [Header("Scene Names")]
    [Tooltip("Scene treated as main menu (case-insensitive).")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Tooltip("Loading scene: do not change BGM here; keep whatever is playing.")]
    [SerializeField] private string loadingSceneName = "Loading";

    [Header("Clips")]
    [SerializeField] private AudioClip menuClip;
    [SerializeField] private AudioClip dayClip;
    [SerializeField] private AudioClip nightClip;

    [Header("Crossfade")]
    [Min(0.01f)] [SerializeField] private float fadeDuration = 1.25f;

    [Tooltip("Per-source linear volume when fully audible (mixer still owns Music group level).")]
    [Range(0f, 1f)] [SerializeField] private float maxSourceVolume = 1f;

    private AudioSource[] _sources;
    private int _activeIndex;
    private Coroutine _fadeRoutine;
    private bool _isCrossfading;
    private GameStateManager _gsm;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureSources();
        TryApplyAudioRoutes();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        TryApplyAudioRoutes();
        BindGameStateManager();
        SyncFromCurrentPhase();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_gsm != null)
        {
            _gsm.OnDayStarted -= HandleDayStarted;
            _gsm.OnNightStarted -= HandleNightStarted;
            _gsm = null;
        }

        if (Instance == this)
            Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string name = scene.name;

        if (IsLoadingScene(name))
        {
            UnbindGameStateManager();
            return;
        }

        TryApplyAudioRoutes();
        BindGameStateManager();
        SyncFromCurrentPhase();
    }

    private void EnsureSources()
    {
        if (_sources != null && _sources.Length == 2 && _sources[0] != null && _sources[1] != null)
            return;

        _sources = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject(i == 0 ? "BGM_A" : "BGM_B");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.priority = 128;
            src.volume = 0f;
            _sources[i] = src;
        }

        _activeIndex = 0;
    }

    /// <summary>Re-apply Music mixer routing (safe if <see cref="GameAudioSettings"/> is not ready yet).</summary>
    public void TryApplyAudioRoutes()
    {
        EnsureSources();
        if (_sources == null)
            return;

        for (int i = 0; i < _sources.Length; i++)
        {
            if (_sources[i] != null)
                GameAudioSettings.ApplyMusicRoute(_sources[i]);
        }
    }

    private void BindGameStateManager()
    {
        var found = FindFirstObjectByType<GameStateManager>(FindObjectsInactive.Include);
        if (found == _gsm)
        {
            SyncFromCurrentPhase();
            return;
        }

        UnbindGameStateManager();
        _gsm = found;

        if (_gsm != null)
        {
            _gsm.OnDayStarted += HandleDayStarted;
            _gsm.OnNightStarted += HandleNightStarted;
        }
    }

    private void UnbindGameStateManager()
    {
        if (_gsm != null)
        {
            _gsm.OnDayStarted -= HandleDayStarted;
            _gsm.OnNightStarted -= HandleNightStarted;
            _gsm = null;
        }
    }

    private void HandleDayStarted()
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
            return;

        RequestMusic(dayClip);
    }

    private void HandleNightStarted()
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
            return;

        RequestMusic(nightClip);
    }

    /// <summary>Updates BGM from current scene + <see cref="GameStateManager.CurrentPhase"/>.</summary>
    public void SyncFromCurrentPhase()
    {
        TryApplyAudioRoutes();

        string sceneName = SceneManager.GetActiveScene().name;

        if (IsLoadingScene(sceneName))
            return;

        if (IsMainMenuScene(sceneName))
        {
            RequestMusic(menuClip);
            return;
        }

        if (!IsGameplayScene(sceneName))
            return;

        if (_gsm == null)
            _gsm = FindFirstObjectByType<GameStateManager>(FindObjectsInactive.Include);

        if (_gsm == null)
            return;

        RequestMusic(_gsm.CurrentPhase == DayNightPhase.Day ? dayClip : nightClip);
    }

    private void RequestMusic(AudioClip clip)
    {
        EnsureSources();

        var active = _sources[_activeIndex];
        if (clip != null && active != null && active.isPlaying && active.clip == clip && !_isCrossfading)
            return;

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        if (clip == null)
        {
            _fadeRoutine = StartCoroutine(FadeOutAllRoutine());
            return;
        }

        _fadeRoutine = StartCoroutine(CrossfadeRoutine(clip));
    }

    private IEnumerator CrossfadeRoutine(AudioClip newClip)
    {
        _isCrossfading = true;
        float dur = Mathf.Max(0.01f, fadeDuration);
        int outIdx = _activeIndex;
        int inIdx = 1 - _activeIndex;
        var srcOut = _sources[outIdx];
        var srcIn = _sources[inIdx];

        float targetVol = Mathf.Clamp01(maxSourceVolume);

        srcIn.clip = newClip;
        srcIn.volume = 0f;
        srcIn.time = 0f;
        srcIn.loop = true;
        TryApplyAudioRoutes();
        srcIn.Play();

        float startOut = srcOut.isPlaying ? srcOut.volume : 0f;
        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            if (srcOut.isPlaying)
                srcOut.volume = Mathf.Lerp(startOut, 0f, k);
            if (srcIn.isPlaying)
                srcIn.volume = Mathf.Lerp(0f, targetVol, k);
            yield return null;
        }

        if (srcOut.isPlaying)
        {
            srcOut.Stop();
            srcOut.clip = null;
        }

        srcOut.volume = 0f;
        srcIn.volume = targetVol;
        _activeIndex = inIdx;

        _isCrossfading = false;
        _fadeRoutine = null;
    }

    private IEnumerator FadeOutAllRoutine()
    {
        _isCrossfading = true;
        float dur = Mathf.Max(0.01f, fadeDuration);
        float t = 0f;
        float[] start = { _sources[0].volume, _sources[1].volume };

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            for (int i = 0; i < 2; i++)
            {
                if (_sources[i].isPlaying)
                    _sources[i].volume = Mathf.Lerp(start[i], 0f, k);
            }
            yield return null;
        }

        for (int i = 0; i < 2; i++)
        {
            if (_sources[i].isPlaying)
                _sources[i].Stop();
            _sources[i].clip = null;
            _sources[i].volume = 0f;
        }

        _isCrossfading = false;
        _fadeRoutine = null;
    }

    private bool IsMainMenuScene(string sceneName)
    {
        return !string.IsNullOrEmpty(mainMenuSceneName)
               && string.Equals(sceneName, mainMenuSceneName, System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsLoadingScene(string sceneName)
    {
        return !string.IsNullOrEmpty(loadingSceneName)
               && string.Equals(sceneName, loadingSceneName, System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsGameplayScene(string sceneName)
    {
        return !IsMainMenuScene(sceneName) && !IsLoadingScene(sceneName);
    }
}
