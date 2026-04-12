using System;
using UnityEngine;

/// <summary>
/// Single entry for settings persistence and edit-session lifecycle (MENU-03c).
/// Audio values remain owned by <see cref="GameAudioSettings"/>; this coordinates Load/Apply/Revert for the settings UI.
/// </summary>
[DefaultExecutionOrder(-95)]
[DisallowMultipleComponent]
public class SettingsManager : MonoBehaviour
{
    public const string PrefKeyControlsPlaceholder = "FGCP_Settings_ControlsPlaceholder";
    public const string PrefKeyGameplayPlaceholder = "FGCP_Settings_GameplayPlaceholder";

    public static SettingsManager Instance { get; private set; }

    [Header("Optional defaults for placeholders (first run)")]
    [SerializeField] private string defaultControlsPlaceholder = "";
    [SerializeField] private int defaultGameplayPlaceholder = 0;

    public bool SessionActive { get; private set; }
    public bool SessionDirty { get; private set; }

    private float _sessionSnapMaster = 1f;
    private float _sessionSnapMusic = 1f;
    private float _sessionSnapSfx = 1f;

    private string _controlsDraft;
    private int _gameplayDraft;

    public event Action SessionApplied;
    public event Action SessionReverted;
    public event Action SessionBegan;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Call when the settings shell is shown. Snapshots current audio for Revert; reloads placeholder prefs into drafts.</summary>
    public void BeginSettingsSession()
    {
        var gas = GameAudioSettings.Instance;
        if (gas != null)
        {
            _sessionSnapMaster = gas.MasterLinear;
            _sessionSnapMusic = gas.MusicLinear;
            _sessionSnapSfx = gas.SfxLinear;
        }

        LoadControlsAndGameplayDrafts();
        SessionActive = true;
        SessionDirty = false;
        SessionBegan?.Invoke();
    }

    private void LoadControlsAndGameplayDrafts()
    {
        _controlsDraft = PlayerPrefs.GetString(PrefKeyControlsPlaceholder, defaultControlsPlaceholder);
        _gameplayDraft = PlayerPrefs.GetInt(PrefKeyGameplayPlaceholder, defaultGameplayPlaceholder);
    }

    /// <summary>Audio sliders changed during an open session (live mixer preview, no disk yet).</summary>
    public void NotifyAudioDraftChanged()
    {
        if (!SessionActive) return;
        SessionDirty = true;
    }

    /// <summary>Apply: persist audio + placeholder categories to PlayerPrefs.</summary>
    public void ApplyAndSaveSettings()
    {
        var gas = GameAudioSettings.Instance;
        if (gas != null)
            gas.Save();

        PlayerPrefs.SetString(PrefKeyControlsPlaceholder, _controlsDraft ?? string.Empty);
        PlayerPrefs.SetInt(PrefKeyGameplayPlaceholder, _gameplayDraft);
        PlayerPrefs.Save();

        if (gas != null)
        {
            _sessionSnapMaster = gas.MasterLinear;
            _sessionSnapMusic = gas.MusicLinear;
            _sessionSnapSfx = gas.SfxLinear;
        }

        SessionDirty = false;
        SessionApplied?.Invoke();
    }

    /// <summary>Revert: restore audio snapshot (discard un-applied slider changes). Placeholder drafts reload from last loaded prefs.</summary>
    public void RevertSettingsSession()
    {
        var gas = GameAudioSettings.Instance;
        if (gas != null)
            gas.RestoreLinearSnapshot(_sessionSnapMaster, _sessionSnapMusic, _sessionSnapSfx);

        LoadControlsAndGameplayDrafts();
        SessionDirty = false;
        SessionReverted?.Invoke();
    }

    /// <summary>When the shell is hidden without explicit Apply (Back, ESC, close): revert if there are un-applied changes.</summary>
    public void EndSettingsSessionIfAborted()
    {
        if (!SessionActive)
            return;

        if (SessionDirty)
            RevertSettingsSession();

        SessionActive = false;
    }

    /// <summary>Call after Apply or clean Back to mark the shell closed.</summary>
    public void CloseSettingsSessionCommitted()
    {
        SessionActive = false;
        SessionDirty = false;
    }

    // --- Placeholder API for future Controls / Gameplay UI ---

    public string GetControlsPlaceholderDraft() => _controlsDraft ?? string.Empty;

    public void SetControlsPlaceholderDraft(string value)
    {
        _controlsDraft = value ?? string.Empty;
        if (SessionActive)
            SessionDirty = true;
    }

    public int GetGameplayPlaceholderDraft() => _gameplayDraft;

    public void SetGameplayPlaceholderDraft(int value)
    {
        _gameplayDraft = value;
        if (SessionActive)
            SessionDirty = true;
    }

    /// <summary>Ensures a manager exists (e.g. if shell opens before any scene had the component).</summary>
    public static SettingsManager EnsureInHierarchy()
    {
        if (Instance != null)
            return Instance;

        var existing = FindFirstObjectByType<SettingsManager>();
        if (existing != null)
            return existing;

        var go = new GameObject("SettingsManager");
        return go.AddComponent<SettingsManager>();
    }
}
