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

    public void SetMaxHP(int newMaxHP, bool fillToMax = true)
    {
        if (newMaxHP < 1) newMaxHP = 1;

        maxHP = newMaxHP;

        if (!dead)
        {
            if (fillToMax) currentHP = maxHP;
            else currentHP = Mathf.Clamp(currentHP, 0, maxHP);

            OnHealthChanged?.Invoke(currentHP, maxHP);
        }
    }

    public void ApplyMaxHPMultiplier(float multiplier, bool fillToMax = true)
    {
        if (multiplier <= 0f) multiplier = 0.01f;

        int newMax = Mathf.Max(1, Mathf.RoundToInt(maxHP * multiplier));
        SetMaxHP(newMax, fillToMax);
    }
}
