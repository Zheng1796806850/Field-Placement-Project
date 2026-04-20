using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Plays a pooled <see cref="SfxId"/> when keyboard/gamepad focus or mouse hover moves between UI controls on this Canvas.
/// Uses <see cref="SfxPlayer.TryPlay"/> so SFX mixer volume applies. Place on the same GameObject as <see cref="Canvas"/>.
/// </summary>
[RequireComponent(typeof(Canvas))]
[DisallowMultipleComponent]
public class CanvasSelectableFocusSfx : MonoBehaviour
{
    [Header("SFX (library + pool)")]
    [Tooltip("Played when a new Button/Toggle under this canvas becomes hovered or EventSystem-selected.")]
    public SfxId selectionSfxId = SfxId.UI_SelectNavigate;

    [Tooltip("When true and the mouse is over a Button/Toggle on this canvas, that wins over EventSystem selection. When false, EventSystem wins; hover is used only when nothing is selected.")]
    public bool pointerHoverTakesPriority = true;

    [Tooltip("Only used when Pointer Hover Takes Priority is on and the mouse is present. If false (default), when the cursor is not over any tracked control on this canvas we report no focus — avoids playing again when you move off a button because EventSystem still has another control selected. If true, keyboard/gamepad selection still drives sound while a mouse is plugged in (can cause that extra sound on mouse-out).")]
    public bool fallBackToEventSystemWhenPointerMisses = false;

    [Tooltip("If false, only EventSystem selection triggers sound (keyboard/gamepad).")]
    public bool playOnPointerHover = true;

    private Canvas _canvas;
    private Selectable _lastTarget;
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(24);

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
    }

    private void OnDisable()
    {
        _lastTarget = null;
    }

    private void LateUpdate()
    {
        Selectable current = ResolveCurrentSelectable();
        if (current == _lastTarget)
            return;

        _lastTarget = current;
        if (current == null)
            return;

        SfxPlayer.TryPlay(selectionSfxId, current.transform.position);
    }

    private Selectable ResolveCurrentSelectable()
    {
        if (_canvas == null)
            return null;

        if (!playOnPointerHover || !Input.mousePresent)
            return SelectableFromEventSystemUnderCanvas();

        if (pointerHoverTakesPriority)
        {
            Selectable hovered = RaycastTopSelectableUnderCanvas();
            if (hovered != null)
                return hovered;
            if (fallBackToEventSystemWhenPointerMisses)
                return SelectableFromEventSystemUnderCanvas();
            return null;
        }

        Selectable fromEvent = SelectableFromEventSystemUnderCanvas();
        if (fromEvent != null)
            return fromEvent;

        return RaycastTopSelectableUnderCanvas();
    }

    private Selectable SelectableFromEventSystemUnderCanvas()
    {
        if (EventSystem.current == null)
            return null;

        GameObject go = EventSystem.current.currentSelectedGameObject;
        if (go == null)
            return null;

        Selectable s = go.GetComponent<Selectable>() ?? go.GetComponentInParent<Selectable>();
        if (!IsTrackedUnderCanvas(s))
            return null;

        return s;
    }

    private Selectable RaycastTopSelectableUnderCanvas()
    {
        if (EventSystem.current == null)
            return null;

        _raycastResults.Clear();
        var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        EventSystem.current.RaycastAll(data, _raycastResults);

        Transform canvasRoot = _canvas.transform;

        for (int i = 0; i < _raycastResults.Count; i++)
        {
            GameObject hit = _raycastResults[i].gameObject;
            if (hit == null)
                continue;

            Transform hitTransform = hit.transform;
            bool underThisCanvas = hitTransform == canvasRoot || hitTransform.IsChildOf(canvasRoot);

            if (!underThisCanvas)
            {
                if (RaycastHitBlocks(hit))
                    return null;
                continue;
            }

            Selectable s = hit.GetComponent<Selectable>() ?? hit.GetComponentInParent<Selectable>();
            if (IsTrackedUnderCanvas(s))
                return s;

            if (RaycastHitBlocks(hit))
                return null;
        }

        return null;
    }

    /// <summary>
    /// True when this raycast hit is a UI graphic that participates in blocking — lower results must be ignored.
    /// </summary>
    private static bool RaycastHitBlocks(GameObject hit)
    {
        var graphic = hit.GetComponent<Graphic>();
        return graphic != null && graphic.isActiveAndEnabled && graphic.raycastTarget;
    }

    private bool IsTrackedUnderCanvas(Selectable s)
    {
        if (s == null || !s.isActiveAndEnabled || !s.IsInteractable())
            return false;

        if (!(s is Button) && !(s is Toggle))
            return false;

        Transform root = _canvas.transform;
        return s.transform == root || s.transform.IsChildOf(root);
    }
}
