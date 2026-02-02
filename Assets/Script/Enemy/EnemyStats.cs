using System;
using UnityEngine;

[Serializable]
public struct EnemyStats
{
    [Header("Core Stats")]
    [Min(1)] public int maxHP;
    [Min(0f)] public float moveSpeed;
    [Min(0)] public int wallDamage;

    public static EnemyStats Default => new EnemyStats
    {
        maxHP = 10,
        moveSpeed = 2f,
        wallDamage = 5
    };

    public void Clamp()
    {
        if (maxHP < 1) maxHP = 1;
        if (moveSpeed < 0f) moveSpeed = 0f;
        if (wallDamage < 0) wallDamage = 0;
    }
}
