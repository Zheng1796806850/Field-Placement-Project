using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class ResourceDrop2D : MonoBehaviour, IInteractable
{
    [Header("Reward")]
    public ResourceType resourceType = ResourceType.Planks;
    [Min(1)] public int amount = 1;

    [Header("Pickup")]
    [Tooltip("If true, pickup only happens when Interact() is called (e.g., Press E). If false, touching the player auto-picks.")]
    public bool requireInteractKey = false;

    [Tooltip("Player tag used for auto-pickup and CanInteract checks.")]
    public string playerTag = "Player";

    [Tooltip("Distance threshold to auto-pick once attracted to the player.")]
    [Min(0.01f)] public float pickupDistance = 0.2f;

    [Tooltip("Destroy the drop after this many seconds. 0 = never.")]
    [Min(0f)] public float lifetimeSeconds = 120f;

    [Header("Magnet (Attraction)")]
    public bool allowMagnet = true;

    [Tooltip("If PlayerPickupMagnet2D provides a speed, it overrides this.")]
    [Min(0f)] public float defaultMagnetSpeed = 7f;

    [Tooltip("0 = constant speed. >0 = accelerate towards target speed.")]
    [Min(0f)] public float magnetAcceleration = 0f;

    [Header("Interactable")]
    [Tooltip("Higher means PlayerInteractor2D prioritizes this more.")]
    public int interactPriority = 1;

    private Rigidbody2D _rb;
    private Collider2D _col;

    private Transform _attractTarget;
    private bool _attracting;
    private float _targetSpeed;
    private float _currentSpeed;

    private bool _picked;

    private void Reset()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();

        if (_col != null) _col.isTrigger = true;

        if (_rb != null)
        {
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.gravityScale = 0f;
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();

        if (_col != null) _col.isTrigger = true;

        if (_rb != null)
        {
            if (_rb.bodyType != RigidbodyType2D.Kinematic)
                _rb.bodyType = RigidbodyType2D.Kinematic;

            _rb.gravityScale = 0f;
            _rb.linearVelocity = Vector2.zero;
        }

        if (lifetimeSeconds > 0f)
            Destroy(gameObject, lifetimeSeconds);
    }

    private void FixedUpdate()
    {
        if (_picked) return;

        if (!_attracting || _attractTarget == null) return;

        Vector2 current = _rb.position;
        Vector2 target = (Vector2)_attractTarget.position;

        if (magnetAcceleration > 0f)
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, magnetAcceleration * Time.fixedDeltaTime);
        }
        else
        {
            _currentSpeed = _targetSpeed;
        }

        Vector2 next = Vector2.MoveTowards(current, target, _currentSpeed * Time.fixedDeltaTime);
        _rb.MovePosition(next);

        if (Vector2.Distance(next, target) <= pickupDistance)
        {
            TryPickup(_attractTarget.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_picked) return;
        if (other == null) return;

        if (!requireInteractKey && other.CompareTag(playerTag))
        {
            TryPickup(other.gameObject);
        }
    }

    public void BeginAttract(Transform target, float speed)
    {
        if (!allowMagnet) return;
        if (_picked) return;
        if (target == null) return;

        _attractTarget = target;
        _attracting = true;

        _targetSpeed = (speed > 0f) ? speed : defaultMagnetSpeed;
        if (_targetSpeed <= 0f) _targetSpeed = 0.01f;

        if (_currentSpeed <= 0f) _currentSpeed = _targetSpeed;
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
    }

    private void TryPickup(GameObject interactor)
    {
        if (_picked) return;
        if (interactor == null) return;
        if (!interactor.CompareTag(playerTag)) return;

        var inv = PlayerResourceInventory.Instance;
        if (inv == null)
            inv = FindFirstObjectByType<PlayerResourceInventory>();

        if (inv != null)
        {
            inv.Add(resourceType, amount);
        }
        else
        {
            Debug.LogWarning($"[ResourceDrop2D] No PlayerResourceInventory found in scene. Drop not applied: {resourceType} x{amount}");
        }

        _picked = true;
        Destroy(gameObject);
    }

    public string GetPrompt() => $"Pick up {resourceType} x{amount}";
    public bool CanInteract(GameObject interactor) => !_picked && interactor != null && interactor.CompareTag(playerTag);
    public void Interact(GameObject interactor) => TryPickup(interactor);
    public int Priority => interactPriority;
}
