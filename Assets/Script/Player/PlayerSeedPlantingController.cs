using UnityEngine;

public class PlayerSeedPlantingController : MonoBehaviour
{
    [Header("Refs")]
    public PlayerCombat2D combat;
    public PlayerInteractor2D interactor;
    public PlayerResourceInventory inventory;

    [Header("Behavior")]
    public bool disableCombatInputWhilePlanting = true;
    public bool allowCancelKey = true;
    public KeyCode cancelPlantingKey = KeyCode.Escape;
    public bool stayInPlantingModeAfterPlant = true;

    [Header("Debug")]
    public bool debugLogs = false;

    private SeedPlantingQuickUseSO _activeConfig;
    private UseContext _context;
    private bool _combatBlockApplied;

    public bool IsPlantingModeActive => _activeConfig != null;
    public bool IsActiveWith(SeedPlantingQuickUseSO config) => IsPlantingModeActive && _activeConfig == config;

    private void Awake()
    {
        ResolveRefs(gameObject);
    }

    private void OnDisable()
    {
        CancelPlanting(false, null);
    }

    private void Update()
    {
        if (!IsPlantingModeActive) return;

        ResolveRefs(_context.user != null ? _context.user : gameObject);

        if (allowCancelKey && cancelPlantingKey != KeyCode.None && Input.GetKeyDown(cancelPlantingKey))
            CancelPlanting(true, string.IsNullOrWhiteSpace(_activeConfig.deactivateMessage) ? "Planting mode cancelled" : _activeConfig.deactivateMessage);
    }

    public bool TogglePlanting(SeedPlantingQuickUseSO config, UseContext context)
    {
        if (config == null) return false;

        if (IsActiveWith(config))
        {
            CancelPlanting(true, string.IsNullOrWhiteSpace(config.deactivateMessage) ? "Planting mode cancelled" : config.deactivateMessage);
            return true;
        }

        if (IsPlantingModeActive)
            CancelPlanting(false, null);

        _context = context;
        ResolveRefs(context.user != null ? context.user : gameObject);

        if (inventory == null)
        {
            PushMessage("No inventory");
            return false;
        }

        if (config.cropConfig == null)
        {
            PushMessage("No crop config assigned");
            return false;
        }

        _activeConfig = config;
        ApplyCombatBlock(true);

        string message = !string.IsNullOrWhiteSpace(config.activateMessage)
            ? config.activateMessage
            : $"{config.cropConfig.displayName} planting ready";
        PushMessage(message);

        if (debugLogs)
            Debug.Log($"[SeedPlanting] Activated {config.name}");

        return true;
    }

    public void CancelPlanting(bool pushMessage, string message)
    {
        ApplyCombatBlock(false);
        _activeConfig = null;

        if (pushMessage && !string.IsNullOrWhiteSpace(message))
            PushMessage(message);
    }

    public bool TryGetSelectedCrop(out CropConfigSO crop)
    {
        if (_activeConfig != null && _activeConfig.cropConfig != null)
        {
            crop = _activeConfig.cropConfig;
            return true;
        }

        crop = null;
        return false;
    }

    public CropConfigSO GetSelectedCrop()
    {
        return _activeConfig != null ? _activeConfig.cropConfig : null;
    }

    public ResourceType GetSelectedSeedResource()
    {
        if (_activeConfig != null)
            return _activeConfig.resourceType;

        return ResourceType.Seeds;
    }

    public int GetSelectedSeedCost()
    {
        if (_activeConfig == null || _activeConfig.cropConfig == null)
            return 0;

        return _activeConfig.cropConfig.GetResolvedSeedCost(_activeConfig.consumeAmount);
    }

    public void NotifyPlantCompleted(PlayerResourceInventory inv)
    {
        if (_activeConfig == null || _activeConfig.cropConfig == null)
            return;

        if (!stayInPlantingModeAfterPlant)
        {
            CancelPlanting(false, null);
            return;
        }

        var targetInventory = inv != null ? inv : inventory;
        if (targetInventory == null)
        {
            CancelPlanting(false, null);
            return;
        }

        int required = _activeConfig.cropConfig.GetResolvedSeedCost(_activeConfig.consumeAmount);
        if (required > 0 && !targetInventory.CanSpend(_activeConfig.resourceType, required))
        {
            string itemName = !string.IsNullOrWhiteSpace(_activeConfig.cropConfig.displayName) ? _activeConfig.cropConfig.displayName : _activeConfig.name;
            CancelPlanting(true, $"{itemName} seeds depleted");
        }
    }

    private void ResolveRefs(GameObject user)
    {
        if (combat == null)
            combat = user != null ? user.GetComponentInParent<PlayerCombat2D>() : GetComponentInParent<PlayerCombat2D>();

        if (interactor == null)
            interactor = user != null ? user.GetComponentInParent<PlayerInteractor2D>() : GetComponentInParent<PlayerInteractor2D>();

        if (inventory == null)
            inventory = user != null ? user.GetComponentInParent<PlayerResourceInventory>() : GetComponentInParent<PlayerResourceInventory>();

        if (inventory == null)
            inventory = PlayerResourceInventory.Instance;
    }

    private void ApplyCombatBlock(bool active)
    {
        if (!disableCombatInputWhilePlanting || combat == null) return;

        if (active)
        {
            if (_combatBlockApplied) return;
            combat.PushExternalInputBlock();
            _combatBlockApplied = true;
        }
        else
        {
            if (!_combatBlockApplied) return;
            combat.PopExternalInputBlock();
            _combatBlockApplied = false;
        }
    }

    private void PushMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        if (_context.pushMessage != null)
        {
            _context.pushMessage(message);
            return;
        }

        if (inventory != null)
            inventory.PushMessage(message);
    }
}
