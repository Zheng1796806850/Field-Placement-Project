using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAISensor2D : MonoBehaviour
{
    [Header("Refs")]
    public EnemyAI2D ai;

    private readonly HashSet<Collider2D> overlaps = new HashSet<Collider2D>();

    private void Reset()
    {
        ai = GetComponentInParent<EnemyAI2D>();
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    private void Awake()
    {
        if (ai == null) ai = GetComponentInParent<EnemyAI2D>();

        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    private void OnDisable()
    {
        overlaps.Clear();
    }

    private void LateUpdate()
    {
        if (overlaps.Count == 0) return;

        overlaps.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);
    }

    public bool Contains(Collider2D other)
    {
        if (other == null) return false;
        return overlaps.Contains(other);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        overlaps.Add(other);

        if (ai == null) return;
        ai.SensorEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;

        overlaps.Remove(other);

        if (ai == null) return;
        ai.SensorExit(other);
    }
}