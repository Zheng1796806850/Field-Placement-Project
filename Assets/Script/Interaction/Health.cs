using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    public int maxHP = 10;
    public int currentHP;

    public bool destroyOnDeath = true;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;

    public bool dead;

    private void Awake()
    {
        currentHP = maxHP;
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void Heal(int amount)
    {
        if (dead) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void TakeDamage(int amount)
    {
        if (dead) return;
        if (amount <= 0) return;

        currentHP -= amount;
        OnHealthChanged?.Invoke(currentHP, maxHP);

        if (currentHP <= 0)
        {
            dead = true;
            OnDied?.Invoke();

            if (destroyOnDeath)
                Destroy(gameObject);
        }
    }
}
