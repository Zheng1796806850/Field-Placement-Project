using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Seeker))]
public class EnemyAI2D : MonoBehaviour
{
    private enum State
    {
        MoveToHouse,
        Attack,
        Dead
    }

    [Header("Goal (House)")]
    [Tooltip("如果不填，会自动使用 HouseObjective.Instance。")]
    public Transform houseOverride;

    [Header("Sensor / Target Filter")]
    [Tooltip("攻击/检测范围 Trigger Collider（一般在子物体 AttackSensor 上）。")]
    public Collider2D sensorTrigger;

    [Tooltip("只把这些 Layer 视为可选目标（可留空表示不过滤）。")]
    public LayerMask targetLayers = ~0;

    [Tooltip("玩家 Tag")]
    public string playerTag = "Player";

    [Tooltip("墙 Tag")]
    public string wallTag = "Wall";

    [Tooltip("是否允许攻击玩家")]
    public bool canTargetPlayer = true;

    [Tooltip("范围内同时有玩家+墙：true=优先打玩家；false=优先打墙")]
    public bool preferPlayerOverWall = true;

    [Header("Movement")]
    [Min(0f)] public float moveSpeed = 2.0f;

    [Tooltip("多久重算一次路径（秒）。怪多时建议 0.5~1.0")]
    [Min(0.05f)] public float repathInterval = 0.5f;

    [Tooltip("距离路径点多近算到达")]
    [Min(0.01f)] public float nextWaypointDistance = 0.2f;

    [Tooltip("如果你是纯横向关卡，可以锁定Y轴移动（只走X）")]
    public bool lockYMovement = false;

    [Header("Attack - Damage")]
    [Min(1)] public int damageToWall = 5;
    [Min(1)] public int damageToPlayer = 5;

    [Header("Attack - Timing (seconds)")]
    [Tooltip("攻击冷却（两次攻击间隔）")]
    [Min(0.05f)] public float attackCooldown = 1.0f;

    [Tooltip("从触发攻击到造成伤害的延迟（风起时间）")]
    [Min(0f)] public float attackWindup = 0.25f;

    [Tooltip("若为 true：命中由动画事件 AnimEvent_DealDamage() 触发；否则用 attackWindup 计时")]
    public bool useAnimationEventForHit = false;

    [Header("Animation (Optional)")]
    public Animator animator;
    public string animSpeedParam = "Speed";
    public string animAttackTrigger = "Attack";

    [Header("Debug")]
    public bool logStateChanges = false;
    public bool drawDebugPath = false;

    private Rigidbody2D _rb;
    private Seeker _seeker;

    private State _state;

    // Path
    private Path _path;
    private int _waypointIndex;
    private float _nextRepathTime;

    // Goal
    private Transform _house;

    // Sensor candidates
    private readonly List<Collider2D> _candidates = new List<Collider2D>(8);

    // Attack target
    private Collider2D _attackTargetCol;
    private Health _attackTargetHp;

    // Attack timing
    private float _nextAttackAllowedTime;
    private float _scheduledHitTime;
    private bool _hitPending;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _seeker = GetComponent<Seeker>();
        if (animator == null) animator = GetComponent<Animator>();

        if (sensorTrigger == null)
            Debug.LogWarning($"{name}: sensorTrigger 未绑定（AttackSensor 的 Trigger Collider）");
    }

    private void OnEnable()
    {
        CacheHouse();
        SetState(State.MoveToHouse);
        _nextRepathTime = 0f;
    }

    private void OnDisable()
    {
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
    }

    private void Update()
    {
        if (_state == State.Dead) return;

        CacheHouseIfLost();
        PruneCandidates();

        ResolveAttackTarget();

        if (_attackTargetHp != null)
        {
            if (_state != State.Attack) SetState(State.Attack);
        }
        else
        {
            if (_state != State.MoveToHouse) SetState(State.MoveToHouse);
        }

        if (!useAnimationEventForHit && _hitPending && Time.time >= _scheduledHitTime)
        {
            _hitPending = false;
            TryDealDamage();
        }

        if (_state == State.MoveToHouse && _house != null && Time.time >= _nextRepathTime)
        {
            _nextRepathTime = Time.time + repathInterval;
            RequestPathToHouse();
        }

        if (animator != null && !string.IsNullOrWhiteSpace(animSpeedParam))
        {
            float speed01 = (_state == State.MoveToHouse && _attackTargetHp == null) ? 1f : 0f;
            animator.SetFloat(animSpeedParam, speed01);
        }

        if (drawDebugPath && _path != null && _path.vectorPath != null)
        {
            for (int i = 0; i < _path.vectorPath.Count - 1; i++)
                Debug.DrawLine(_path.vectorPath[i], _path.vectorPath[i + 1], Color.cyan);
        }
    }

    private void FixedUpdate()
    {
        if (_state == State.Dead) return;

        switch (_state)
        {
            case State.MoveToHouse:
                TickMove();
                break;

            case State.Attack:
                TickAttack();
                break;
        }
    }

    private void TickMove()
    {
        if (_house == null)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (_path == null || _path.vectorPath == null || _path.vectorPath.Count == 0)
        {
            Vector2 dirFallback = ((Vector2)_house.position - _rb.position).normalized;
            if (lockYMovement) dirFallback.y = 0f;
            _rb.linearVelocity = dirFallback * moveSpeed;
            return;
        }

        if (_waypointIndex >= _path.vectorPath.Count)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 waypoint = (Vector2)_path.vectorPath[_waypointIndex];
        Vector2 to = waypoint - _rb.position;

        if (to.magnitude <= nextWaypointDistance)
        {
            _waypointIndex++;
            if (_waypointIndex >= _path.vectorPath.Count)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }
            waypoint = (Vector2)_path.vectorPath[_waypointIndex];
            to = waypoint - _rb.position;
        }

        Vector2 dir = to.normalized;
        if (lockYMovement) dir.y = 0f;

        _rb.linearVelocity = dir * moveSpeed;
    }

    private void RequestPathToHouse()
    {
        if (_seeker == null || !_seeker.IsDone() || _house == null) return;

        Vector3 start = _rb.position;
        Vector3 end = _house.position;

        _seeker.StartPath(start, end, OnPathComplete);
    }

    private void OnPathComplete(Path p)
    {
        if (p == null || p.error) return;

        _path = p;
        _waypointIndex = 0;
    }

    private void TickAttack()
    {
        _rb.linearVelocity = Vector2.zero;

        if (_attackTargetHp == null || _attackTargetCol == null)
            return;

        if (!_candidates.Contains(_attackTargetCol) || _attackTargetHp.dead)
        {
            _attackTargetCol = null;
            _attackTargetHp = null;
            return;
        }

        if (Time.time < _nextAttackAllowedTime) return;

        StartAttackCycle();
    }

    private void StartAttackCycle()
    {
        _nextAttackAllowedTime = Time.time + attackCooldown;

        if (animator != null && !string.IsNullOrWhiteSpace(animAttackTrigger))
            animator.SetTrigger(animAttackTrigger);

        if (useAnimationEventForHit)
        {
            _hitPending = false;
        }
        else
        {
            _scheduledHitTime = Time.time + attackWindup;
            _hitPending = true;
        }
    }

    private void TryDealDamage()
    {
        if (_attackTargetHp == null || _attackTargetCol == null) return;

        int dmg = 0;
        if (canTargetPlayer && _attackTargetCol.CompareTag(playerTag))
            dmg = damageToPlayer;
        else if (_attackTargetCol.CompareTag(wallTag))
            dmg = damageToWall;
        else
            return;

        _attackTargetHp.TakeDamage(dmg);
    }

    public void AnimEvent_DealDamage()
    {
        if (_state == State.Dead) return;
        if (!useAnimationEventForHit) return;
        TryDealDamage();
    }

    private void ResolveAttackTarget()
    {
        Collider2D chosen = ChooseTargetByPriority();
        if (chosen == null)
        {
            _attackTargetCol = null;
            _attackTargetHp = null;
            return;
        }

        Health hp = chosen.GetComponentInParent<Health>();
        if (hp == null || hp.dead)
        {
            _candidates.Remove(chosen);
            _attackTargetCol = null;
            _attackTargetHp = null;
            return;
        }

        _attackTargetCol = chosen;
        _attackTargetHp = hp;
    }

    private Collider2D ChooseTargetByPriority()
    {
        Collider2D bestPlayer = null;
        Collider2D bestWall = null;

        float bestPlayerDist = float.MaxValue;
        float bestWallDist = float.MaxValue;

        for (int i = 0; i < _candidates.Count; i++)
        {
            Collider2D c = _candidates[i];
            if (c == null) continue;

            float d = Vector2.Distance(_rb.position, c.transform.position);

            if (canTargetPlayer && c.CompareTag(playerTag))
            {
                if (d < bestPlayerDist)
                {
                    bestPlayerDist = d;
                    bestPlayer = c;
                }
            }
            else if (c.CompareTag(wallTag))
            {
                if (d < bestWallDist)
                {
                    bestWallDist = d;
                    bestWall = c;
                }
            }
        }

        if (preferPlayerOverWall)
            return bestPlayer != null ? bestPlayer : bestWall;
        else
            return bestWall != null ? bestWall : bestPlayer;
    }

    private void PruneCandidates()
    {
        for (int i = _candidates.Count - 1; i >= 0; i--)
        {
            if (_candidates[i] == null) _candidates.RemoveAt(i);
        }
    }

    public void SensorEnter(Collider2D other)
    {
        if (other == null) return;

        if (((1 << other.gameObject.layer) & targetLayers.value) == 0) return;

        bool isPlayer = canTargetPlayer && other.CompareTag(playerTag);
        bool isWall = other.CompareTag(wallTag);
        if (!isPlayer && !isWall) return;

        if (!_candidates.Contains(other))
            _candidates.Add(other);
    }

    public void SensorExit(Collider2D other)
    {
        if (other == null) return;

        _candidates.Remove(other);

        if (_attackTargetCol == other)
        {
            _attackTargetCol = null;
            _attackTargetHp = null;
        }
    }

    private void CacheHouse()
    {
        if (houseOverride != null)
        {
            _house = houseOverride;
            return;
        }

        if (HouseObjective.Instance != null)
        {
            _house = HouseObjective.Instance.targetPoint != null
                ? HouseObjective.Instance.targetPoint
                : HouseObjective.Instance.transform;
        }
        else
        {
            _house = null;
        }
    }

    private void CacheHouseIfLost()
    {
        if (_house != null) return;
        CacheHouse();
    }

    private void SetState(State next)
    {
        if (_state == next) return;

        _state = next;
        if (logStateChanges)
            Debug.Log($"[EnemyAI2D] {name} -> {_state}");

        if (_state == State.Attack)
        {
            _rb.linearVelocity = Vector2.zero;
            _path = null;
        }
    }

    public void Die()
    {
        if (_state == State.Dead) return;
        _state = State.Dead;
        _rb.linearVelocity = Vector2.zero;
    }

    public void ApplySpeedMultiplier(float multiplier)
    {
        if (multiplier <= 0f) multiplier = 0.01f;
        moveSpeed *= multiplier;
    }
}
