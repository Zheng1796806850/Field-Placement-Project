using System;
using UnityEngine;

public class PlayerHungerThirst : MonoBehaviour
{
    public enum DecayMode
    {
        RealTime = 0,   // decay every second
        PhaseTick = 1   // decay on DayStart / NightStart
    }

    [Header("Refs")]
    public PlayerMovementController movement;
    public PlayerCombat2D combat;
    public Health health;
    public PlayerResourceInventory inventory;

    [Header("Mode")]
    public DecayMode decayMode = DecayMode.RealTime;

    [Tooltip("If true, uses unscaled time when in RealTime decay (pause won't advance if your pause uses timeScale).")]
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
    [Tooltip("Applied when GameStateManager.OnDayStarted fires (i.e., Night ended).")]
    [Min(0f)] public float hungerDecay_OnDayStarted = 8f;
    [Min(0f)] public float thirstDecay_OnDayStarted = 12f;

    [Tooltip("Applied when GameStateManager.OnNightStarted fires (i.e., Day ended).")]
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

    [Tooltip("HP drain rate per second when hunger==0 OR thirst==0 (RealTime only).")]
    [Min(0f)] public float hpDrainPerSec = 0.5f;

    [Tooltip("HP drain amount per phase tick when hunger==0 OR thirst==0 (PhaseTick only).")]
    [Min(0)] public int hpDrainPerPhaseTick = 1;

    [Header("Consumables (Inventory)")]
    public KeyCode consumeFoodKey = KeyCode.Alpha1;
    public KeyCode consumeWaterKey = KeyCode.Alpha2;

    [Min(1)] public int foodCostPerUse = 1;
    [Min(1)] public int waterCostPerUse = 1;

    [Min(0f)] public float hungerRestorePerFood = 25f;
    [Min(0f)] public float thirstRestorePerWater = 25f;

    [Header("Debug")]
    public bool debugLogs = false;

    [SerializeField] private float hunger;
    [SerializeField] private float thirst;

    public event Action<float, float> OnHungerChanged;
    public event Action<float, float> OnThirstChanged;
    public event Action<float, float> OnDebuffChanged; 

    private float _baseMoveSpeed;
    private AttackHitbox[] _hitboxes;
    private int[] _hitboxBaseDamage;

    private GameStateManager _gsm;
    private bool _subscribed;
    private float _retryAt;

    private float _hpDrainBuffer;

    public float Hunger => hunger;
    public float Thirst => thirst;

    public float Hunger01 => Mathf.Clamp01(hunger / Mathf.Max(1f, hungerMax));
    public float Thirst01 => Mathf.Clamp01(thirst / Mathf.Max(1f, thirstMax));

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

        BroadcastAll();
        ApplyDebuffs();
    }

    private void OnEnable()
    {
        EnsureSubscribedIfNeeded();
    }

    private void Start()
    {
        EnsureSubscribedIfNeeded();
    }

    private void Update()
    {
        // hotkeys for consumables
        if (Input.GetKeyDown(consumeFoodKey)) TryConsumeFood();
        if (Input.GetKeyDown(consumeWaterKey)) TryConsumeWater();

        // RealTime decay
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

        // PhaseTick subscription retry (if GSM created later)
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
            var list = new System.Collections.Generic.List<AttackHitbox>(8);

            TryAddHitbox(combat.attackUp, list);
            TryAddHitbox(combat.attackDown, list);
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

    private void TryAddHitbox(Collider2D col, System.Collections.Generic.List<AttackHitbox> list)
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


    public bool TryConsumeFood()
    {
        if (inventory == null) inventory = PlayerResourceInventory.Instance;
        if (inventory == null) return false;

        if (!inventory.Spend(ResourceType.Food, foodCostPerUse))
            return false;

        RestoreHunger(hungerRestorePerFood);
        inventory.SaveInMemory();

        if (debugLogs)
            Debug.Log($"[HungerThirst] Consumed Food x{foodCostPerUse} (+Hunger {hungerRestorePerFood})");

        return true;
    }

    public bool TryConsumeWater()
    {
        if (inventory == null) inventory = PlayerResourceInventory.Instance;
        if (inventory == null) return false;

        if (!inventory.Spend(ResourceType.Water, waterCostPerUse))
            return false;

        RestoreThirst(thirstRestorePerWater);
        inventory.SaveInMemory();

        if (debugLogs)
            Debug.Log($"[HungerThirst] Consumed Water x{waterCostPerUse} (+Thirst {thirstRestorePerWater})");

        return true;
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
}
