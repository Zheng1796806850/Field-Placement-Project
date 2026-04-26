using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class DayNightVisualSeparationController : MonoBehaviour
{
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RadiusClearId = Shader.PropertyToID("_RadiusClear");
    private static readonly int RadiusDarkId = Shader.PropertyToID("_RadiusDark");
    private static readonly int FalloffSharpnessId = Shader.PropertyToID("_FalloffSharpness");
    private static readonly int FalloffCurveId = Shader.PropertyToID("_FalloffCurve");
    private static readonly int DarknessId = Shader.PropertyToID("_Darkness");
    private static readonly int EnabledId = Shader.PropertyToID("_Enabled");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");

    [Header("Refs")]
    public GameStateManager gameState;
    public Camera targetCamera;

    [Header("Overlay (UI Tint)")]
    public CanvasGroup overlayGroup;
    public Image overlayImage;
    public Color nightTintColor = new Color(0f, 0f, 0f, 1f);
    [Range(0f, 1f)] public float nightTintAlpha = 0.45f;

    [Header("Night vision (UI circle)")]
    [Tooltip("When enabled at night, only a circular area around the player stays clear; outside uses darknessAlpha on the overlay.")]
    public bool enableNightVisionRestriction = true;
    [Tooltip("Optional. If unset, the first PlayerMovementController in the scene is used.")]
    public Transform playerTarget;
    [Tooltip("Inside this radius (viewport-normalized, aspect-corrected) the overlay is fully transparent — fully clear vision.")]
    [Min(0.0001f)] public float visibilityRadius = 0.1f;
    [Tooltip("Distance from the clear edge to where the vignette reaches full darkness. Larger = longer, softer candle-like falloff.")]
    [Min(0.0001f)] public float falloffToDarkDistance = 0.28f;
    [Tooltip("Higher = contrastier falloff inside the band (still smooth). Lower = gentler, more washed transition.")]
    [Min(0.05f)] public float falloffSharpness = 2.2f;
    [Tooltip("Higher = darkness ramps up more slowly right outside the clear circle (more candle-like). 1 = neutral.")]
    [Min(0.1f)] public float falloffCurve = 1.35f;
    [Tooltip("Viewport-space offset added to the circle center (x right, y up). Use small values (e.g. ±0.02) to align with art.")]
    public Vector2 circleViewportOffset = Vector2.zero;
    [Range(0f, 1f)] public float darknessAlpha = 0.95f;
    [Tooltip("Material using shader UI/NightVisionCircleMask. A runtime instance is created so shared assets are not modified.")]
    public Material nightVisionMaterial;

    [Header("Transition Feedback")]
    public bool useBlackFlashOnNightStart = true;
    [Min(0.01f)] public float flashToBlackDuration = 0.12f;
    [Min(0f)] public float flashHoldDuration = 0.03f;
    [Min(0.01f)] public float flashToTintDuration = 0.22f;

    public bool useFadeOnDayStart = true;
    [Min(0.01f)] public float dayFadeOutDuration = 0.22f;

    [Header("Camera Background (Optional)")]
    public bool changeCameraBackground = false;
    public Color dayCameraBackground = new Color(0.55f, 0.8f, 1f, 1f);
    public Color nightCameraBackground = new Color(0.06f, 0.08f, 0.12f, 1f);
    [Min(0.01f)] public float cameraBackgroundLerpDuration = 0.3f;

    [Header("Global Lighting (Optional)")]
    public bool affectGlobalLights = true;
    public Component[] globalLights;
    [Min(0f)] public float dayLightIntensity = 1f;
    [Min(0f)] public float nightLightIntensity = 0.65f;
    public Color dayLightColor = Color.white;
    public Color nightLightColor = new Color(0.78f, 0.84f, 1f, 1f);
    [Min(0.01f)] public float lightLerpDuration = 0.3f;

    [Header("Night Ambience Audio")]
    public AudioSource ambienceSource;
    public AudioClip nightAmbienceLoop;
    [Range(0f, 1f)] public float nightAmbienceVolume = 0.35f;
    [Min(0.01f)] public float ambienceFadeInDuration = 0.45f;
    [Min(0.01f)] public float ambienceFadeOutDuration = 0.3f;

    [Header("UI Banner (Optional)")]
    public WaveEventBannerHUD banner;
    public string nightBannerText = "Nightfall";
    public string dayBannerText = "Dawn";

    private Coroutine _transitionRoutine;
    private Coroutine _ambienceRoutine;

    private readonly List<LightBinding> _lightBindings = new List<LightBinding>(8);

    private Material _nightVisionMaterialInstance;
    private Transform _cachedPlayerTransform;
    private bool _loggedMissingNightVisionMaterial;
    private bool _loggedMissingPlayer;

    private struct LightBinding
    {
        public Component component;
        public Func<float> getIntensity;
        public Action<float> setIntensity;
        public Func<Color> getColor;
        public Action<Color> setColor;
    }

    private void Awake()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (targetCamera == null) targetCamera = Camera.main;

        EnsureOverlayRefs(forceHideOverlayAlpha: true);
        EnsureNightVisionMaterial();
        EnsureAmbienceSource();
        RebuildLightBindings();
    }

    private void OnDestroy()
    {
        if (_nightVisionMaterialInstance != null)
        {
            Destroy(_nightVisionMaterialInstance);
            _nightVisionMaterialInstance = null;
        }
    }

    private void LateUpdate()
    {
        UpdateNightVisionCircle();
    }

    private void OnEnable()
    {
        if (gameState == null) gameState = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (gameState != null)
        {
            gameState.OnNightStarted += HandleNightStarted;
            gameState.OnDayStarted += HandleDayStarted;
        }

        EnsureOverlayRefs();
        EnsureNightVisionMaterial();
        ApplyImmediate(gameState != null ? gameState.CurrentPhase : DayNightPhase.Day);
    }

    private void OnDisable()
    {
        if (gameState != null)
        {
            gameState.OnNightStarted -= HandleNightStarted;
            gameState.OnDayStarted -= HandleDayStarted;
        }

        StopAllRoutines();
    }

    private void StopAllRoutines()
    {
        if (_transitionRoutine != null) { StopCoroutine(_transitionRoutine); _transitionRoutine = null; }
        if (_ambienceRoutine != null) { StopCoroutine(_ambienceRoutine); _ambienceRoutine = null; }
    }

    /// <param name="forceHideOverlayAlpha">Only use from Awake: hide overlay before first ApplyImmediate. Must stay false during transitions or day fade will start from alpha 0.</param>
    private void EnsureOverlayRefs(bool forceHideOverlayAlpha = false)
    {
        if (overlayGroup == null || overlayImage == null)
        {
            var cg = GetComponentInChildren<CanvasGroup>(true);
            var img = GetComponentInChildren<Image>(true);

            if (overlayGroup == null) overlayGroup = cg;
            if (overlayImage == null) overlayImage = img;
        }

        if (overlayGroup != null)
        {
            if (forceHideOverlayAlpha)
                overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = false;
            overlayGroup.interactable = false;
        }

        if (overlayImage != null)
        {
            overlayImage.raycastTarget = false;
        }
    }

    private void EnsureNightVisionMaterial()
    {
        if (overlayImage == null)
            return;

        if (!enableNightVisionRestriction)
        {
            if (_nightVisionMaterialInstance != null)
            {
                Destroy(_nightVisionMaterialInstance);
                _nightVisionMaterialInstance = null;
            }

            overlayImage.material = null;
            return;
        }

        Material sourceMaterial = nightVisionMaterial;
        if (sourceMaterial == null && overlayImage.material != null)
        {
            Shader sh = overlayImage.material.shader;
            if (sh != null && sh.name == "UI/NightVisionCircleMask")
                sourceMaterial = overlayImage.material;
        }

        if (sourceMaterial == null)
        {
            if (!_loggedMissingNightVisionMaterial)
            {
                Debug.LogWarning($"{nameof(DayNightVisualSeparationController)}: enableNightVisionRestriction is true but nightVisionMaterial is not assigned (and NightOverlay Image has no UI/NightVisionCircleMask material).");
                _loggedMissingNightVisionMaterial = true;
            }

            if (_nightVisionMaterialInstance != null)
            {
                Destroy(_nightVisionMaterialInstance);
                _nightVisionMaterialInstance = null;
            }

            overlayImage.material = null;
            return;
        }

        if (_nightVisionMaterialInstance == null || _nightVisionMaterialInstance.shader != sourceMaterial.shader)
        {
            if (_nightVisionMaterialInstance != null)
                Destroy(_nightVisionMaterialInstance);

            _nightVisionMaterialInstance = Instantiate(sourceMaterial);
            _nightVisionMaterialInstance.name = $"{sourceMaterial.name} (Instance)";
            overlayImage.material = _nightVisionMaterialInstance;
        }
    }

    private Transform TryResolvePlayerTransform()
    {
        if (playerTarget != null)
            return playerTarget;

        if (_cachedPlayerTransform != null)
            return _cachedPlayerTransform;

        var pm = FindFirstObjectByType<PlayerMovementController>();
        if (pm != null)
        {
            _cachedPlayerTransform = pm.transform;
            return _cachedPlayerTransform;
        }

        if (!_loggedMissingPlayer)
        {
            Debug.LogWarning($"{nameof(DayNightVisualSeparationController)}: No playerTarget and no {nameof(PlayerMovementController)} found in scene.");
            _loggedMissingPlayer = true;
        }

        return null;
    }

    private void UpdateNightVisionCircle()
    {
        EnsureNightVisionMaterial();

        if (_nightVisionMaterialInstance == null || !enableNightVisionRestriction)
            return;

        DayNightPhase phase = gameState != null ? gameState.CurrentPhase : DayNightPhase.Day;
        bool nightPhase = phase == DayNightPhase.Night;
        // CurrentPhase flips to Day before OnDayStarted runs, but overlay alpha may still be lerping down.
        // Keep shader mask enabled while overlay has strength so CanvasGroup.alpha can smoothly fade the night look.
        float overlayStrength = overlayGroup != null ? overlayGroup.alpha : 0f;
        bool shaderNightMaskEnabled = nightPhase || overlayStrength > 0.001f;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null)
            _nightVisionMaterialInstance.SetFloat(AspectId, Mathf.Max(0.0001f, cam.aspect));

        float rClear = Mathf.Max(0.0001f, visibilityRadius);
        float rDark = Mathf.Max(rClear + 0.0001f, rClear + Mathf.Max(0.0001f, falloffToDarkDistance));
        _nightVisionMaterialInstance.SetFloat(RadiusClearId, rClear);
        _nightVisionMaterialInstance.SetFloat(RadiusDarkId, rDark);
        _nightVisionMaterialInstance.SetFloat(FalloffSharpnessId, falloffSharpness);
        _nightVisionMaterialInstance.SetFloat(FalloffCurveId, falloffCurve);
        _nightVisionMaterialInstance.SetFloat(DarknessId, darknessAlpha);
        _nightVisionMaterialInstance.SetFloat(EnabledId, shaderNightMaskEnabled ? 1f : 0f);

        if (!shaderNightMaskEnabled || cam == null)
            return;

        Transform playerTr = playerTarget != null ? playerTarget : TryResolvePlayerTransform();
        if (playerTr == null)
            return;

        Vector3 vp = cam.WorldToViewportPoint(playerTr.position);
        if (vp.z <= 0f)
            return;

        float cx = vp.x + circleViewportOffset.x;
        float cy = vp.y + circleViewportOffset.y;
        _nightVisionMaterialInstance.SetVector(CenterId, new Vector4(cx, cy, 0f, 0f));
    }

    private void EnsureAmbienceSource()
    {
        if (ambienceSource == null)
        {
            ambienceSource = GetComponent<AudioSource>();
            if (ambienceSource == null) ambienceSource = gameObject.AddComponent<AudioSource>();

            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.volume = 0f;
        }

        ApplyAmbienceMixerRouting();
    }

    private void ApplyAmbienceMixerRouting()
    {
        GameAudioSettings.ApplyMusicRoute(ambienceSource);
    }

    private void Start()
    {
        ApplyAmbienceMixerRouting();
    }

    private void RebuildLightBindings()
    {
        _lightBindings.Clear();

        if (!affectGlobalLights) return;
        if (globalLights == null || globalLights.Length == 0) return;

        for (int i = 0; i < globalLights.Length; i++)
        {
            var c = globalLights[i];
            if (c == null) continue;

            if (c is Light l)
            {
                _lightBindings.Add(new LightBinding
                {
                    component = c,
                    getIntensity = () => l.intensity,
                    setIntensity = (v) => l.intensity = v,
                    getColor = () => l.color,
                    setColor = (col) => l.color = col
                });
                continue;
            }

            Type t = c.GetType();
            var pIntensity = t.GetProperty("intensity", BindingFlags.Instance | BindingFlags.Public);
            var pColor = t.GetProperty("color", BindingFlags.Instance | BindingFlags.Public);

            Func<float> gi = null;
            Action<float> si = null;
            Func<Color> gc = null;
            Action<Color> sc = null;

            if (pIntensity != null && pIntensity.PropertyType == typeof(float))
            {
                gi = () => (float)pIntensity.GetValue(c);
                si = (v) => pIntensity.SetValue(c, v);
            }

            if (pColor != null && pColor.PropertyType == typeof(Color))
            {
                gc = () => (Color)pColor.GetValue(c);
                sc = (col) => pColor.SetValue(c, col);
            }

            if (gi != null && si != null)
            {
                _lightBindings.Add(new LightBinding
                {
                    component = c,
                    getIntensity = gi,
                    setIntensity = si,
                    getColor = gc,
                    setColor = sc
                });
            }
        }
    }

    private void HandleNightStarted()
    {
        if (banner != null && !string.IsNullOrWhiteSpace(nightBannerText))
            banner.Show(nightBannerText);

        StartTransition(DayNightPhase.Night);
    }

    private void HandleDayStarted()
    {
        if (banner != null && !string.IsNullOrWhiteSpace(dayBannerText))
            banner.Show(dayBannerText);

        StartTransition(DayNightPhase.Day);
    }

    private void StartTransition(DayNightPhase targetPhase)
    {
        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(RunVisualTransition(targetPhase));

        if (_ambienceRoutine != null) StopCoroutine(_ambienceRoutine);
        _ambienceRoutine = StartCoroutine(RunAmbienceTransition(targetPhase));
    }

    private IEnumerator RunVisualTransition(DayNightPhase targetPhase)
    {
        EnsureOverlayRefs();
        EnsureNightVisionMaterial();

        if (overlayImage != null)
            overlayImage.color = nightTintColor;

        float targetAlpha = (targetPhase == DayNightPhase.Night) ? Mathf.Clamp01(nightTintAlpha) : 0f;

        if (targetCamera == null) targetCamera = Camera.main;

        Color camFrom = targetCamera != null ? targetCamera.backgroundColor : Color.black;
        Color camTo = camFrom;

        if (changeCameraBackground && targetCamera != null)
            camTo = (targetPhase == DayNightPhase.Night) ? nightCameraBackground : dayCameraBackground;

        float camDur = Mathf.Max(0.01f, cameraBackgroundLerpDuration);

        float lightFromIntensity = dayLightIntensity;
        float lightToIntensity = (targetPhase == DayNightPhase.Night) ? nightLightIntensity : dayLightIntensity;

        Color lightFromColor = dayLightColor;
        Color lightToColor = (targetPhase == DayNightPhase.Night) ? nightLightColor : dayLightColor;

        float lightDur = Mathf.Max(0.01f, lightLerpDuration);

        if (targetPhase == DayNightPhase.Night && useBlackFlashOnNightStart && overlayGroup != null)
        {
            yield return FadeOverlayAlpha(overlayGroup.alpha, 1f, flashToBlackDuration);
            if (flashHoldDuration > 0f)
                yield return WaitRealtime(flashHoldDuration);
            yield return FadeOverlayAlpha(1f, targetAlpha, flashToTintDuration);
        }
        else if (targetPhase == DayNightPhase.Day && useFadeOnDayStart && overlayGroup != null)
        {
            yield return FadeOverlayAlpha(overlayGroup.alpha, 0f, dayFadeOutDuration);
        }
        else
        {
            if (overlayGroup != null) overlayGroup.alpha = targetAlpha;
        }

        float t = 0f;
        float dur = Mathf.Max(camDur, lightDur);

        float[] startInt = null;
        Color[] startCol = null;

        if (_lightBindings.Count > 0)
        {
            startInt = new float[_lightBindings.Count];
            startCol = new Color[_lightBindings.Count];

            for (int i = 0; i < _lightBindings.Count; i++)
            {
                var b = _lightBindings[i];
                startInt[i] = b.getIntensity != null ? b.getIntensity() : dayLightIntensity;
                startCol[i] = b.getColor != null ? b.getColor() : dayLightColor;
            }
        }

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);

            if (changeCameraBackground && targetCamera != null)
            {
                float kc = camDur <= 0.0001f ? 1f : Mathf.Clamp01(t / camDur);
                targetCamera.backgroundColor = Color.Lerp(camFrom, camTo, kc);
            }

            if (affectGlobalLights && _lightBindings.Count > 0)
            {
                float kl = lightDur <= 0.0001f ? 1f : Mathf.Clamp01(t / lightDur);

                for (int i = 0; i < _lightBindings.Count; i++)
                {
                    var b = _lightBindings[i];

                    if (b.setIntensity != null)
                        b.setIntensity(Mathf.Lerp(startInt[i], lightToIntensity, kl));

                    if (b.setColor != null)
                        b.setColor(Color.Lerp(startCol[i], lightToColor, kl));
                }
            }

            yield return null;
        }

        if (changeCameraBackground && targetCamera != null)
            targetCamera.backgroundColor = camTo;

        if (affectGlobalLights && _lightBindings.Count > 0)
        {
            for (int i = 0; i < _lightBindings.Count; i++)
            {
                var b = _lightBindings[i];
                b.setIntensity?.Invoke(lightToIntensity);
                b.setColor?.Invoke(lightToColor);
            }
        }

        _transitionRoutine = null;
    }

    private IEnumerator FadeOverlayAlpha(float from, float to, float duration)
    {
        if (overlayGroup == null)
            yield break;

        float d = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            overlayGroup.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }

        overlayGroup.alpha = to;
    }

    private IEnumerator RunAmbienceTransition(DayNightPhase targetPhase)
    {
        EnsureAmbienceSource();

        if (ambienceSource == null)
        {
            _ambienceRoutine = null;
            yield break;
        }

        if (targetPhase == DayNightPhase.Night)
        {
            if (nightAmbienceLoop == null)
            {
                _ambienceRoutine = null;
                yield break;
            }

            if (ambienceSource.clip != nightAmbienceLoop)
                ambienceSource.clip = nightAmbienceLoop;

            if (!ambienceSource.isPlaying)
                ambienceSource.Play();

            yield return FadeAudio(ambienceSource, ambienceSource.volume, Mathf.Clamp01(nightAmbienceVolume), ambienceFadeInDuration);
        }
        else
        {
            yield return FadeAudio(ambienceSource, ambienceSource.volume, 0f, ambienceFadeOutDuration);

            if (ambienceSource.isPlaying)
                ambienceSource.Stop();

            ambienceSource.clip = null;
        }

        _ambienceRoutine = null;
    }

    private IEnumerator FadeAudio(AudioSource src, float from, float to, float duration)
    {
        if (src == null) yield break;

        float d = Mathf.Max(0.01f, duration);
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            src.volume = Mathf.Lerp(from, to, k);
            yield return null;
        }

        src.volume = to;
    }

    private IEnumerator WaitRealtime(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void ApplyImmediate(DayNightPhase phase)
    {
        EnsureOverlayRefs();
        EnsureNightVisionMaterial();
        EnsureAmbienceSource();
        RebuildLightBindings();

        float a = (phase == DayNightPhase.Night) ? Mathf.Clamp01(nightTintAlpha) : 0f;

        if (overlayImage != null)
            overlayImage.color = nightTintColor;

        if (overlayGroup != null)
            overlayGroup.alpha = a;

        if (changeCameraBackground)
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
                targetCamera.backgroundColor = (phase == DayNightPhase.Night) ? nightCameraBackground : dayCameraBackground;
        }

        if (affectGlobalLights && _lightBindings.Count > 0)
        {
            float li = (phase == DayNightPhase.Night) ? nightLightIntensity : dayLightIntensity;
            Color lc = (phase == DayNightPhase.Night) ? nightLightColor : dayLightColor;

            for (int i = 0; i < _lightBindings.Count; i++)
            {
                var b = _lightBindings[i];
                b.setIntensity?.Invoke(li);
                b.setColor?.Invoke(lc);
            }
        }

        if (ambienceSource != null)
        {
            if (phase == DayNightPhase.Night && nightAmbienceLoop != null)
            {
                ambienceSource.clip = nightAmbienceLoop;
                ambienceSource.loop = true;
                ambienceSource.spatialBlend = 0f;
                ambienceSource.volume = Mathf.Clamp01(nightAmbienceVolume);
                if (!ambienceSource.isPlaying) ambienceSource.Play();
            }
            else
            {
                ambienceSource.volume = 0f;
                if (ambienceSource.isPlaying) ambienceSource.Stop();
                ambienceSource.clip = null;
            }
        }

        UpdateNightVisionCircle();
    }
}
