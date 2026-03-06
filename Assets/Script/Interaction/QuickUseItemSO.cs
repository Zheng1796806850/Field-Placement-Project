using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Quick Use Item", fileName = "QuickUseItem")]
public class QuickUseItemSO : ScriptableObject, IUsableItem
{
    public enum HpTarget
    {
        Player = 0,
        HouseCore = 1
    }

    [Header("Display")]
    public string displayName;
    public Sprite icon;

    [Header("Cost")]
    public ResourceType resourceType;
    [Min(0)] public int consumeAmount = 1;

    [Header("Cooldown")]
    [Min(0f)] public float cooldownSeconds = 0.5f;

    [Header("Effects")]
    public float addHunger;
    public float addThirst;
    public int addHP;
    public HpTarget hpTarget = HpTarget.Player;

    [Header("Messaging")]
    public bool showSuccessMessage = true;
    public bool showFailMessage = true;
    public string successMessageOverride;
    public string insufficientMessageOverride;
    public string invalidTargetMessage = "Cannot use now";

    public bool Use(UseContext context)
    {
        if (context.inventory == null)
        {
            context.pushMessage?.Invoke("No inventory");
            return false;
        }

        if (!ValidateTargets(context, out string targetFail))
        {
            if (showFailMessage && !string.IsNullOrWhiteSpace(targetFail))
                context.pushMessage?.Invoke(targetFail);

            return false;
        }

        if (consumeAmount > 0)
        {
            int have = context.inventory.Get(resourceType);
            if (have < consumeAmount)
            {
                if (showFailMessage)
                {
                    string msg = !string.IsNullOrWhiteSpace(insufficientMessageOverride)
                        ? insufficientMessageOverride
                        : $"Not enough {resourceType} ({have}/{consumeAmount})";
                    context.pushMessage?.Invoke(msg);
                }
                return false;
            }

            if (!context.inventory.Spend(resourceType, consumeAmount))
            {
                if (showFailMessage)
                {
                    string msg = !string.IsNullOrWhiteSpace(insufficientMessageOverride)
                        ? insufficientMessageOverride
                        : $"Not enough {resourceType}";
                    context.pushMessage?.Invoke(msg);
                }
                return false;
            }
        }

        ApplyEffects(context);

        if (showSuccessMessage)
        {
            string msg = !string.IsNullOrWhiteSpace(successMessageOverride)
                ? successMessageOverride
                : $"Used {GetDisplayName()}";
            context.pushMessage?.Invoke(msg);
        }

        return true;
    }

    private bool ValidateTargets(UseContext context, out string failMessage)
    {
        failMessage = null;

        if ((addHunger != 0f || addThirst != 0f) && context.vitals == null)
        {
            failMessage = invalidTargetMessage;
            return false;
        }

        if (addHP != 0)
        {
            var hp = ResolveHpTarget(context);
            if (hp == null || hp.dead)
            {
                failMessage = invalidTargetMessage;
                return false;
            }
        }

        return true;
    }

    private void ApplyEffects(UseContext context)
    {
        if (context.vitals != null)
        {
            if (addHunger != 0f) context.vitals.RestoreHunger(addHunger);
            if (addThirst != 0f) context.vitals.RestoreThirst(addThirst);
        }

        if (addHP != 0)
        {
            var hp = ResolveHpTarget(context);
            if (hp != null && !hp.dead)
            {
                if (addHP > 0) hp.Heal(addHP);
                else if (addHP < 0) hp.TakeDamage(-addHP);
            }
        }
    }

    private Health ResolveHpTarget(UseContext context)
    {
        if (hpTarget == HpTarget.HouseCore)
        {
            var house = HouseObjective.Instance != null
                ? HouseObjective.Instance
                : UnityEngine.Object.FindFirstObjectByType<HouseObjective>(FindObjectsInactive.Include);

            if (house != null && house.coreHealth != null)
                return house.coreHealth;

            return null;
        }

        if (context.vitals != null && context.vitals.health != null)
            return context.vitals.health;

        if (context.user != null)
            return context.user.GetComponentInParent<Health>();

        return null;
    }

    private string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
        return name;
    }
}