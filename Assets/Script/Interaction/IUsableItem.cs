using System;
using UnityEngine;

public struct UseContext
{
    public GameObject user;
    public PlayerHungerThirst vitals;
    public PlayerResourceInventory inventory;
    public int slotIndex;
    public Action<string> pushMessage;
}

public interface IUsableItem
{
    bool Use(UseContext context);
}