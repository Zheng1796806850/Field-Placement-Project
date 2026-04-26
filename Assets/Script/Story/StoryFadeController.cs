using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>全屏黑场渐变，使用 <see cref="Time.unscaledDeltaTime"/>。</summary>
[DisallowMultipleComponent]
public class StoryFadeController : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image blackoutImage;
    [SerializeField] bool startBlackOnEnable = true;

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (blackoutImage == null && canvasGroup != null)
            blackoutImage = canvasGroup.GetComponentInChildren<Image>(true);

        ApplyInitialVisualState();
    }

    void OnEnable()
    {
        ApplyInitialVisualState();
    }

    void ApplyInitialVisualState()
    {
        if (blackoutImage != null)
        {
            var c = blackoutImage.color;
            blackoutImage.color = new Color(c.r, c.g, c.b, 1f);
            blackoutImage.raycastTarget = false;
        }

        if (canvasGroup == null)
            return;

        canvasGroup.interactable = false;
        if (startBlackOnEnable)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void SnapToBlack()
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }

    public void SnapToClear()
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    public IEnumerator FadeFromBlack(float durationSeconds)
    {
        if (canvasGroup == null)
            yield break;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;
        float dur = Mathf.Max(0.01f, durationSeconds);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / dur);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    public IEnumerator FadeToBlack(float durationSeconds)
    {
        if (canvasGroup == null)
            yield break;

        canvasGroup.blocksRaycasts = true;
        float dur = Mathf.Max(0.01f, durationSeconds);
        float t = 0f;
        float a0 = canvasGroup.alpha;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(a0, 1f, Mathf.Clamp01(t / dur));
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }
}
