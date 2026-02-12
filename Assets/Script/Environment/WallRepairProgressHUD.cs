using UnityEngine;
using UnityEngine.UI;

public class WallRepairProgressHUD : MonoBehaviour
{
    [Header("UI")]
    public Image fillImage;
    public CanvasGroup canvasGroup;

    [Header("Behavior")]
    public bool showOnlyWhileRepairing = true;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        SetVisible(false);
        SetProgress(0f);
    }

    public void SetProgress(float t01)
    {
        t01 = Mathf.Clamp01(t01);
        if (fillImage != null) fillImage.fillAmount = t01;
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
