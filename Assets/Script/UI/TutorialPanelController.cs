using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TutorialPanelController : MonoBehaviour
{
    [Header("Panel Root (must start inactive)")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Scale animation target. If empty, uses panelRoot so nav buttons and page content share one scale.")]
    [SerializeField] private Transform panelScaleTarget;

    [Header("Tutorial Pages (multiple GameObjects)")]
    [SerializeField] private List<GameObject> pages = new List<GameObject>();

    [Header("Buttons")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button skipButton;
    [SerializeField] private Button closeButton;

    [Header("Behavior")]
    [SerializeField] private bool completeOnLastNext = true;
    [SerializeField] private bool hideNextOnLastPage = false;
    [SerializeField] private bool hidePreviousOnFirstPage = false;

    [Header("Optional Focus")]
    [SerializeField] private bool selectNextOnOpen = true;

    [Header("Panel Pop Animation")]
    [SerializeField] private bool usePanelPopAnimation = true;
    [SerializeField, Min(0.01f)] private float showDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float hideDuration = 0.16f;
    [SerializeField, Range(0.01f, 1f)] private float hiddenScaleMultiplier = 0.75f;

    public bool IsCompleted { get; private set; }
    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    public event Action OnTutorialCompleted;

    private int _pageIndex;
    private Coroutine _panelAnimRoutine;
    private Vector3 _baseScale = Vector3.one;
    private Transform _cachedScaleTarget;
    private bool _baseScaleCached;

    private void Awake()
    {
        ResolveScaleTarget();
        if (panelScaleTarget != null)
            _baseScale = panelScaleTarget.localScale;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        DisableAllPages();
    }

    private void OnEnable()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(HandleNext);
            nextButton.onClick.AddListener(HandleNext);
        }

        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(HandlePrevious);
            previousButton.onClick.AddListener(HandlePrevious);
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(HandleSkip);
            skipButton.onClick.AddListener(HandleSkip);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HandleClose);
            closeButton.onClick.AddListener(HandleClose);
        }
    }

    private void OnDisable()
    {
        if (nextButton != null)
            nextButton.onClick.RemoveListener(HandleNext);

        if (previousButton != null)
            previousButton.onClick.RemoveListener(HandlePrevious);

        if (skipButton != null)
            skipButton.onClick.RemoveListener(HandleSkip);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(HandleClose);
    }

    public void BeginTutorial()
    {
        IsCompleted = false;

        if (pages == null || pages.Count == 0)
        {
            CompleteTutorial();
            return;
        }

        SetPanelVisible(true, instant: false);

        _pageIndex = FindFirstValidPageIndex();
        ApplyPage(_pageIndex);
        UpdateButtonInteractable();

        if (selectNextOnOpen && EventSystem.current != null && nextButton != null)
            EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
    }

    public void HidePanelOnly()
    {
        DisableAllPages();
        SetPanelVisible(false, instant: false);
    }

    private int FindFirstValidPageIndex()
    {
        if (pages == null) return 0;
        for (int i = 0; i < pages.Count; i++)
            if (pages[i] != null)
                return i;
        return 0;
    }

    private void DisableAllPages()
    {
        if (pages == null) return;
        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(false);
        }
    }

    private void ApplyPage(int index)
    {
        if (pages == null) return;

        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] == null) continue;
            pages[i].SetActive(i == index);
        }
    }

    private void UpdateButtonInteractable()
    {
        if (previousButton != null)
        {
            bool atFirst = _pageIndex <= 0;
            if (hidePreviousOnFirstPage)
                previousButton.gameObject.SetActive(!atFirst);
            else
                previousButton.gameObject.SetActive(true);

            previousButton.interactable = _pageIndex > 0;
        }

        if (nextButton != null)
        {
            bool hasPages = pages != null && pages.Count > 0;
            bool atLast = hasPages && _pageIndex >= pages.Count - 1;

            if (hideNextOnLastPage)
                nextButton.gameObject.SetActive(!atLast);
            else
                nextButton.gameObject.SetActive(true);

            if (atLast && !completeOnLastNext)
                nextButton.interactable = false;
            else
                nextButton.interactable = true;
        }

        if (skipButton != null)
            skipButton.interactable = true;

        if (closeButton != null)
            closeButton.interactable = true;
    }

    private void HandleNext()
    {
        if (IsCompleted) return;
        if (pages == null || pages.Count == 0) { CompleteTutorial(); return; }

        int lastIndex = pages.Count - 1;
        if (_pageIndex >= lastIndex)
        {
            if (completeOnLastNext)
                CompleteTutorial();
            return;
        }

        _pageIndex = Mathf.Clamp(_pageIndex + 1, 0, lastIndex);
        ApplyPage(_pageIndex);
        UpdateButtonInteractable();
    }

    private void HandlePrevious()
    {
        if (IsCompleted) return;
        if (pages == null || pages.Count == 0) return;

        if (_pageIndex <= 0) return;

        _pageIndex = Mathf.Clamp(_pageIndex - 1, 0, pages.Count - 1);
        ApplyPage(_pageIndex);
        UpdateButtonInteractable();
    }

    private void HandleSkip()
    {
        CompleteTutorial();
    }

    private void HandleClose()
    {
        CompleteTutorial();
    }

    private void CompleteTutorial()
    {
        if (IsCompleted) return;
        IsCompleted = true;

        DisableAllPages();
        SetPanelVisible(false, instant: false);

        OnTutorialCompleted?.Invoke();
    }

    private void SetPanelVisible(bool visible, bool instant)
    {
        if (panelRoot == null)
            return;
        ResolveScaleTarget();

        if (_panelAnimRoutine != null)
            StopCoroutine(_panelAnimRoutine);

        if (!usePanelPopAnimation || instant)
        {
            panelRoot.SetActive(visible);
            if (visible && panelScaleTarget != null)
                panelScaleTarget.localScale = _baseScale;
            return;
        }

        _panelAnimRoutine = StartCoroutine(AnimatePanelScale(visible));
    }

    private IEnumerator AnimatePanelScale(bool show)
    {
        if (panelRoot == null)
            yield break;
        ResolveScaleTarget();
        if (panelScaleTarget == null)
            yield break;

        Vector3 hidden = _baseScale * hiddenScaleMultiplier;
        float duration = Mathf.Max(0.01f, show ? showDuration : hideDuration);

        if (show)
        {
            panelRoot.SetActive(true);
            panelScaleTarget.localScale = hidden;
        }

        Vector3 from = show ? hidden : panelScaleTarget.localScale;
        Vector3 to = show ? _baseScale : hidden;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = show ? 1f - Mathf.Pow(1f - k, 3f) : k * k;
            panelScaleTarget.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        panelScaleTarget.localScale = to;
        if (!show)
            panelRoot.SetActive(false);

        _panelAnimRoutine = null;
    }

    private void ResolveScaleTarget()
    {
        if (panelRoot == null)
            return;

        if (panelScaleTarget == null)
            panelScaleTarget = panelRoot.transform;

        if (panelScaleTarget == null)
            return;

        if (!_baseScaleCached || _cachedScaleTarget != panelScaleTarget)
        {
            _baseScale = panelScaleTarget.localScale;
            _cachedScaleTarget = panelScaleTarget;
            _baseScaleCached = true;
        }
    }
}

