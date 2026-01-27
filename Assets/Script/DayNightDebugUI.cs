using TMPro;
using UnityEngine;

public class DayNightDebugUI : MonoBehaviour
{
    public TextMeshProUGUI label;

    private void Reset()
    {
        label = GetComponent<TextMeshProUGUI>();
    }

    private void Update()
    {
        var gsm = GameStateManager.Instance;
        if (gsm == null || label == null) return;

        label.text = $"Phase: {gsm.CurrentPhase}  |  Remaining: {gsm.PhaseTimeRemaining:F1}s";
    }
}
