using UnityEngine;

[DisallowMultipleComponent]
public class EnemyStatsApplier : MonoBehaviour
{
    [Header("Source")]
    public EnemyStatsSO statsSO;

    [Header("Apply Options")]
    public bool applyOnAwake = true;

    [Tooltip("应用 maxHP 时是否把 currentHP 直接补满到 maxHP。")]
    public bool fillHPToMaxOnApply = true;

    private void Awake()
    {
        if (applyOnAwake) Apply();
    }

    [ContextMenu("Apply Stats Now")]
    public void Apply()
    {
        if (statsSO == null)
        {
            Debug.LogWarning($"[{name}] EnemyStatsApplier: statsSO is null, skip.");
            return;
        }

        EnemyStats s = statsSO.baseStats;
        s.Clamp();

        // Health
        var hp = GetComponentInChildren<Health>();
        if (hp != null)
        {
            hp.SetMaxHP(s.maxHP, fillToMax: fillHPToMaxOnApply);
        }

        // Enemy AI
        var ai = GetComponentInChildren<EnemyAI2D>();
        if (ai != null)
        {
            ai.SetBaseMoveSpeed(s.moveSpeed);
            ai.SetBaseWallDamage(s.wallDamage);

            // 注意：这里只设置墙伤基础值。玩家伤害如果你未来也想纳入 Stats，可以再扩展。
        }
    }
}
