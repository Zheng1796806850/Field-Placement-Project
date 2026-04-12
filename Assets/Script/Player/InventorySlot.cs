using System;
using UnityEngine;

/// <summary>One backpack cell: one stack. Empty when amount &lt;= 0.</summary>
[Serializable]
public struct InventorySlot
{
    public ResourceType type;
    public int amount;

    public bool IsEmpty => amount <= 0;

    public static InventorySlot Empty => new InventorySlot { type = default, amount = 0 };
}
