using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BackpackSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI")]
    public Image icon;
    public TextMeshProUGUI countLabel;
    public TextMeshProUGUI nameLabel;

    [Header("Empty")]
    public Sprite emptyIcon;

    private BackpackPanelHUD _owner;
    private int _slotIndex = -1;
    private bool _hasItem;
    private ResourceType _resourceType;
    private Canvas _dragCanvas;
    private GameObject _dragVisual;
    private Image _dragVisualImage;
    private Coroutine _deferredPayloadReset;

    public int SlotIndex => _slotIndex;
    public bool HasItem => _hasItem;
    public ResourceType ResourceType => _resourceType;

    public void Configure(BackpackPanelHUD owner, int slotIndex)
    {
        _owner = owner;
        _slotIndex = slotIndex;
    }

    public void SetEmpty()
    {
        _hasItem = false;

        if (icon != null)
        {
            icon.sprite = emptyIcon;
            icon.enabled = true;
            icon.raycastTarget = true;
            // Keep Image enabled for drop raycasts; without a sprite, default Image draws a solid quad — hide via alpha.
            icon.color = emptyIcon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        if (countLabel != null) countLabel.text = "";
        if (nameLabel != null) nameLabel.text = "";
    }

    public void Set(ResourceType type, int amountInStack, int stackSize, Sprite sprite, string displayName)
    {
        _hasItem = true;
        _resourceType = type;

        if (icon != null)
        {
            icon.sprite = sprite != null ? sprite : emptyIcon;
            icon.enabled = icon.sprite != null;
            icon.raycastTarget = true;
            icon.color = Color.white;
        }

        if (countLabel != null) countLabel.text = $"{amountInStack}/{Mathf.Max(1, stackSize)}";
        if (nameLabel != null) nameLabel.text = string.IsNullOrWhiteSpace(displayName) ? type.ToString() : displayName;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_hasItem) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (_deferredPayloadReset != null)
        {
            StopCoroutine(_deferredPayloadReset);
            _deferredPayloadReset = null;
        }

        DragPayload.active = true;
        DragPayload.source = this;
        DragPayload.sourceSlotIndex = _slotIndex;
        DragPayload.resourceType = _resourceType;

        if (_dragCanvas == null)
            _dragCanvas = GetComponentInParent<Canvas>() != null ? GetComponentInParent<Canvas>().rootCanvas : null;

        if (_dragCanvas == null || icon == null || icon.sprite == null)
            return;

        _dragVisual = new GameObject("BackpackDragVisual");
        _dragVisual.transform.SetParent(_dragCanvas.transform, false);
        _dragVisual.transform.SetAsLastSibling();

        _dragVisualImage = _dragVisual.AddComponent<Image>();
        _dragVisualImage.sprite = icon.sprite;
        _dragVisualImage.raycastTarget = false;
        _dragVisualImage.preserveAspect = true;
        _dragVisualImage.color = new Color(1f, 1f, 1f, 0.9f);

        var rect = _dragVisual.GetComponent<RectTransform>();
        rect.sizeDelta = icon.rectTransform.rect.size;
        UpdateDragVisualPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!DragPayload.active) return;
        UpdateDragVisualPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ClearDragVisual();
        if (_deferredPayloadReset != null)
            StopCoroutine(_deferredPayloadReset);
        _deferredPayloadReset = StartCoroutine(ResetPayloadAfterPointerEvents());
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!DragPayload.active) return;
        if (DragPayload.source == null) return;
        if (DragPayload.source == this) return;
        if (_owner == null) return;

        _owner.HandleSlotDrop(DragPayload.sourceSlotIndex, _slotIndex);
        DragPayload.Reset();
        if (_deferredPayloadReset != null)
        {
            StopCoroutine(_deferredPayloadReset);
            _deferredPayloadReset = null;
        }
    }

    private IEnumerator ResetPayloadAfterPointerEvents()
    {
        yield return null;
        if (DragPayload.source == this && DragPayload.active)
            DragPayload.Reset();
        _deferredPayloadReset = null;
    }

    private void UpdateDragVisualPosition(PointerEventData eventData)
    {
        if (_dragVisual == null || _dragCanvas == null) return;

        RectTransform canvasRect = _dragCanvas.transform as RectTransform;
        RectTransform dragRect = _dragVisual.transform as RectTransform;
        if (canvasRect == null || dragRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out var localPoint))
            dragRect.localPosition = localPoint;
    }

    private void ClearDragVisual()
    {
        if (_dragVisual != null)
            Destroy(_dragVisual);

        _dragVisual = null;
        _dragVisualImage = null;
    }

    public static class DragPayload
    {
        public static bool active;
        public static BackpackSlotUI source;
        public static int sourceSlotIndex = -1;
        public static ResourceType resourceType;

        public static void Reset()
        {
            active = false;
            source = null;
            sourceSlotIndex = -1;
            resourceType = default;
        }
    }
}
