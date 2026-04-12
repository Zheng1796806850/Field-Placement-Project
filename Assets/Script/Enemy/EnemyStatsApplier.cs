using UnityEngine;

/// <summary>
/// Applies <see cref="EnemyStatsSO"/> to <see cref="Health"/> and <see cref="EnemyAI2D"/> on Awake (optional).
/// This runs independently of <see cref="WaveSpawnController2D"/> wave multipliers.
/// </summary>
[DisallowMultipleComponent]
public class EnemyStatsApplier : MonoBehaviour
{
    public EnemyStatsSO statsSO;

    [Tooltip("When true, Apply() runs in Awake.")]
    public bool applyOnAwake = true;

    [Tooltip("When true, Health max/current HP are set from stats after apply.")]
    public bool fillHPToMaxOnApply = true;

    [Tooltip("When true, EnemyAI2D speed is overwritten by stats SO (calls SetBaseMoveSpeed). When false, prefab EnemyAI2D.moveSpeed is kept.")]
    public bool applyMoveSpeedFromStats = true;

    private void Awake()
    {
        if (applyOnAwake)
            Apply();
    }

    public void Apply()
    {
        if (statsSO == null) return;

        EnemyStats s = statsSO.baseStats;
        s.Clamp();

        var hp = GetComponent<Health>();
        if (hp == null) hp = GetComponentInChildren<Health>();
        if (hp != null)
            hp.SetMaxHP(s.maxHP, fillHPToMaxOnApply);

        var ai = GetComponent<EnemyAI2D>();
        if (ai == null) ai = GetComponentInChildren<EnemyAI2D>();
        if (ai != null)
        {
            if (applyMoveSpeedFromStats)
                ai.SetBaseMoveSpeed(s.moveSpeed);
            ai.SetBaseWallDamage(s.wallDamage);
        }
    }
}
