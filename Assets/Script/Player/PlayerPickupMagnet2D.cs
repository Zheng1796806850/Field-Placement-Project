using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PlayerPickupMagnet2D : MonoBehaviour
{
    [Header("Magnet Settings")]
    public Transform attractTarget;
    [Min(0.01f)] public float attractionSpeed = 7f;

    [Header("Filter")]
    public LayerMask dropLayers = ~0;

    [Header("Refs")]
    public PlayerResourceInventory inventory;

    [Header("Debug")]
    public bool drawGizmo = true;

    private Collider2D _trigger;

    private void Reset()
    {
        _trigger = GetComponent<Collider2D>();
        if (_trigger != null) _trigger.isTrigger = true;

        if (attractTarget == null)
            attractTarget = transform.parent != null ? transform.parent : transform;
    }

    private void Awake()
    {
        _trigger = GetComponent<Collider2D>();
        if (_trigger == null)
        {
            enabled = false;
            return;
        }

        _trigger.isTrigger = true;

        if (attractTarget == null)
            attractTarget = transform.parent != null ? transform.parent : transform;

        if (inventory == null)
            inventory = PlayerResourceInventory.Instance != null ? PlayerResourceInventory.Instance : FindFirstObjectByType<PlayerResourceInventory>();
    }

    private void OnTriggerEnter2D(Collider2D other) => TryAttract(other);
    private void OnTriggerStay2D(Collider2D other) => TryAttract(other);

    private void TryAttract(Collider2D other)
    {
        if (other == null) return;

        if ((dropLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        var drop = other.GetComponentInParent<ResourceDrop2D>();
        if (drop == null) return;

        if (inventory != null && !inventory.CanAcceptAny(drop.resourceType, drop.amount))
        {
            drop.CancelAttract();
            return;
        }

        drop.BeginAttract(attractTarget, attractionSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmo) return;

        var c = GetComponent<Collider2D>();
        var circle = c as CircleCollider2D;
        if (circle != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 center = transform.TransformPoint(circle.offset);
            float r = circle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
            Gizmos.DrawWireSphere(center, r);
        }
    }
}