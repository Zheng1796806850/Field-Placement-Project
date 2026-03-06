using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BackpackSlotUI : MonoBehaviour
{
    [Header("UI")]
    public Image icon;
    public TextMeshProUGUI countLabel;
    public TextMeshProUGUI nameLabel;

    [Header("Empty")]
    public Sprite emptyIcon;

    public void SetEmpty()
    {
        if (icon != null)
        {
            icon.sprite = emptyIcon;
            icon.enabled = emptyIcon != null;
        }

        if (countLabel != null) countLabel.text = "";
        if (nameLabel != null) nameLabel.text = "";
    }

    public void Set(ResourceType type, int amountInStack, int stackSize, Sprite sprite, string displayName)
    {
        if (icon != null)
        {
            icon.sprite = sprite != null ? sprite : emptyIcon;
            icon.enabled = icon.sprite != null;
        }

        if (countLabel != null) countLabel.text = $"{amountInStack}/{Mathf.Max(1, stackSize)}";
        if (nameLabel != null) nameLabel.text = string.IsNullOrWhiteSpace(displayName) ? type.ToString() : displayName;
    }
}