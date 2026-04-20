using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class ResourceDrop2D : MonoBehaviour, IInteractable
{
    [Header("Reward")]
    public ResourceType resourceType = ResourceType.Planks;
    [Min(1)] public int amount = 1;

    [Header("Visual")]
    public BackpackRulesSO rulesOverride;
    public bool usePlayerInventoryRules = true;
    public SpriteRenderer iconRenderer;
    public bool searchIconRendererInChildren = true;
    public bool preserveExistingSpriteAsFallback = true;

    [Header("Pickup")]
    public bool requireInteractKey = false;
    public string playerTag = "Player";
    [Min(0.01f)] public float pickupDistance = 0.2f;
    [Min(0f)] public float lifetimeSeconds = 120f;

    [Header("Magnet (Attraction)")]
    public bool allowMagnet = true;
    [Min(0f)] public float defaultMagnetSpeed = 7f;
    [Min(0f)] public float magnetAcceleration = 0f;

    [Header("Interactable")]
    public int interactPriority = 1;

    [Header("Cooldown")]
    [Min(0f)] public float retryCooldownSeconds = 0.35f;

    [Header("Spawn Grace")]
    [Tooltip("Block pickup/magnet for a short time right after spawn to avoid immediate self-pickup.")]
    [Min(0f)] public float spawnPickupGraceSeconds = 0.35f;

    private Rigidbody2D _rb;
    private Collider2D _col;
    private Sprite _fallbackSprite;

    private Transform _attractTarget;
    private bool _attracting;
    private float _targetSpeed;
    private float _currentSpeed;

    private bool _picked;
    private bool _magnetSfxPlayed;

    private float _pickupBlockedUntil;
    private float _magnetBlockedUntil;

    private void Reset()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        ResolveIconRenderer();
        CacheFallbackSprite();

        if (_col != null) _col.isTrigger = true;

        if (_rb != null)
        {
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;
        }

        RefreshVisual();
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        ResolveIconRenderer();
        CacheFallbackSprite();

        if (_col != null) _col.isTrigger = true;

        if (_rb != null)
        {
            if (_rb.bodyType != RigidbodyType2D.Kinematic)
                _rb.bodyType = RigidbodyType2D.Kinematic;

            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.zero;
        }

        RefreshVisual();

        // Prevent instant pickup when spawned inside/near player trigger.
        float now = Time.time;
        float grace = Mathf.Max(0f, spawnPickupGraceSeconds);
        if (grace > 0f)
        {
            _pickupBlockedUntil = Mathf.Max(_pickupBlockedUntil, now + grace);
            _magnetBlockedUntil = Mathf.Max(_magnetBlockedUntil, now + grace);
        }

        if (lifetimeSeconds > 0f)
            Destroy(gameObject, lifetimeSeconds);
    }

    private void Start()
    {
        RefreshVisual();
    }

    private void FixedUpdate()
    {
        if (_picked) return;
        if (!_attracting || _attractTarget == null) return;

        var inv = PlayerResourceInventory.Instance;
        if (inv != null && !inv.CanAcceptAny(resourceType, amount))
        {
            CancelAttract();
            _magnetBlockedUntil = Time.time + retryCooldownSeconds;
            return;
        }

        Vector2 current = _rb.position;
        Vector2 target = (Vector2)_attractTarget.position;

        if (magnetAcceleration > 0f)
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, magnetAcceleration * Time.fixedDeltaTime);
        else
            _currentSpeed = _targetSpeed;

        Vector2 next = Vector2.MoveTowards(current, target, _currentSpeed * Time.fixedDeltaTime);
        _rb.MovePosition(next);

        if (Vector2.Distance(next, target) <= pickupDistance)
            TryPickup(_attractTarget.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_picked) return;
        if (other == null) return;

        if (!requireInteractKey && other.CompareTag(playerTag))
            TryPickup(other.gameObject);
    }

    public void BeginAttract(Transform target, float speed)
    {
        if (!allowMagnet) return;
        if (_picked) return;
        if (target == null) return;
        if (Time.time < _magnetBlockedUntil) return;

        var inv = PlayerResourceInventory.Instance;
        if (inv != null && !inv.CanAcceptAny(resourceType, amount))
        {
            CancelAttract();
            _magnetBlockedUntil = Time.time + retryCooldownSeconds;
            return;
        }

        bool firstStart = !_attracting;

        _attractTarget = target;
        _attracting = true;

        _targetSpeed = (speed > 0f) ? speed : defaultMagnetSpeed;
        if (_targetSpeed <= 0f) _targetSpeed = 0.01f;

        if (_currentSpeed <= 0f) _currentSpeed = _targetSpeed;

        if (firstStart && !_magnetSfxPlayed)
        {
            _magnetSfxPlayed = true;
            SfxPlayer.TryPlay(SfxId.Economy_DropMagnet, transform.position);
        }
    }

    public void CancelAttract()
    {
        _attracting = false;
        _attractTarget = null;
    }

    public void Configure(ResourceType type, int amt)
    {
        resourceType = type;
        amount = Mathf.Max(1, amt);
        RefreshVisual();
    }

    private void TryPickup(GameObject interactor)
    {
        if (_picked) return;
        if (interactor == null) return;
        if (!interactor.CompareTag(playerTag)) return;
        if (Time.time < _pickupBlockedUntil) return;

        var inv = PlayerResourceInventory.Instance;
        if (inv == null)
            inv = FindFirstObjectByType<PlayerResourceInventory>();

        if (inv == null)
            return;

        int accepted;
        int rejected;

        bool ok = inv.TryAdd(resourceType, amount, transform.position, out accepted, out rejected, true);

        if (accepted > 0)
        {
            if (resourceType == ResourceType.Planks)
                SfxPlayer.TryPlay(SfxId.Economy_PlankPickup, transform.position);
            else
                SfxPlayer.TryPlay(SfxId.Economy_DropPickup, transform.position);
        }

        if (ok || rejected <= 0)
        {
            _picked = true;
            Destroy(gameObject);
            return;
        }

        amount = Mathf.Max(1, rejected);
        RefreshVisual();

        CancelAttract();
        _pickupBlockedUntil = Time.time + retryCooldownSeconds;
        _magnetBlockedUntil = Time.time + retryCooldownSeconds;
    }

    public string GetPrompt()
    {
        var rules = ResolveRules();
        string displayName = rules != null ? rules.GetDisplayName(resourceType) : resourceType.ToString();
        return $"Pick up {displayName} x{amount}";
    }

    public bool CanInteract(GameObject interactor) => !_picked && interactor != null && interactor.CompareTag(playerTag);
    public void Interact(GameObject interactor) => TryPickup(interactor);
    public int Priority => interactPriority;

    private void ResolveIconRenderer()
    {
        if (iconRenderer == null)
            iconRenderer = GetComponent<SpriteRenderer>();

        if (iconRenderer == null && searchIconRendererInChildren)
            iconRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void CacheFallbackSprite()
    {
        if (!preserveExistingSpriteAsFallback) return;
        if (iconRenderer == null) return;
        if (_fallbackSprite != null) return;
        _fallbackSprite = iconRenderer.sprite;
    }

    private BackpackRulesSO ResolveRules()
    {
        if (rulesOverride != null)
            return rulesOverride;

        if (usePlayerInventoryRules)
        {
            var inv = PlayerResourceInventory.Instance;
            if (inv == null)
                inv = FindFirstObjectByType<PlayerResourceInventory>();

            if (inv != null && inv.rules != null)
                return inv.rules;
        }

        return null;
    }

    private void RefreshVisual()
    {
        ResolveIconRenderer();
        CacheFallbackSprite();

        if (iconRenderer == null)
            return;

        Sprite sprite = null;
        var rules = ResolveRules();
        if (rules != null)
            sprite = rules.GetIcon(resourceType);

        if (sprite != null)
            iconRenderer.sprite = sprite;
        else if (_fallbackSprite != null)
            iconRenderer.sprite = _fallbackSprite;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveIconRenderer();
        CacheFallbackSprite();
        if (!Application.isPlaying)
            RefreshVisual();
    }
#endif
}
