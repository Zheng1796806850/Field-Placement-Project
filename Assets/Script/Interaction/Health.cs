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
        if (maxHP < 1) maxHP = 1;

        if (dead)
            currentHP = 0;
        else
            currentHP = Mathf.Clamp(currentHP <= 0 ? maxHP : currentHP, 0, maxHP);

        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void Heal(int amount)
    {
        if (dead) return;
        if (amount <= 0) return;

        currentHP = Mathf.Min(maxHP, currentHP + amount);
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void TakeDamage(int amount)
    {
        if (dead) return;
        if (amount <= 0) return;

        currentHP = Mathf.Max(0, currentHP - amount);
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

    public void SetCurrentHP(int newCurrentHP, bool reviveIfDead = false)
    {
        if (reviveIfDead)
            dead = false;

        if (dead) return;

        currentHP = Mathf.Clamp(newCurrentHP, 0, maxHP);
        OnHealthChanged?.Invoke(currentHP, maxHP);

        if (currentHP <= 0)
        {
            dead = true;
            OnDied?.Invoke();

            if (destroyOnDeath)
                Destroy(gameObject);
        }
    }

    public void Revive(int hp)
    {
        maxHP = Mathf.Max(1, maxHP);
        dead = false;
        currentHP = Mathf.Clamp(hp, 1, maxHP);
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void RestoreToFull(bool reviveIfDead = true)
    {
        if (reviveIfDead)
            dead = false;

        if (dead) return;

        currentHP = maxHP;
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    public void SetRubbleDeadStateWithoutDiedEvent()
    {
        dead = true;
        currentHP = 0;
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }
}