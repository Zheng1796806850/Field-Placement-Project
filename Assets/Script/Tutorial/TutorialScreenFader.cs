using System.Collections;
using UnityEngine;

public class TutorialScreenFader : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    [Min(0.01f)] public float defaultFadeDuration = 0.35f;

    private Coroutine _routine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    public void SetBlackImmediate(bool isBlack)
    {
        if (canvasGroup == null) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;

        canvasGroup.alpha = isBlack ? 1f : 0f;
        canvasGroup.blocksRaycasts = isBlack;
        canvasGroup.interactable = isBlack;
    }

    public Coroutine FadeTo(float targetAlpha, float duration)
    {
        if (canvasGroup == null) return null;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FadeRoutine(targetAlpha, duration));
        return _routine;
    }

    public IEnumerator FadeOutIn(float holdSeconds, float fadeDuration)
    {
        float d = fadeDuration > 0f ? fadeDuration : defaultFadeDuration;
        yield return FadeRoutine(1f, d);

        if (holdSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdSeconds);

        yield return FadeRoutine(0f, d);
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (canvasGroup == null) yield break;

        float from = canvasGroup.alpha;
        float t = 0f;
        float d = Mathf.Max(0.01f, duration);

        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            canvasGroup.alpha = Mathf.Lerp(from, targetAlpha, k);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        bool block = targetAlpha > 0.999f;
        canvasGroup.blocksRaycasts = block;
        canvasGroup.interactable = block;
        _routine = null;
    }
}
