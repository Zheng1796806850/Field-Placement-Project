using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TutorialPanelController : MonoBehaviour
{
    [Header("Panel Root (must start inactive)")]
    [SerializeField] private GameObject panelRoot;

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

    public bool IsCompleted { get; private set; }
    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    public event Action OnTutorialCompleted;

    private int _pageIndex;

    private void Awake()
    {
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

        if (panelRoot != null)
            panelRoot.SetActive(true);

        _pageIndex = FindFirstValidPageIndex();
        ApplyPage(_pageIndex);
        UpdateButtonInteractable();

        if (selectNextOnOpen && EventSystem.current != null && nextButton != null)
            EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
    }

    public void HidePanelOnly()
    {
        DisableAllPages();
        if (panelRoot != null)
            panelRoot.SetActive(false);
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
        if (panelRoot != null)
            panelRoot.SetActive(false);

        OnTutorialCompleted?.Invoke();
    }
}

