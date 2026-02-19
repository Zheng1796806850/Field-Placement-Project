using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    public int damage = 10;

    public bool playHitConfirm = true;
    public SfxId hitConfirmSfx = SfxId.Combat_HitConfirm;
    public bool requireEnemyComponent = true;
    public float minHitConfirmInterval = 0.03f;

    private float _lastHitSfxTime;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        Health hp = other.GetComponentInParent<Health>();
        if (hp == null) return;
        if (hp.dead) return;

        bool isEnemy = !requireEnemyComponent || IsEnemy(other);

        if (damage > 0)
            hp.TakeDamage(damage);

        if (playHitConfirm && isEnemy)
        {
            float now = Time.unscaledTime;
            if (minHitConfirmInterval <= 0f || now - _lastHitSfxTime >= minHitConfirmInterval)
            {
                _lastHitSfxTime = now;
                SfxPlayer.TryPlay(hitConfirmSfx, other.transform.position);
            }
        }
    }

    private bool IsEnemy(Collider2D other)
    {
        if (other.GetComponentInParent<WaveEnemyAgent>() != null) return true;
        if (other.GetComponentInParent<EnemyAI2D>() != null) return true;
        return false;
    }
}
