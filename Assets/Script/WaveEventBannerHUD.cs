using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WaveEventBannerHUD : MonoBehaviour
{
    [Header("UI")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI label;

    [Header("Timings")]
    [Min(0.01f)] public float fadeInDuration = 0.15f;
    [Min(0.01f)] public float holdDuration = 1.4f;
    [Min(0.01f)] public float fadeOutDuration = 0.25f;

    [Header("Behavior")]
    public bool queueMessages = true;

    private readonly Queue<string> _queue = new Queue<string>(16);
    private Coroutine _runner;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    public void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        if (!queueMessages) _queue.Clear();
        _queue.Enqueue(message);

        if (_runner == null)
            _runner = StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        while (_queue.Count > 0)
        {
            string msg = _queue.Dequeue();
            if (label != null) label.text = msg;

            yield return Fade(0f, 1f, fadeInDuration);
            yield return new WaitForSecondsRealtime(holdDuration);
            yield return Fade(1f, 0f, fadeOutDuration);
        }

        _runner = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float t = 0f;
        canvasGroup.alpha = from;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }

        canvasGroup.alpha = to;
    }
}
