using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds three sliders to GameAudioSettings for settings panels (main menu and/or pause menu).
/// When placed under <see cref="SettingsShellController"/>, uses draft mode: live mixer preview without PlayerPrefs until Apply.
/// </summary>
[DisallowMultipleComponent]
public class SettingsAudioPanelBinder : MonoBehaviour
{
    [Header("Sliders (0 = min, 1 = max)")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    private SettingsShellController _shell;
    private bool _wired;

    private void Awake()
    {
        _shell = GetComponentInParent<SettingsShellController>(true);
    }

    private void Start()
    {
        if (_shell != null)
            WireShellManaged();
        else
            WireStandalone();
    }

    private void OnEnable()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SessionReverted += OnSessionReverted;
            SettingsManager.Instance.SessionApplied += OnSessionApplied;
        }

        if (_shell != null && GameAudioSettings.Instance != null)
            PushSliderValuesFromGameAudio();
    }

    private void OnDisable()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SessionReverted -= OnSessionReverted;
            SettingsManager.Instance.SessionApplied -= OnSessionApplied;
        }
    }

    private void OnDestroy()
    {
        RemoveListeners();
    }

    private void OnSessionReverted()
    {
        PushSliderValuesFromGameAudio();
    }

    private void OnSessionApplied()
    {
        PushSliderValuesFromGameAudio();
    }

    private void WireShellManaged()
    {
        var gas = GameAudioSettings.Instance;
        if (gas == null)
        {
            Debug.LogWarning("[SettingsAudioPanelBinder] GameAudioSettings.Instance is null.");
            return;
        }

        RemoveListeners();
        _wired = true;

        ConfigureSlider(masterSlider, OnMasterDraft);
        ConfigureSlider(musicSlider, OnMusicDraft);
        ConfigureSlider(sfxSlider, OnSfxDraft);

        PushSliderValuesFromGameAudio();
    }

    private void WireStandalone()
    {
        var gas = GameAudioSettings.Instance;
        if (gas == null)
        {
            Debug.LogWarning("[SettingsAudioPanelBinder] GameAudioSettings.Instance is null. Add GameAudioSettings to the first loaded scene (e.g. with SfxPlayer).");
            return;
        }

        gas.Load();
        RemoveListeners();
        _wired = true;

        ConfigureSlider(masterSlider, OnMasterPersist);
        ConfigureSlider(musicSlider, OnMusicPersist);
        ConfigureSlider(sfxSlider, OnSfxPersist);

        PushSliderValuesFromGameAudio();
    }

    private static void ConfigureSlider(Slider s, UnityEngine.Events.UnityAction<float> handler)
    {
        if (s == null) return;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.wholeNumbers = false;
        s.onValueChanged.AddListener(handler);
    }

    private void RemoveListeners()
    {
        if (masterSlider != null)
        {
            masterSlider.onValueChanged.RemoveListener(OnMasterDraft);
            masterSlider.onValueChanged.RemoveListener(OnMasterPersist);
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(OnMusicDraft);
            musicSlider.onValueChanged.RemoveListener(OnMusicPersist);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSfxDraft);
            sfxSlider.onValueChanged.RemoveListener(OnSfxPersist);
        }

        _wired = false;
    }

    private void PushSliderValuesFromGameAudio()
    {
        var gas = GameAudioSettings.Instance;
        if (gas == null) return;

        if (masterSlider != null) masterSlider.SetValueWithoutNotify(gas.MasterLinear);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(gas.MusicLinear);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(gas.SfxLinear);
    }

    private void OnMasterDraft(float v)
    {
        GameAudioSettings.Instance?.SetMasterLinear(v, persist: false);
        SettingsManager.Instance?.NotifyAudioDraftChanged();
    }

    private void OnMusicDraft(float v)
    {
        GameAudioSettings.Instance?.SetMusicLinear(v, persist: false);
        SettingsManager.Instance?.NotifyAudioDraftChanged();
    }

    private void OnSfxDraft(float v)
    {
        GameAudioSettings.Instance?.SetSfxLinear(v, persist: false);
        SettingsManager.Instance?.NotifyAudioDraftChanged();
    }

    private void OnMasterPersist(float v) => GameAudioSettings.Instance?.SetMasterLinear(v);
    private void OnMusicPersist(float v) => GameAudioSettings.Instance?.SetMusicLinear(v);
    private void OnSfxPersist(float v) => GameAudioSettings.Instance?.SetSfxLinear(v);
}
