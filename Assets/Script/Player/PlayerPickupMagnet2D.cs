using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PlayerPickupMagnet2D : MonoBehaviour
{
    [Header("Magnet Settings")]
    public Transform attractTarget;
    [Min(0.01f)] public float attractionSpeed = 7f;

    [Header("Delay")]
    [Tooltip("Drop must stay inside the magnet trigger for this long before attraction starts. 0 = immediate.")]
    [Min(0f)] public float attractDelaySeconds = 0.35f;
    [Tooltip("If true, delay uses unscaled time (ignores pause).")]
    public bool useUnscaledTimeForDelay;

    [Header("Filter")]
    public LayerMask dropLayers = ~0;

    [Header("Refs")]
    public PlayerResourceInventory inventory;

    [Header("Debug")]
    public bool drawGizmo = true;

    private Collider2D _trigger;

    /// <summary>ResourceDrop2D instance ID → clock time when it first entered the magnet trigger.</summary>
    private readonly Dictionary<int, float> _firstSeenTimeByDropId = new Dictionary<int, float>();

    private float DelayClockNow => useUnscaledTimeForDelay ? Time.unscaledTime : Time.time;

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        RegisterDropEnter(other);
        TryAttract(other);
    }

    private void OnTriggerStay2D(Collider2D other) => TryAttract(other);

    private void OnTriggerExit2D(Collider2D other)
    {
        var drop = other != null ? other.GetComponentInParent<ResourceDrop2D>() : null;
        if (drop == null) return;
        _firstSeenTimeByDropId.Remove(drop.GetInstanceID());
        // Do not CancelAttract here: the drop may have already started moving and left the trigger on the way to the player.
    }

    private void RegisterDropEnter(Collider2D other)
    {
        if (other == null) return;
        if ((dropLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        var drop = other.GetComponentInParent<ResourceDrop2D>();
        if (drop == null) return;

        int id = drop.GetInstanceID();
        if (!_firstSeenTimeByDropId.ContainsKey(id))
            _firstSeenTimeByDropId[id] = DelayClockNow;
    }

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
            _firstSeenTimeByDropId.Remove(drop.GetInstanceID());
            return;
        }

        int id = drop.GetInstanceID();
        if (attractDelaySeconds > 0f)
        {
            if (!_firstSeenTimeByDropId.TryGetValue(id, out float t0))
            {
                t0 = DelayClockNow;
                _firstSeenTimeByDropId[id] = t0;
            }

            if (DelayClockNow - t0 < attractDelaySeconds)
                return;
        }

        drop.BeginAttract(attractTarget, attractionSpeed);
        _firstSeenTimeByDropId.Remove(id);
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