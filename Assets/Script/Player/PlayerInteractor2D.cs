using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerInteractor2D : MonoBehaviour
{
    [Header("Input")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Detection")]
    public string interactableTag = "Interactable";
    public bool requireTag = false;

    private readonly List<IInteractable> inRange = new List<IInteractable>();
    private IInteractable current;

    private void Update()
    {
        SelectBestInteractable();

        if (Input.GetKeyDown(interactKey))
        {
            if (current != null && current.CanInteract(gameObject))
            {
                current.Interact(gameObject);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (requireTag && !other.CompareTag(interactableTag)) return;

        var interactable = other.GetComponentInParent<IInteractable>();
        if (interactable == null) return;

        if (!inRange.Contains(interactable))
            inRange.Add(interactable);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (requireTag && !other.CompareTag(interactableTag)) return;

        var interactable = other.GetComponentInParent<IInteractable>();
        if (interactable == null) return;

        inRange.Remove(interactable);

        if (current == interactable)
            current = null;
    }

    private void SelectBestInteractable()
    {
        IInteractable best = null;

        for (int i = inRange.Count - 1; i >= 0; i--)
        {
            var item = inRange[i];
            if (item == null)
            {
                inRange.RemoveAt(i);
                continue;
            }

            if (!item.CanInteract(gameObject))
                continue;

            if (best == null || item.Priority > best.Priority)
                best = item;
        }

        current = best;
    }
}
