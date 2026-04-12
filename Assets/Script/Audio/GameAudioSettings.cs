using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Master / Music / SFX volumes via AudioMixer exposed parameters and PlayerPrefs persistence.
/// Place on a DontDestroyOnLoad object (typically alongside SfxPlayer). Execution order -100 so SfxPlayer can rely on Instance.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class GameAudioSettings : MonoBehaviour
{
    public const string PrefKeyMaster = "FGCP_Audio_Master";
    public const string PrefKeyMusic = "FGCP_Audio_Music";
    public const string PrefKeySfx = "FGCP_Audio_Sfx";

    public const string MasterVolumeParam = "MasterVolume";
    public const string MusicVolumeParam = "MusicVolume";
    public const string SfxVolumeParam = "SfxVolume";

    public static GameAudioSettings Instance { get; private set; }

    [Header("Mixer")]
    [Tooltip("Assign MainMixer asset. Expose attenuation on Master, Music, and SFX groups as MasterVolume, MusicVolume, SfxVolume.")]
    public AudioMixer mainMixer;

    [Tooltip("Child Audio Mixer Group for SFX (routed from SfxPlayer pool).")]
    public AudioMixerGroup sfxMixerGroup;

    [Tooltip("Child Audio Mixer Group for music / ambience (e.g. DayNightVisualSeparationController).")]
    public AudioMixerGroup musicMixerGroup;

    [Range(0f, 1f)] [SerializeField] private float masterLinear = 1f;
    [Range(0f, 1f)] [SerializeField] private float musicLinear = 1f;
    [Range(0f, 1f)] [SerializeField] private float sfxLinear = 1f;

    public float MasterLinear => masterLinear;
    public float MusicLinear => musicLinear;
    public float SfxLinear => sfxLinear;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        ApplyAll();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Sends an <see cref="AudioSource"/> through the SFX mixer group so the SFX volume slider applies.
    /// Safe when Instance or group is missing (no-op).
    /// </summary>
    public static void ApplySfxRoute(AudioSource source)
    {
        if (source == null) return;
        var gas = Instance;
        if (gas != null && gas.sfxMixerGroup != null)
            source.outputAudioMixerGroup = gas.sfxMixerGroup;
    }

    /// <summary>
    /// Sends an <see cref="AudioSource"/> through the Music mixer group so the Music volume slider applies (BGM, ambience, etc.).
    /// </summary>
    public static void ApplyMusicRoute(AudioSource source)
    {
        if (source == null) return;
        var gas = Instance;
        if (gas != null && gas.musicMixerGroup != null)
            source.outputAudioMixerGroup = gas.musicMixerGroup;
    }

    /// <summary>
    /// Maps linear 0..1 to dB. 0 (and near-zero) maps to -80 dB for effective silence.
    /// </summary>
    public static float LinearToDecibels(float linear)
    {
        linear = Mathf.Clamp01(linear);
        if (linear <= 0.0001f)
            return -80f;
        return 20f * Mathf.Log10(linear);
    }

    public void SetMasterLinear(float value)
    {
        SetMasterLinear(value, persist: true);
    }

    public void SetMusicLinear(float value)
    {
        SetMusicLinear(value, persist: true);
    }

    public void SetSfxLinear(float value)
    {
        SetSfxLinear(value, persist: true);
    }

    /// <summary>Updates master level and mixer; optionally writes PlayerPrefs (used by settings shell draft mode).</summary>
    public void SetMasterLinear(float value, bool persist)
    {
        masterLinear = Mathf.Clamp01(value);
        ApplyGroupVolume(MasterVolumeParam, masterLinear);
        if (persist) Save();
    }

    public void SetMusicLinear(float value, bool persist)
    {
        musicLinear = Mathf.Clamp01(value);
        ApplyGroupVolume(MusicVolumeParam, musicLinear);
        if (persist) Save();
    }

    public void SetSfxLinear(float value, bool persist)
    {
        sfxLinear = Mathf.Clamp01(value);
        ApplyGroupVolume(SfxVolumeParam, sfxLinear);
        if (persist) Save();
    }

    /// <summary>Restores in-memory levels and mixer from a snapshot without touching PlayerPrefs.</summary>
    public void RestoreLinearSnapshot(float master, float music, float sfx)
    {
        masterLinear = Mathf.Clamp01(master);
        musicLinear = Mathf.Clamp01(music);
        sfxLinear = Mathf.Clamp01(sfx);
        ApplyAll();
    }

    private void ApplyGroupVolume(string parameterName, float linear)
    {
        if (mainMixer == null) return;
        mainMixer.SetFloat(parameterName, LinearToDecibels(linear));
    }

    /// <summary>
    /// Re-applies all three mixer levels from current in-memory values. Safe to call multiple times.
    /// </summary>
    public void ApplyAll()
    {
        if (mainMixer == null) return;
        mainMixer.SetFloat(MasterVolumeParam, LinearToDecibels(masterLinear));
        mainMixer.SetFloat(MusicVolumeParam, LinearToDecibels(musicLinear));
        mainMixer.SetFloat(SfxVolumeParam, LinearToDecibels(sfxLinear));
    }

    /// <summary>
    /// Loads 0..1 values from PlayerPrefs and applies to mixer (no extra Save).
    /// </summary>
    public void Load()
    {
        masterLinear = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeyMaster, 1f));
        musicLinear = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeyMusic, 1f));
        sfxLinear = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeySfx, 1f));
        ApplyAll();
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(PrefKeyMaster, masterLinear);
        PlayerPrefs.SetFloat(PrefKeyMusic, musicLinear);
        PlayerPrefs.SetFloat(PrefKeySfx, sfxLinear);
        PlayerPrefs.Save();
    }
}
