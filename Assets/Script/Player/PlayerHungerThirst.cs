using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class PlayerHungerThirst : MonoBehaviour
{
    public enum DecayMode
    {
        RealTime = 0,
        PhaseTick = 1
    }

    [Serializable]
    public class QuickSlot
    {
        public bool useResourceBinding;
        public ResourceType boundResourceType;
        /// <summary>-1 = not bound to a backpack cell; use aggregate Get(type) / Spend.</summary>
        public int boundBackpackSlotIndex = -1;
        public QuickUseItemSO item;
    }

    [Header("Refs")]
    public PlayerMovementController movement;
    public PlayerCombat2D combat;
    public Health health;
    public PlayerResourceInventory inventory;

    [Header("Mode")]
    public DecayMode decayMode = DecayMode.RealTime;
    public bool useUnscaledTime = false;

    [Header("Max Values")]
    [Min(1f)] public float hungerMax = 100f;
    [Min(1f)] public float thirstMax = 100f;

    [Header("Start Values")]
    public float hungerStart = 100f;
    public float thirstStart = 100f;

    [Header("RealTime Decay (per second)")]
    [Min(0f)] public float hungerDecayPerSec_Day = 0.25f;
    [Min(0f)] public float hungerDecayPerSec_Night = 0.15f;
    [Min(0f)] public float thirstDecayPerSec_Day = 0.35f;
    [Min(0f)] public float thirstDecayPerSec_Night = 0.20f;

    [Header("PhaseTick Decay (per phase start)")]
    [Min(0f)] public float hungerDecay_OnDayStarted = 8f;
    [Min(0f)] public float thirstDecay_OnDayStarted = 12f;
    [Min(0f)] public float hungerDecay_OnNightStarted = 10f;
    [Min(0f)] public float thirstDecay_OnNightStarted = 14f;

    [Header("Debuff Thresholds (0..1 = % of max)")]
    [Range(0f, 1f)] public float hungerLowThreshold = 0.25f;
    [Range(0f, 1f)] public float thirstLowThreshold = 0.25f;

    [Header("Debuff Multipliers When Low")]
    [Range(0.05f, 1f)] public float hungerMoveMultiplier = 0.85f;
    [Range(0.05f, 1f)] public float hungerAttackMultiplier = 0.90f;
    [Range(0.05f, 1f)] public float thirstMoveMultiplier = 0.80f;
    [Range(0.05f, 1f)] public float thirstAttackMultiplier = 0.85f;

    [Header("Optional HP Drain at 0")]
    public bool enableHpDrainAtZero = true;
    [Min(0f)] public float hpDrainPerSec = 0.5f;
    [Min(0)] public int hpDrainPerPhaseTick = 1;

    [Header("Quick Slots")]
    public bool enableQuickSlotsInput = true;
    public bool ignoreQuickSlotsWhenPaused = true;
    public bool saveInventoryAfterQuickUse = true;
    public bool loadQuickSlotKeysFromPlayerPrefs = false;
    public string quickSlotPrefsKeyPrefix = "QuickSlotKey_";
    public List<QuickSlot> quickSlots = new List<QuickSlot> { new QuickSlot(), new QuickSlot(), new QuickSlot(), new QuickSlot() };
    public List<KeyCode> quickSlotKeys = new List<KeyCode> { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

    [Header("Quick Slots Feedback")]
    public bool showCooldownMessage = true;
    public string cooldownMessage = "On cooldown";
    public bool showEmptySlotMessage = false;
    public string emptySlotMessage = "Empty slot";

    [Header("Debug")]
    public bool debugLogs = false;

    [SerializeField] private float hunger;
    [SerializeField] private float thirst;

    public event Action<float, float> OnHungerChanged;
    public event Action<float, float> OnThirstChanged;
    public event Action<float, float> OnDebuffChanged;

    public event Action OnQuickSlotsLayoutChanged;
    public event Action<int> OnQuickSlotUsed;
    public event Action<int> OnQuickSlotSelectionChanged;

    private float _baseMoveSpeed;
    private AttackHitbox[] _hitboxes;
    private int[] _hitboxBaseDamage;

    private GameStateManager _gsm;
    private bool _subscribed;
    private float _retryAt;

    private float _hpDrainBuffer;

    private readonly List<float> _quickSlotCooldownRemaining = new List<float>();

    public float Hunger => hunger;
    public float Thirst => thirst;

    public float Hunger01 => Mathf.Clamp01(hunger / Mathf.Max(1f, hungerMax));
    public float Thirst01 => Mathf.Clamp01(thirst / Mathf.Max(1f, thirstMax));

    public int QuickSlotsSelectedIndex { get; private set; } = -1;
    public int QuickSlotCount => quickSlots != null ? quickSlots.Count : 0;

    private void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovementController>();
        if (combat == null) combat = GetComponent<PlayerCombat2D>();
        if (health == null) health = GetComponent<Health>();
        if (inventory == null) inventory = PlayerResourceInventory.Instance;

        if (movement != null) _baseMoveSpeed = movement.speed;

        CacheAttackHitboxes();

        hunger = Mathf.Clamp(hungerStart, 0f, hungerMax);
        thirst = Mathf.Clamp(thirstStart, 0f, thirstMax);

        EnsureQuickSlotKeysSize();
        EnsureQuickSlotCooldownSize();
        if (loadQuickSlotKeysFromPlayerPrefs) LoadQuickSlotKeybinds();

        BroadcastAll();
        ApplyDebuffs();
    }

    private void OnEnable()
    {
        EnsureSubscribedIfNeeded();
        EnsureInventoryHook();
        OnQuickSlotsLayoutChanged?.Invoke();
    }

    private void Start()
    {
        EnsureSubscribedIfNeeded();
        EnsureInventoryHook();
    }

    private void Update()
    {
        HandleQuickSlotsInputAndCooldown();

        if (decayMode == DecayMode.RealTime)
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null && gsm.IsPaused) return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            bool isNight = (gsm != null && gsm.CurrentPhase == DayNightPhase.Night);

            float hRate = isNight ? hungerDecayPerSec_Night : hungerDecayPerSec_Day;
            float tRate = isNight ? thirstDecayPerSec_Night : thirstDecayPerSec_Day;

            if (hRate > 0f) SetHunger(hunger - hRate * dt);
            if (tRate > 0f) SetThirst(thirst - tRate * dt);

            if (enableHpDrainAtZero)
                HandleHpDrain_RealTime(dt);
        }

        if (decayMode == DecayMode.PhaseTick && !_subscribed)
        {
            if (Time.unscaledTime >= _retryAt)
            {
                _retryAt = Time.unscaledTime + 0.5f;
                EnsureSubscribedIfNeeded();
            }
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        RemoveInventoryHook();
    }

    private void EnsureSubscribedIfNeeded()
    {
        if (decayMode != DecayMode.PhaseTick) return;
        if (_subscribed) return;

        _gsm = GameStateManager.Instance != null ? GameStateManager.Instance : FindFirstObjectByType<GameStateManager>();
        if (_gsm == null) return;

        _gsm.OnDayStarted += OnDayStarted;
        _gsm.OnNightStarted += OnNightStarted;
        _subscribed = true;

        if (debugLogs)
            Debug.Log($"[HungerThirst] Subscribed to GSM: {_gsm.name}");
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;

        if (_gsm != null)
        {
            _gsm.OnDayStarted -= OnDayStarted;
            _gsm.OnNightStarted -= OnNightStarted;
        }
        _subscribed = false;
        _gsm = null;
    }

    private void OnDayStarted()
    {
        SetHunger(hunger - hungerDecay_OnDayStarted);
        SetThirst(thirst - thirstDecay_OnDayStarted);

        if (enableHpDrainAtZero)
            HandleHpDrain_PhaseTick();
    }

    private void OnNightStarted()
    {
        SetHunger(hunger - hungerDecay_OnNightStarted);
        SetThirst(thirst - thirstDecay_OnNightStarted);

        if (enableHpDrainAtZero)
            HandleHpDrain_PhaseTick();
    }

    private void ApplyDebuffs()
    {
        float moveMult = 1f;
        float atkMult = 1f;

        bool hungerLow = Hunger01 <= hungerLowThreshold;
        bool thirstLow = Thirst01 <= thirstLowThreshold;

        if (hungerLow)
        {
            moveMult *= hungerMoveMultiplier;
            atkMult *= hungerAttackMultiplier;
        }

        if (thirstLow)
        {
            moveMult *= thirstMoveMultiplier;
            atkMult *= thirstAttackMultiplier;
        }

        if (movement != null)
        {
            movement.speed = _baseMoveSpeed * moveMult;
        }

        if (_hitboxes != null && _hitboxBaseDamage != null)
        {
            for (int i = 0; i < _hitboxes.Length; i++)
            {
                if (_hitboxes[i] == null) continue;

                int baseDmg = _hitboxBaseDamage[i];
                int next = Mathf.RoundToInt(baseDmg * atkMult);
                if (baseDmg > 0) next = Mathf.Max(1, next);

                _hitboxes[i].damage = next;
            }
        }

        OnDebuffChanged?.Invoke(moveMult, atkMult);
    }

    private void CacheAttackHitboxes()
    {
        if (combat == null)
        {
            _hitboxes = GetComponentsInChildren<AttackHitbox>(true);
        }
        else
        {
            var list = new List<AttackHitbox>(8);

            TryAddHitbox(combat.attackLeft, list);
            TryAddHitbox(combat.attackRight, list);

            if (list.Count == 0)
                list.AddRange(GetComponentsInChildren<AttackHitbox>(true));

            _hitboxes = list.ToArray();
        }

        _hitboxBaseDamage = new int[_hitboxes.Length];
        for (int i = 0; i < _hitboxes.Length; i++)
        {
            _hitboxBaseDamage[i] = _hitboxes[i] != null ? _hitboxes[i].damage : 0;
        }
    }

    private void TryAddHitbox(Collider2D col, List<AttackHitbox> list)
    {
        if (col == null) return;
        var hb = col.GetComponent<AttackHitbox>();
        if (hb != null && !list.Contains(hb)) list.Add(hb);
    }

    private void HandleHpDrain_RealTime(float dt)
    {
        if (health == null || health.dead) return;

        bool starving = hunger <= 0.0001f || thirst <= 0.0001f;
        if (!starving) { _hpDrainBuffer = 0f; return; }

        if (hpDrainPerSec <= 0f) return;

        _hpDrainBuffer += hpDrainPerSec * dt;
        int dmg = Mathf.FloorToInt(_hpDrainBuffer);
        if (dmg <= 0) return;

        _hpDrainBuffer -= dmg;
        health.TakeDamage(dmg);

        if (debugLogs)
            Debug.Log($"[HungerThirst] HP drain (RealTime) -{dmg} (starving={starving})");
    }

    private void HandleHpDrain_PhaseTick()
    {
        if (health == null || health.dead) return;

        bool starving = hunger <= 0.0001f || thirst <= 0.0001f;
        if (!starving) return;

        if (hpDrainPerPhaseTick <= 0) return;

        health.TakeDamage(hpDrainPerPhaseTick);

        if (debugLogs)
            Debug.Log($"[HungerThirst] HP drain (PhaseTick) -{hpDrainPerPhaseTick} (starving={starving})");
    }

    public void RestoreHunger(float amount)
    {
        if (amount <= 0f) return;
        SetHunger(hunger + amount);
    }

    public void RestoreThirst(float amount)
    {
        if (amount <= 0f) return;
        SetThirst(thirst + amount);
    }

    public void SetHunger(float value)
    {
        float next = Mathf.Clamp(value, 0f, hungerMax);
        if (Mathf.Approximately(next, hunger)) return;

        hunger = next;
        OnHungerChanged?.Invoke(hunger, hungerMax);
        ApplyDebuffs();
    }

    public void SetThirst(float value)
    {
        float next = Mathf.Clamp(value, 0f, thirstMax);
        if (Mathf.Approximately(next, thirst)) return;

        thirst = next;
        OnThirstChanged?.Invoke(thirst, thirstMax);
        ApplyDebuffs();
    }

    private void BroadcastAll()
    {
        OnHungerChanged?.Invoke(hunger, hungerMax);
        OnThirstChanged?.Invoke(thirst, thirstMax);
    }

    private void HandleQuickSlotsInputAndCooldown()
    {
        EnsureQuickSlotKeysSize();
        EnsureQuickSlotCooldownSize();

        var gsm = GameStateManager.Instance;
        bool paused = gsm != null && gsm.IsPaused;

        if (!(ignoreQuickSlotsWhenPaused && paused))
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _quickSlotCooldownRemaining.Count; i++)
            {
                if (_quickSlotCooldownRemaining[i] > 0f)
                    _quickSlotCooldownRemaining[i] = Mathf.Max(0f, _quickSlotCooldownRemaining[i] - dt);
            }
        }

        if (!enableQuickSlotsInput) return;
        if (ignoreQuickSlotsWhenPaused && paused) return;

        int max = Mathf.Min(QuickSlotCount, quickSlotKeys.Count);
        for (int i = 0; i < max; i++)
        {
            if (Input.GetKeyDown(quickSlotKeys[i]))
                TryUseQuickSlot(i);
        }
    }

    private void EnsureInventoryHook()
    {
        if (inventory == null) inventory = PlayerResourceInventory.Instance;
        if (inventory == null) return;
        inventory.OnAnyResourceChanged -= HandleInventoryChanged;
        inventory.OnAnyResourceChanged += HandleInventoryChanged;
    }

    private void RemoveInventoryHook()
    {
        if (inventory == null) return;
        inventory.OnAnyResourceChanged -= HandleInventoryChanged;
    }

    private void HandleInventoryChanged()
    {
        OnQuickSlotsLayoutChanged?.Invoke();
    }

    private void PushInventoryMessage(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return;

        if (inventory == null) inventory = PlayerResourceInventory.Instance;
        if (inventory != null) inventory.PushMessage(msg);
        else if (debugLogs) Debug.Log(msg);
    }

    private bool LegacyTryUseQuickItem(QuickUseItemSO item)
    {
        if (item == null) return false;

        if (inventory == null) inventory = PlayerResourceInventory.Instance;
        if (inventory == null) return false;

        if (item.consumeAmount > 0)
        {
            if (!inventory.Spend(item.resourceType, item.consumeAmount))
            {
                PushInventoryMessage($"Not enough {item.resourceType}");
                return false;
            }
        }

        ApplyQuickUseEffects(item);

        if (!string.IsNullOrWhiteSpace(item.displayName))
            PushInventoryMessage($"Used {item.displayName}");
        else
            PushInventoryMessage("Used item");

        return true;
    }

    private void EnsureQuickSlotKeysSize()
    {
        if (quickSlots == null) quickSlots = new List<QuickSlot>();
        if (quickSlotKeys == null) quickSlotKeys = new List<KeyCode>();

        int target = quickSlots.Count;
        while (quickSlotKeys.Count < target) quickSlotKeys.Add(KeyCode.None);
        while (quickSlotKeys.Count > target) quickSlotKeys.RemoveAt(quickSlotKeys.Count - 1);

        for (int i = 0; i < quickSlotKeys.Count; i++)
        {
            if (quickSlotKeys[i] == KeyCode.None)
            {
                if (i == 0) quickSlotKeys[i] = KeyCode.Alpha1;
                else if (i == 1) quickSlotKeys[i] = KeyCode.Alpha2;
                else if (i == 2) quickSlotKeys[i] = KeyCode.Alpha3;
                else if (i == 3) quickSlotKeys[i] = KeyCode.Alpha4;
            }
        }
    }

    private void EnsureQuickSlotCooldownSize()
    {
        if (quickSlots == null) return;
        while (_quickSlotCooldownRemaining.Count < quickSlots.Count) _quickSlotCooldownRemaining.Add(0f);
        while (_quickSlotCooldownRemaining.Count > quickSlots.Count) _quickSlotCooldownRemaining.RemoveAt(_quickSlotCooldownRemaining.Count - 1);
    }

    public QuickUseItemSO GetQuickSlotItem(int index)
    {
        return ResolveQuickSlotItem(index);
    }

    public bool TryGetQuickSlotBoundResourceType(int index, out ResourceType type)
    {
        if (quickSlots == null || index < 0 || index >= quickSlots.Count)
        {
            type = default;
            return false;
        }

        var slot = quickSlots[index];
        if (slot == null || !slot.useResourceBinding)
        {
            type = default;
            return false;
        }

        type = slot.boundResourceType;
        return true;
    }

    public void BindQuickSlotResource(int index, ResourceType type, int backpackSlotIndex = -1)
    {
        if (quickSlots == null) return;
        if (index < 0 || index >= quickSlots.Count) return;
        if (quickSlots[index] == null) quickSlots[index] = new QuickSlot();

        // A single backpack slot can only be bound by one quick slot.
        // Rebinding the same backpack cell to a new quick slot clears previous owners.
        if (backpackSlotIndex >= 0)
        {
            for (int i = 0; i < quickSlots.Count; i++)
            {
                if (i == index) continue;
                var other = quickSlots[i];
                if (other == null || !other.useResourceBinding) continue;
                if (other.boundBackpackSlotIndex != backpackSlotIndex) continue;
                ClearQuickSlot(i);
            }
        }

        quickSlots[index].useResourceBinding = true;
        quickSlots[index].boundResourceType = type;
        quickSlots[index].boundBackpackSlotIndex = backpackSlotIndex;
        quickSlots[index].item = null;

        EnsureQuickSlotCooldownSize();
        OnQuickSlotsLayoutChanged?.Invoke();
    }

    public void SetQuickSlotItem(int index, QuickUseItemSO item)
    {
        if (quickSlots == null) return;
        if (index < 0 || index >= quickSlots.Count) return;
        if (quickSlots[index] == null) quickSlots[index] = new QuickSlot();

        quickSlots[index].useResourceBinding = false;
        quickSlots[index].item = item;
        quickSlots[index].boundBackpackSlotIndex = -1;

        EnsureQuickSlotCooldownSize();
        OnQuickSlotsLayoutChanged?.Invoke();
    }

    public Sprite GetQuickSlotIcon(int index)
    {
        if (inventory == null) inventory = PlayerResourceInventory.Instance;

        if (TryGetQuickSlotBoundResourceType(index, out var boundType))
        {
            if (inventory != null && inventory.rules != null)
            {
                Sprite icon = inventory.rules.GetIcon(boundType);
                if (icon != null) return icon;
            }
        }

        var item = ResolveQuickSlotItem(index);
        return item != null ? item.icon : null;
    }

    public string GetQuickSlotDisplayName(int index)
    {
        if (inventory == null) inventory = PlayerResourceInventory.Instance;

        if (TryGetQuickSlotBoundResourceType(index, out var boundType))
        {
            if (inventory != null && inventory.rules != null)
                return inventory.rules.GetDisplayName(boundType);

            return boundType.ToString();
        }

        var item = ResolveQuickSlotItem(index);
        if (item == null) return string.Empty;
        return string.IsNullOrWhiteSpace(item.displayName) ? item.name : item.displayName;
    }

    public float GetQuickSlotCooldownRemaining(int index)
    {
        if (index < 0 || index >= _quickSlotCooldownRemaining.Count) return 0f;
        return _quickSlotCooldownRemaining[index];
    }

    public float GetQuickSlotCooldownDuration(int index)
    {
        var item = ResolveQuickSlotItem(index);
        return item != null ? Mathf.Max(0f, item.cooldownSeconds) : 0f;
    }

    public int GetQuickSlotAvailableCount(int index)
    {
        if (inventory == null) inventory = PlayerResourceInventory.Instance;
        if (inventory == null) return 0;

        if (TryGetQuickSlotBoundResourceType(index, out var boundType))
        {
            if (TryGetValidBoundBackpackSlotIndex(index, boundType, out int validBoundSlotIndex))
            {
                var cell = inventory.GetSlot(validBoundSlotIndex);
                return (!cell.IsEmpty && cell.type == boundType) ? Mathf.Max(0, cell.amount) : 0;
            }

            return Mathf.Max(0, inventory.Get(boundType));
        }

        var item = ResolveQuickSlotItem(index);
        if (item == null) return 0;
        return Mathf.Max(0, inventory.Get(item.resourceType));
    }

    public bool TryUseQuickSlot(int index)
    {
        SetQuickSlotSelectedIndex(index);

        EnsureQuickSlotCooldownSize();
        if (index < 0 || index >= _quickSlotCooldownRemaining.Count) return false;

        if (_quickSlotCooldownRemaining[index] > 0f)
        {
            if (showCooldownMessage)
                PushInventoryMessage(string.IsNullOrWhiteSpace(cooldownMessage) ? "On cooldown" : cooldownMessage);

            return false;
        }

        if (inventory == null) inventory = PlayerResourceInventory.Instance;
        if (inventory == null)
        {
            PushInventoryMessage("No inventory");
            return false;
        }

        var item = ResolveQuickSlotItem(index);
        if (item == null)
        {
            if (TryGetQuickSlotBoundResourceType(index, out var boundType))
            {
                string itemName = inventory.rules != null ? inventory.rules.GetDisplayName(boundType) : boundType.ToString();
                PushInventoryMessage($"No quick use configured for {itemName}");
            }
            else if (showEmptySlotMessage)
            {
                PushInventoryMessage(string.IsNullOrWhiteSpace(emptySlotMessage) ? "Empty slot" : emptySlotMessage);
            }

            return false;
        }

        bool used = false;

        if (item is IUsableItem usable)
        {
            UseContext ctx = new UseContext
            {
                user = gameObject,
                vitals = this,
                inventory = inventory,
                slotIndex = index,
                pushMessage = inventory.PushMessage
            };

            bool scoped = false;
            int scopedBackpackSlotIndex = -1;
            if (TryGetQuickSlotBoundResourceType(index, out var boundTypeForScope))
            {
                scoped = TryGetValidBoundBackpackSlotIndex(index, boundTypeForScope, out scopedBackpackSlotIndex);
            }

            if (scoped)
                inventory.BeginQuickUseBackpackSlotScope(scopedBackpackSlotIndex);

            try
            {
                used = usable.Use(ctx);
            }
            finally
            {
                if (scoped)
                    inventory.EndQuickUseBackpackSlotScope();
            }
        }
        else
        {
            used = LegacyTryUseQuickItem(item);
        }

        if (!used) return false;

        if (saveInventoryAfterQuickUse)
            inventory.SaveInMemory();

        _quickSlotCooldownRemaining[index] = Mathf.Max(0f, item.cooldownSeconds);
        OnQuickSlotUsed?.Invoke(index);
        OnQuickSlotsLayoutChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// Called by backpack UI after slot drag/swap/merge so quick-slot bindings can follow moved items.
    /// </summary>
    public void NotifyBackpackSlotsReordered(int fromIndex, int toIndex)
    {
        if (quickSlots == null || inventory == null)
            return;

        bool changed = false;
        for (int i = 0; i < quickSlots.Count; i++)
        {
            var qs = quickSlots[i];
            if (qs == null || !qs.useResourceBinding)
                continue;

            if (TryRefreshQuickSlotBindingIndex(i, qs.boundResourceType))
                changed = true;
        }

        if (changed)
            OnQuickSlotsLayoutChanged?.Invoke();
    }

    private bool TryGetValidBoundBackpackSlotIndex(int quickSlotIndex, ResourceType boundType, out int backpackSlotIndex)
    {
        backpackSlotIndex = -1;

        if (quickSlots == null || quickSlotIndex < 0 || quickSlotIndex >= quickSlots.Count)
            return false;

        var qs = quickSlots[quickSlotIndex];
        if (qs == null)
            return false;

        if (qs.boundBackpackSlotIndex < 0)
        {
            if (TryFindFirstBackpackSlotWithResource(boundType, out int recoveredSlot))
            {
                qs.boundBackpackSlotIndex = recoveredSlot;
                backpackSlotIndex = recoveredSlot;
                OnQuickSlotsLayoutChanged?.Invoke();
                return true;
            }

            ClearQuickSlot(quickSlotIndex);
            OnQuickSlotsLayoutChanged?.Invoke();
            return false;
        }

        int candidate = qs.boundBackpackSlotIndex;
        var cell = inventory != null ? inventory.GetSlot(candidate) : InventorySlot.Empty;
        if (!cell.IsEmpty && cell.type == boundType)
        {
            backpackSlotIndex = candidate;
            return true;
        }

        // Bound slot became stale after backpack reordering/swapping/consuming.
        // Try auto-rebind to another slot that still has this resource.
        if (TryFindFirstBackpackSlotWithResource(boundType, out int fallbackSlot))
        {
            qs.boundBackpackSlotIndex = fallbackSlot;
            backpackSlotIndex = fallbackSlot;
            OnQuickSlotsLayoutChanged?.Invoke();
            return true;
        }

        // No more such resource in backpack: clear quick slot completely.
        ClearQuickSlot(quickSlotIndex);
        OnQuickSlotsLayoutChanged?.Invoke();
        return false;
    }

    private bool TryRefreshQuickSlotBindingIndex(int quickSlotIndex, ResourceType boundType)
    {
        if (quickSlots == null || quickSlotIndex < 0 || quickSlotIndex >= quickSlots.Count)
            return false;
        if (inventory == null)
            return false;

        var qs = quickSlots[quickSlotIndex];
        if (qs == null || !qs.useResourceBinding)
            return false;

        int oldIndex = qs.boundBackpackSlotIndex;
        if (oldIndex >= 0)
        {
            var cell = inventory.GetSlot(oldIndex);
            if (!cell.IsEmpty && cell.type == boundType)
                return false;
        }

        if (TryFindFirstBackpackSlotWithResource(boundType, out int newIndex))
        {
            qs.boundBackpackSlotIndex = newIndex;
            return newIndex != oldIndex;
        }

        ClearQuickSlot(quickSlotIndex);
        return true;
    }

    private bool TryFindFirstBackpackSlotWithResource(ResourceType type, out int slotIndex)
    {
        slotIndex = -1;
        if (inventory == null)
            return false;

        int max = inventory.MaxSlots;
        for (int i = 0; i < max; i++)
        {
            var cell = inventory.GetSlot(i);
            if (!cell.IsEmpty && cell.type == type && cell.amount > 0)
            {
                slotIndex = i;
                return true;
            }
        }

        return false;
    }

    private void ClearQuickSlot(int index)
    {
        if (quickSlots == null || index < 0 || index >= quickSlots.Count)
            return;
        if (quickSlots[index] == null)
            quickSlots[index] = new QuickSlot();

        quickSlots[index].useResourceBinding = false;
        quickSlots[index].boundBackpackSlotIndex = -1;
        quickSlots[index].item = null;
    }

    private QuickUseItemSO ResolveQuickSlotItem(int index)
    {
        if (quickSlots == null) return null;
        if (index < 0 || index >= quickSlots.Count) return null;

        var slot = quickSlots[index];
        if (slot == null) return null;

        if (slot.useResourceBinding)
        {
            if (inventory == null) inventory = PlayerResourceInventory.Instance;
            if (inventory == null || inventory.rules == null) return null;
            return inventory.rules.GetQuickUseItem(slot.boundResourceType);
        }

        return slot.item;
    }

    private void SetQuickSlotSelectedIndex(int index)
    {
        if (index < -1 || index >= QuickSlotCount) return;
        if (QuickSlotsSelectedIndex == index) return;
        QuickSlotsSelectedIndex = index;
        OnQuickSlotSelectionChanged?.Invoke(index);
    }

    private void ApplyQuickUseEffects(QuickUseItemSO item)
    {
        if (item.addHunger != 0f) RestoreHunger(item.addHunger);
        if (item.addThirst != 0f) RestoreThirst(item.addThirst);

        if (item.addHP != 0)
        {
            if (health == null) health = GetComponent<Health>();
            if (health != null) ApplyHealthDelta(health, item.addHP);
        }
    }

    private static void ApplyHealthDelta(Health target, int delta)
    {
        if (target == null) return;

        if (delta < 0)
        {
            target.TakeDamage(-delta);
            return;
        }

        Type t = target.GetType();

        var mi = t.GetMethod("Heal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
        if (mi != null) { mi.Invoke(target, new object[] { delta }); return; }

        mi = t.GetMethod("Heal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);
        if (mi != null) { mi.Invoke(target, new object[] { (float)delta }); return; }

        string[] props = { "currentHP", "hp", "CurrentHP" };
        foreach (var pName in props)
        {
            var p = t.GetProperty(pName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanRead && p.CanWrite && p.PropertyType == typeof(int))
            {
                int cur = (int)p.GetValue(target);
                p.SetValue(target, cur + delta);
                return;
            }

            var f = t.GetField(pName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(int))
            {
                int cur = (int)f.GetValue(target);
                f.SetValue(target, cur + delta);
                return;
            }
        }
    }

    public void SaveQuickSlotKeybinds()
    {
        if (string.IsNullOrWhiteSpace(quickSlotPrefsKeyPrefix)) return;
        EnsureQuickSlotKeysSize();
        for (int i = 0; i < quickSlotKeys.Count; i++)
            PlayerPrefs.SetString(quickSlotPrefsKeyPrefix + i, quickSlotKeys[i].ToString());
        PlayerPrefs.Save();
    }

    public void LoadQuickSlotKeybinds()
    {
        if (string.IsNullOrWhiteSpace(quickSlotPrefsKeyPrefix)) return;
        EnsureQuickSlotKeysSize();
        for (int i = 0; i < quickSlotKeys.Count; i++)
        {
            string k = quickSlotPrefsKeyPrefix + i;
            if (!PlayerPrefs.HasKey(k)) continue;
            string v = PlayerPrefs.GetString(k);
            if (Enum.TryParse(v, out KeyCode parsed)) quickSlotKeys[i] = parsed;
        }
    }
}