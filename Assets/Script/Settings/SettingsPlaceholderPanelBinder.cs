using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional: binds placeholder Controls (string) and Gameplay (int) UI to <see cref="SettingsManager"/> drafts.
/// Values persist only after Apply on the parent <see cref="SettingsShellController"/>.
/// </summary>
[DisallowMultipleComponent]
public class SettingsPlaceholderPanelBinder : MonoBehaviour
{
    [SerializeField] private TMP_InputField controlsInput;
    [SerializeField] private Slider gameplayPlaceholderSlider;

    private void OnEnable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SessionReverted += OnSessionReverted;

        PushFromManager();
        WireListeners();
    }

    private void OnDisable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SessionReverted -= OnSessionReverted;

        UnwireListeners();
    }

    private void OnSessionReverted() => PushFromManager();

    private void PushFromManager()
    {
        var mgr = SettingsManager.Instance;
        if (mgr == null) return;

        if (controlsInput != null)
            controlsInput.SetTextWithoutNotify(mgr.GetControlsPlaceholderDraft());

        if (gameplayPlaceholderSlider != null)
        {
            gameplayPlaceholderSlider.wholeNumbers = true;
            gameplayPlaceholderSlider.minValue = 0f;
            gameplayPlaceholderSlider.maxValue = 3f;
            gameplayPlaceholderSlider.SetValueWithoutNotify(mgr.GetGameplayPlaceholderDraft());
        }
    }

    private void WireListeners()
    {
        if (controlsInput != null)
        {
            controlsInput.onEndEdit.RemoveListener(OnControlsEndEdit);
            controlsInput.onEndEdit.AddListener(OnControlsEndEdit);
        }

        if (gameplayPlaceholderSlider != null)
        {
            gameplayPlaceholderSlider.onValueChanged.RemoveListener(OnGameplaySlider);
            gameplayPlaceholderSlider.onValueChanged.AddListener(OnGameplaySlider);
        }
    }

    private void UnwireListeners()
    {
        if (controlsInput != null)
            controlsInput.onEndEdit.RemoveListener(OnControlsEndEdit);
        if (gameplayPlaceholderSlider != null)
            gameplayPlaceholderSlider.onValueChanged.RemoveListener(OnGameplaySlider);
    }

    private void OnControlsEndEdit(string text)
    {
        SettingsManager.Instance?.SetControlsPlaceholderDraft(text ?? string.Empty);
    }

    private void OnGameplaySlider(float v)
    {
        SettingsManager.Instance?.SetGameplayPlaceholderDraft(Mathf.RoundToInt(v));
    }
}
