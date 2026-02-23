using TMPro;
using UnityEngine;

public class DaytimeTimerHUD : MonoBehaviour
{
    public TextMeshProUGUI label;
    public bool hideDuringNight = true;
    public bool disableLabelComponentDuringHide = true;

    private void Reset()
    {
        label = GetComponent<TextMeshProUGUI>();
    }

    private void Awake()
    {
        if (label == null) label = GetComponent<TextMeshProUGUI>();
    }

    private void Update()
    {
        var gsm = GameStateManager.Instance;
        if (gsm == null || label == null) return;

        if (gsm.CurrentPhase != DayNightPhase.Day)
        {
            if (hideDuringNight)
            {
                SetVisible(false);
            }
            else
            {
                SetVisible(true);
                label.text = "Night";
            }
            return;
        }

        SetVisible(true);

        int t = Mathf.CeilToInt(Mathf.Max(0f, gsm.PhaseTimeRemaining));
        int m = t / 60;
        int s = t % 60;
        label.text = $"{m:00}:{s:00}";
    }

    private void SetVisible(bool visible)
    {
        if (disableLabelComponentDuringHide)
        {
            if (label.enabled != visible) label.enabled = visible;
        }
        else
        {
            var c = label.color;
            c.a = visible ? 1f : 0f;
            label.color = c;
        }
    }
}
