using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimedActionHUD : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public Image fillImage;
    public TextMeshProUGUI actionLabel;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        SetVisible(false);
        SetProgress(0f);
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void SetProgress(float t01)
    {
        t01 = Mathf.Clamp01(t01);
        if (fillImage != null) fillImage.fillAmount = t01;
    }

    public void SetLabel(string text)
    {
        if (actionLabel != null) actionLabel.text = text ?? "";
    }
}
