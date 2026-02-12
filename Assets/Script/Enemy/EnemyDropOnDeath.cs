using UnityEngine;

/// <summary>
/// Spawns resource drops when an enemy dies (Health.OnDied).
/// Attach this to your enemy prefab (root recommended).
/// </summary>
[DisallowMultipleComponent]
public class EnemyDropOnDeath : MonoBehaviour
{
    [Header("Refs")]
    public Health health;

    [Tooltip("If null, tries to find Health in children.")]
    public bool searchHealthInChildren = true;

    [Header("Drop Config")]
    public ZombieDropTableSO dropTable;

    [Tooltip("Prefab that has ResourceDrop2D on it.")]
    public GameObject dropPrefab;

    [Header("Spawn Placement")]
    [Tooltip("Local offset applied at spawn (e.g., slightly above the ground).")]
    public Vector2 spawnOffset = new Vector2(0f, 0.2f);

    [Tooltip("Random scatter radius around the spawn position.")]
    [Min(0f)] public float scatterRadius = 0.25f;

    [Tooltip("If true, rotate drops randomly (useful for non-symmetric sprites).")]
    public bool randomRotation = false;

    [Header("Safety")]
    [Tooltip("Avoid duplicate spawns if OnDied is triggered multiple times.")]
    public bool spawnOnlyOnce = true;

    [Header("Debug")]
    public bool logDrops = false;

    private bool _spawned;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
            if (health == null && searchHealthInChildren)
                health = GetComponentInChildren<Health>();
        }

        if (health != null)
            health.OnDied += HandleDied;
        else
            Debug.LogWarning($"[EnemyDropOnDeath] {name}: no Health found, drops won't spawn.");
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        if (spawnOnlyOnce && _spawned) return;
        _spawned = true;

        if (dropPrefab == null || dropTable == null)
            return;

        var drops = dropTable.RollDrops();
        if (drops == null || drops.Count == 0) return;

        Vector3 basePos = transform.position + (Vector3)spawnOffset;

        foreach (var d in drops)
        {
            Vector2 scatter = (scatterRadius > 0f) ? Random.insideUnitCircle * scatterRadius : Vector2.zero;
            Vector3 pos = basePos + (Vector3)scatter;

            Quaternion rot = randomRotation ? Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)) : Quaternion.identity;

            var go = Instantiate(dropPrefab, pos, rot);

            var drop = go.GetComponentInChildren<ResourceDrop2D>();
            if (drop == null)
                drop = go.GetComponent<ResourceDrop2D>();

            if (drop != null)
                drop.Configure(d.type, d.amount);

            if (logDrops)
                Debug.Log($"[EnemyDropOnDeath] {name} dropped {d.type} x{d.amount}");
        }
    }
}
