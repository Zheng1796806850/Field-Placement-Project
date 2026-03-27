using UnityEngine;
using UnityEngine.UI;

public class BackpackButtonBinder : MonoBehaviour
{
    [Header("Refs")]
    public Button button;
    public BackpackPanelHUD backpackPanel;

    [Header("Auto Resolve")]
    public bool findButtonOnSelfIfNull = true;
    public bool findBackpackPanelInSceneIfNull = true;
    public bool includeInactiveWhenFindingPanel = true;

    [Header("Binding")]
    public bool bindOnEnable = true;

    private void Awake()
    {
        ResolveRefs();
        if (!bindOnEnable)
            Bind();
    }

    private void OnEnable()
    {
        ResolveRefs();
        if (bindOnEnable)
            Bind();
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Bind()
    {
        if (button == null || backpackPanel == null)
            return;

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    public void HandleClick()
    {
        if (backpackPanel == null)
        {
            ResolveRefs();
            if (backpackPanel == null) return;
        }

        backpackPanel.Toggle();
    }

    private void ResolveRefs()
    {
        if (button == null && findButtonOnSelfIfNull)
            button = GetComponent<Button>();

        if (backpackPanel == null && findBackpackPanelInSceneIfNull)
            backpackPanel = FindFirstObjectByType<BackpackPanelHUD>(
                includeInactiveWhenFindingPanel ? FindObjectsInactive.Include : FindObjectsInactive.Exclude
            );
    }
}

