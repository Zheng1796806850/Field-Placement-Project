using System;

public static class InventorySlotTransfer
{
    /// <summary>
    /// Slot-based transfer rule:
    /// empty target => move, same type => partial/full stack (overflow remains in source),
    /// different type => swap.
    /// </summary>
    public static bool TryTransfer(ref InventorySlot source, ref InventorySlot target, Func<ResourceType, int> getStackSize)
    {
        if (source.IsEmpty)
            return false;

        if (target.IsEmpty)
        {
            target = source;
            source = InventorySlot.Empty;
            return true;
        }

        if (source.type == target.type)
        {
            int stackSize = Math.Max(1, getStackSize != null ? getStackSize(source.type) : 20);
            int room = Math.Max(0, stackSize - target.amount);
            if (room <= 0)
                return false;

            int moved = Math.Min(room, source.amount);
            if (moved <= 0)
                return false;

            target = new InventorySlot { type = target.type, amount = target.amount + moved };
            int left = source.amount - moved;
            source = left > 0 ? new InventorySlot { type = source.type, amount = left } : InventorySlot.Empty;
            return true;
        }

        InventorySlot temp = source;
        source = target;
        target = temp;
        return true;
    }
}

