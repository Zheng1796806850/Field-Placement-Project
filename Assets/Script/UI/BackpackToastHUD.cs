using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BackpackToastHUD : MonoBehaviour
{
    [Header("Refs")]
    public PlayerResourceInventory inventory;

    [Header("UI")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI label;

    [Header("Timing")]
    [Min(0.1f)] public float holdSeconds = 1.2f;
    [Min(0.05f)] public float fadeSeconds = 0.25f;

    [Header("Behavior")]
    public bool queueMessages = true;

    private readonly Queue<string> _queue = new Queue<string>();
    private float _until;
    private bool _showing;

    private void Awake()
    {
        if (inventory == null)
            inventory = PlayerResourceInventory.Instance != null ? PlayerResourceInventory.Instance : FindFirstObjectByType<PlayerResourceInventory>(FindObjectsInactive.Include);

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (label != null) label.text = "";
    }

    private void OnEnable()
    {
        if (inventory == null)
            inventory = PlayerResourceInventory.Instance != null ? PlayerResourceInventory.Instance : FindFirstObjectByType<PlayerResourceInventory>(FindObjectsInactive.Include);

        if (inventory != null)
            inventory.OnInventoryMessage += Enqueue;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.OnInventoryMessage -= Enqueue;
    }

    private void Update()
    {
        if (!_showing)
        {
            if (_queue.Count > 0) ShowNext();
            return;
        }

        if (Time.unscaledTime < _until)
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            return;
        }

        float t = (Time.unscaledTime - _until) / Mathf.Max(0.0001f, fadeSeconds);
        float a = 1f - Mathf.Clamp01(t);
        if (canvasGroup != null) canvasGroup.alpha = a;

        if (a <= 0.001f)
        {
            _showing = false;
            if (label != null) label.text = "";
        }
    }

    private void Enqueue(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return;

        if (!queueMessages)
        {
            _queue.Clear();
            _queue.Enqueue(msg);
        }
        else
        {
            _queue.Enqueue(msg);
        }

        if (!_showing) ShowNext();
    }

    private void ShowNext()
    {
        if (_queue.Count == 0) return;

        _showing = true;
        string msg = _queue.Dequeue();

        if (label != null) label.text = msg;
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        _until = Time.unscaledTime + holdSeconds;
    }
}