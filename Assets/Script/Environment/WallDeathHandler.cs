using System;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class WallDeathHandler : MonoBehaviour
{
    public static event Action<WallDeathHandler> OnAnyWallDestroyed;

    [Header("Refs")]
    public Health health;

    [Header("Blocking Colliders")]
    public Collider2D[] blockingColliders;

    [Header("A* Graph Update")]
    public bool updateGraphOnDeath = true;
    public Collider2D[] graphBoundsColliders;
    [Min(0f)] public float boundsPadding = 0.25f;

    private Collider2D[] _allColliders;
    private readonly List<Collider2D> _scratch = new List<Collider2D>(16);
    private bool _destroyedState;
    private Bounds _cachedBounds;
    private bool _hasCachedBounds;

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        _allColliders = GetComponentsInChildren<Collider2D>(true);

        CaptureGraphBounds();

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
        EnterDestroyedState();
    }

    public void EnterDestroyedState()
    {
        if (_destroyedState) return;

        CaptureGraphBounds();
        _destroyedState = true;

        OnAnyWallDestroyed?.Invoke(this);

        SetBlockingEnabled(false);
        ApplyGraphUpdate();
    }

    public void RestoreBlockingState()
    {
        SetBlockingEnabled(true);
        _destroyedState = false;
        CaptureGraphBounds();
        ApplyGraphUpdate();
    }

    public void RefreshGraphNow()
    {
        CaptureGraphBounds();
        ApplyGraphUpdate();
    }

    private void SetBlockingEnabled(bool enabled)
    {
        if (blockingColliders != null && blockingColliders.Length > 0)
        {
            for (int i = 0; i < blockingColliders.Length; i++)
            {
                var c = blockingColliders[i];
                if (c != null) c.enabled = enabled;
            }
            return;
        }

        if (_allColliders == null)
            _allColliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < _allColliders.Length; i++)
        {
            var c = _allColliders[i];
            if (c == null) continue;
            if (c.isTrigger) continue;
            c.enabled = enabled;
        }
    }

    private void CaptureGraphBounds()
    {
        _scratch.Clear();

        if (graphBoundsColliders != null && graphBoundsColliders.Length > 0)
        {
            for (int i = 0; i < graphBoundsColliders.Length; i++)
            {
                var c = graphBoundsColliders[i];
                if (c != null) _scratch.Add(c);
            }
        }
        else if (_allColliders != null)
        {
            for (int i = 0; i < _allColliders.Length; i++)
            {
                var c = _allColliders[i];
                if (c != null) _scratch.Add(c);
            }
        }

        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < _scratch.Count; i++)
        {
            var c = _scratch[i];
            if (c == null) continue;

            Bounds cb = c.bounds;
            if (cb.size == Vector3.zero) continue;

            if (!hasBounds)
            {
                b = cb;
                hasBounds = true;
            }
            else
            {
                b.Encapsulate(cb);
            }
        }

        if (!hasBounds) return;

        b.Expand(boundsPadding * 2f);
        _cachedBounds = b;
        _hasCachedBounds = true;
    }

    private void ApplyGraphUpdate()
    {
        if (!updateGraphOnDeath) return;
        if (!_hasCachedBounds) return;
        if (AstarPath.active == null) return;

        AstarPath.active.UpdateGraphs(_cachedBounds);
        AstarPath.active.FlushGraphUpdates();
    }
}