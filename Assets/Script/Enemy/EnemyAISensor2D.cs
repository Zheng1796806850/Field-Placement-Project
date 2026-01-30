using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAISensor2D : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("自动在父物体里找 EnemyAI2D；也可以手动拖拽。")]
    public EnemyAI2D ai;

    private void Reset()
    {
        ai = GetComponentInParent<EnemyAI2D>();
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    private void Awake()
    {
        if (ai == null) ai = GetComponentInParent<EnemyAI2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (ai == null) return;
        ai.SensorEnter(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (ai == null) return;
        ai.SensorExit(other);
    }
}
