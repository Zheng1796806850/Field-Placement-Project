using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StorageSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI")]
    public Image icon;
    public TextMeshProUGUI countLabel;
    public TextMeshProUGUI nameLabel;
    public Sprite emptyIcon;

    private StoragePanelHUD _owner;
    private int _slotIndex = -1;
    private bool _hasItem;
    private ResourceType _resourceType;
    private Canvas _dragCanvas;
    private GameObject _dragVisual;
    private Coroutine _deferredPayloadReset;

    public void Configure(StoragePanelHUD owner, int slotIndex)
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
        if (nameLabel != null) nameLabel.text = "";
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_hasItem || eventData.button != PointerEventData.InputButton.Left || _owner == null)
            return;

        if (_deferredPayloadReset != null)
        {
            StopCoroutine(_deferredPayloadReset);
            _deferredPayloadReset = null;
        }

        BackpackSlotUI.DragPayload.active = true;
        BackpackSlotUI.DragPayload.source = null;
        BackpackSlotUI.DragPayload.sourceSlotIndex = _slotIndex;
        BackpackSlotUI.DragPayload.resourceType = _resourceType;
        BackpackSlotUI.DragPayload.DropConsumedByValidTarget = false;
        BackpackSlotUI.DragPayload.sourceContainerType = BackpackSlotUI.DragContainerType.Storage;
        BackpackSlotUI.DragPayload.sourceBackpackPanel = null;
        BackpackSlotUI.DragPayload.sourceStoragePanel = _owner;

        if (_dragCanvas == null)
            _dragCanvas = GetComponentInParent<Canvas>() != null ? GetComponentInParent<Canvas>().rootCanvas : null;
        if (_dragCanvas == null || icon == null || icon.sprite == null)
            return;

        _dragVisual = new GameObject("StorageDragVisual");
        _dragVisual.transform.SetParent(_dragCanvas.transform, false);
        _dragVisual.transform.SetAsLastSibling();

        var visualImage = _dragVisual.AddComponent<Image>();
        visualImage.sprite = icon.sprite;
        visualImage.raycastTarget = false;
        visualImage.preserveAspect = true;
        visualImage.color = new Color(1f, 1f, 1f, 0.9f);

        var rect = _dragVisual.GetComponent<RectTransform>();
        rect.sizeDelta = icon.rectTransform.rect.size;
        UpdateDragVisualPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!BackpackSlotUI.DragPayload.active)
            return;
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
        if (!BackpackSlotUI.DragPayload.active || _owner == null)
            return;
        if (BackpackSlotUI.DragPayload.sourceContainerType == BackpackSlotUI.DragContainerType.Storage &&
            BackpackSlotUI.DragPayload.sourceStoragePanel == _owner &&
            BackpackSlotUI.DragPayload.sourceSlotIndex == _slotIndex)
            return;

        bool moved = false;
        if (BackpackSlotUI.DragPayload.sourceContainerType == BackpackSlotUI.DragContainerType.Storage &&
            BackpackSlotUI.DragPayload.sourceStoragePanel == _owner)
        {
            moved = _owner.HandleStorageSlotDrop(BackpackSlotUI.DragPayload.sourceSlotIndex, _slotIndex);
        }
        else if (BackpackSlotUI.DragPayload.sourceContainerType == BackpackSlotUI.DragContainerType.Backpack &&
                 BackpackSlotUI.DragPayload.sourceBackpackPanel != null)
        {
            moved = _owner.HandleDropFromBackpack(
                BackpackSlotUI.DragPayload.sourceBackpackPanel,
                BackpackSlotUI.DragPayload.sourceSlotIndex,
                _slotIndex
            );
        }

        if (moved)
            BackpackSlotUI.DragPayload.DropConsumedByValidTarget = true;

        BackpackSlotUI.DragPayload.Reset();
    }

    private IEnumerator ResetPayloadAfterPointerEvents()
    {
        yield return null;
        if (BackpackSlotUI.DragPayload.active &&
            BackpackSlotUI.DragPayload.sourceContainerType == BackpackSlotUI.DragContainerType.Storage &&
            BackpackSlotUI.DragPayload.sourceStoragePanel == _owner &&
            BackpackSlotUI.DragPayload.sourceSlotIndex == _slotIndex)
        {
            BackpackSlotUI.DragPayload.Reset();
        }

        _deferredPayloadReset = null;
    }

    private void UpdateDragVisualPosition(PointerEventData eventData)
    {
        if (_dragVisual == null || _dragCanvas == null)
            return;

        RectTransform canvasRect = _dragCanvas.transform as RectTransform;
        RectTransform dragRect = _dragVisual.transform as RectTransform;
        if (canvasRect == null || dragRect == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out var localPoint))
            dragRect.localPosition = localPoint;
    }

    private void ClearDragVisual()
    {
        if (_dragVisual != null)
            Destroy(_dragVisual);
        _dragVisual = null;
    }
}

