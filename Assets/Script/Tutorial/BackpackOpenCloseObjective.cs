using UnityEngine;

public class BackpackOpenCloseObjective : TutorialObjective
{
    public BackpackPanelHUD backpackPanel;
    public bool requireOpenAndClose = true;

    private bool _opened;
    private bool _closedAfterOpen;

    protected override void OnBegin()
    {
        if (backpackPanel == null)
            backpackPanel = FindFirstObjectByType<BackpackPanelHUD>(FindObjectsInactive.Include);

        _opened = false;
        _closedAfterOpen = false;

        if (backpackPanel != null)
        {
            backpackPanel.OnPanelVisibilityChanged -= HandlePanelVisibilityChanged;
            backpackPanel.OnPanelVisibilityChanged += HandlePanelVisibilityChanged;
            HandlePanelVisibilityChanged(backpackPanel.IsOpen);
        }
    }

    protected override void OnEnd()
    {
        if (backpackPanel != null)
            backpackPanel.OnPanelVisibilityChanged -= HandlePanelVisibilityChanged;
    }

    private void HandlePanelVisibilityChanged(bool isOpen)
    {
        if (isOpen)
            _opened = true;
        else if (_opened)
            _closedAfterOpen = true;

        if (requireOpenAndClose)
        {
            if (_opened && _closedAfterOpen)
                Complete();
            return;
        }

        if (_opened)
            Complete();
    }

    public override string GetProgressText()
    {
        if (!requireOpenAndClose)
            return _opened ? "Open backpack: Done" : "Open backpack";

        return $"Open backpack: {(_opened ? "Done" : "Pending")} / Close backpack: {(_closedAfterOpen ? "Done" : "Pending")}";
    }
}
