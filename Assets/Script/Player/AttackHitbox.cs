using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    public int damage = 10;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Health hp = other.GetComponentInParent<Health>();
        if (hp != null)
        {
            hp.TakeDamage(damage);
        }
    }
}
