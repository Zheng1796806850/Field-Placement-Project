using TMPro;
using UnityEngine;

public class PlaytestObjectiveHUD : MonoBehaviour
{
    [Header("UI")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI titleLabel;
    public TextMeshProUGUI progressLabel;

    [Header("Runtime")]
    public bool isVisible = true;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        ApplyVisible();
    }

    public void SetTitle(string text)
    {
        if (titleLabel != null) titleLabel.text = text ?? "";
    }

    public void SetProgress(string text)
    {
        if (progressLabel != null) progressLabel.text = text ?? "";
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        ApplyVisible();
    }

    private void ApplyVisible()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        else
        {
            gameObject.SetActive(isVisible);
        }
    }
}