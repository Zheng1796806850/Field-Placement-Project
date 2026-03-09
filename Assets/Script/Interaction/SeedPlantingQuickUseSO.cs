using UnityEngine;

[CreateAssetMenu(menuName = "Game/Seed Planting Quick Use Item", fileName = "SeedPlantingQuickUse")]
public class SeedPlantingQuickUseSO : QuickUseItemSO, IUsableItem
{
    [Header("Crop")]
    public CropConfigSO cropConfig;

    [Header("Messages")]
    public string activateMessage = "Planting mode ready";
    public string deactivateMessage = "Planting mode cancelled";

    bool IUsableItem.Use(UseContext context)
    {
        return Use(context);
    }

    public new bool Use(UseContext context)
    {
        var controller = ResolveController(context);
        if (controller == null)
        {
            context.pushMessage?.Invoke("No seed planting controller");
            return false;
        }

        if (cropConfig == null)
        {
            context.pushMessage?.Invoke("No crop config assigned");
            return false;
        }

        int required = cropConfig.GetResolvedSeedCost(consumeAmount);

        if (!controller.IsActiveWith(this))
        {
            if (context.inventory == null)
            {
                context.pushMessage?.Invoke("No inventory");
                return false;
            }

            if (required > 0 && !context.inventory.CanSpend(resourceType, required))
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

        return controller.TogglePlanting(this, context);
    }

    private PlayerSeedPlantingController ResolveController(UseContext context)
    {
        if (context.user != null)
        {
            var c = context.user.GetComponentInParent<PlayerSeedPlantingController>();
            if (c != null) return c;
        }

        return Object.FindFirstObjectByType<PlayerSeedPlantingController>(FindObjectsInactive.Include);
    }
}
