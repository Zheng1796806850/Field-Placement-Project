using UnityEngine;

[CreateAssetMenu(menuName = "AI/Enemy Stats", fileName = "EnemyStats_")]
public class EnemyStatsSO : ScriptableObject
{
    [Header("Base Stats")]
    public EnemyStats baseStats = EnemyStats.Default;

    private void OnValidate()
    {
        baseStats.Clamp();
    }
}
