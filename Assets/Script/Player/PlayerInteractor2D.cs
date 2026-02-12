using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerInteractor2D : MonoBehaviour
{
    [Header("Input")]
    public KeyCode interactKey = KeyCode.E;
    public bool allowHoldToStartInteraction = true;
    public bool blockNewInteractionsWhileTimedActionActive = true;

    [Header("Detection")]
    public string interactableTag = "Interactable";
    public bool requireTag = false;
    public Transform distanceOrigin;

    [Header("Selection")]
    public bool tieBreakByNearest = true;
    public float maxInteractDistance = 0f;

    [Header("Debug")]
    public bool debugLogs = false;

    private readonly Dictionary<IInteractable, int> overlapCounts = new Dictionary<IInteractable, int>(32);
    private readonly List<IInteractable> candidates = new List<IInteractable>(32);
    private IInteractable current;
    private bool _interactedThisHold = false;

    private TimedActionController _timed;

    private void Awake()
    {
        _timed = GetComponentInParent<TimedActionController>();
        if (_timed == null) _timed = GetComponent<TimedActionController>();
    }

    private void Update()
    {
        SelectBestInteractable();

        bool keyHeld = Input.GetKey(interactKey);
        bool keyDown = Input.GetKeyDown(interactKey);

        if (!keyHeld)
            _interactedThisHold = false;

        if (blockNewInteractionsWhileTimedActionActive && _timed != null && _timed.IsBusy)
            return;

        if (keyDown)
        {
            if (TryInteractCurrent())
                _interactedThisHold = true;
        }
        else if (allowHoldToStartInteraction && keyHeld && !_interactedThisHold)
        {
            if (TryInteractCurrent())
                _interactedThisHold = true;
        }
    }

    private bool TryInteractCurrent()
    {
        if (current == null) return false;
        if (!current.CanInteract(gameObject)) return false;

        current.Interact(gameObject);

        if (debugLogs)
        {
            var c = current as Component;
            Debug.Log($"[Interactor] Interacted with {(c != null ? c.name : current.ToString())}");
        }

        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        var interactable = other.GetComponentInParent<IInteractable>();
        if (interactable == null) return;

        if (requireTag && !PassesTagFilter(other, interactable))
            return;

        if (overlapCounts.TryGetValue(interactable, out int count))
        {
            overlapCounts[interactable] = count + 1;
        }
        else
        {
            overlapCounts.Add(interactable, 1);
            candidates.Add(interactable);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;

        var interactable = other.GetComponentInParent<IInteractable>();
        if (interactable == null) return;

        if (requireTag && !PassesTagFilter(other, interactable))
            return;

        if (!overlapCounts.TryGetValue(interactable, out int count))
            return;

        count -= 1;

        if (count <= 0)
        {
            overlapCounts.Remove(interactable);
            candidates.Remove(interactable);

            if (current == interactable)
                current = null;
        }
        else
        {
            overlapCounts[interactable] = count;
        }
    }

    private bool PassesTagFilter(Collider2D other, IInteractable interactable)
    {
        if (other.CompareTag(interactableTag))
            return true;

        var comp = interactable as Component;
        if (comp != null && comp.CompareTag(interactableTag))
            return true;

        return false;
    }

    private void SelectBestInteractable()
    {
        IInteractable best = null;
        float bestDist = float.MaxValue;

        Vector3 origin = (distanceOrigin != null) ? distanceOrigin.position : transform.position;

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            var item = candidates[i];

            if (item == null)
            {
                candidates.RemoveAt(i);
                continue;
            }

            if (!item.CanInteract(gameObject))
                continue;

            float dist = GetDistance(origin, item);

            if (maxInteractDistance > 0f && dist > maxInteractDistance)
                continue;

            if (best == null)
            {
                best = item;
                bestDist = dist;
                continue;
            }

            if (item.Priority > best.Priority)
            {
                best = item;
                bestDist = dist;
                continue;
            }

            if (tieBreakByNearest && item.Priority == best.Priority && dist < bestDist)
            {
                best = item;
                bestDist = dist;
            }
        }

        current = best;
    }

    private float GetDistance(Vector3 origin, IInteractable item)
    {
        var c = item as Component;
        if (c == null) return float.MaxValue;
        return Vector2.Distance(origin, c.transform.position);
    }
}
