using System;
using UnityEngine;
using Pathfinding;

public class WallDeathHandler : MonoBehaviour
{
    public static event Action<WallDeathHandler> OnAnyWallDestroyed;

    public Health health;

    [Header("A* Graph Update")]
    public bool updateGraphOnDeath = true;

    [Min(0f)] public float boundsPadding = 0.25f;

    private Collider2D[] _colliders;
    private bool _fired;

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        _colliders = GetComponentsInChildren<Collider2D>(true);

        if (health != null)
            health.OnDied += OnWallDestroyed;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDied -= OnWallDestroyed;
    }

    private void OnWallDestroyed()
    {
        if (_fired) return;
        _fired = true;

        OnAnyWallDestroyed?.Invoke(this);

        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        if (_colliders != null)
        {
            foreach (var c in _colliders)
            {
                if (c == null) continue;
                if (!hasBounds) { b = c.bounds; hasBounds = true; }
                else b.Encapsulate(c.bounds);
            }
        }

        if (_colliders != null)
        {
            foreach (var c in _colliders)
            {
                if (c != null) c.enabled = false;
            }
        }

        if (!updateGraphOnDeath) return;
        if (!hasBounds) return;
        if (AstarPath.active == null) return;

        b.Expand(boundsPadding * 2f);
        AstarPath.active.UpdateGraphs(b);
        AstarPath.active.FlushGraphUpdates();
    }
}
