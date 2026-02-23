using System;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Seeker))]
public class EnemyAI2D : MonoBehaviour
{
    private enum State
    {
        MoveToGoal,
        Attack,
        Dead
    }

    [Header("Goal (House)")]
    public Transform houseOverride;

    [Header("Sensor / Target Filter")]
    public Collider2D sensorTrigger;
    public LayerMask targetLayers = ~0;
    public string playerTag = "Player";
    public string wallTag = "Wall";
    public string coreTag = "Core";
    public bool canTargetPlayer = true;
    public bool canTargetCore = true;
    public bool preferPlayerOverWall = true;
    public bool preferWallOverCore = true;

    [Header("Reactive Targeting")]
    [Min(0f)] public float playerAggroRange = 6f;
    [Min(0f)] public float playerDisengageRange = 7f;
    [Min(0f)] public float hitAggroDuration = 3f;
    [Min(0f)] public float hitAggroMaxDistance = 12f;
    public bool breakWallAttackWhenAggroPlayer = true;

    [Header("Movement")]
    [Min(0f)] public float moveSpeed = 2.0f;
    [Min(0.05f)] public float repathInterval = 0.5f;
    [Min(0.01f)] public float nextWaypointDistance = 0.2f;
    public bool lockYMovement = false;

    [Header("Attack - Damage")]
    [Min(0)] public int damageToWall = 5;
    [Min(0)] public int damageToPlayer = 5;
    [Min(0)] public int damageToCore = 8;

    [Header("Attack - Timing (seconds)")]
    [Min(0.05f)] public float attackCooldown = 1.0f;
    [Min(0f)] public float attackWindup = 0.25f;
    public bool useAnimationEventForHit = false;

    [Header("Animation (Optional)")]
    public Animator animator;
    public string animSpeedParam = "Speed";
    public string animAttackTrigger = "Attack";

    [Header("Debug")]
    public bool logStateChanges = false;
    public bool drawDebugPath = false;

    [Header("Debug Gizmos")]
    public bool drawReactiveGizmos = true;
    public bool drawTargetLines = true;
    public Color gizmoAggroColor = new Color(0.25f, 1f, 0.25f, 0.75f);
    public Color gizmoDisengageColor = new Color(1f, 0.9f, 0.2f, 0.75f);
    public Color gizmoHitAggroMaxColor = new Color(1f, 0.25f, 0.25f, 0.75f);
    public Color gizmoLineToPlayerColor = new Color(1f, 0.35f, 0.35f, 0.9f);
    public Color gizmoLineToHouseColor = new Color(0.35f, 0.8f, 1f, 0.9f);
    [Min(0f)] public float gizmoZOffset = 0f;

    private Rigidbody2D _rb;
    private Seeker _seeker;

    private State _state;

    private Path _path;
    private int _waypointIndex;
    private float _nextRepathTime;

    private Transform _house;
    private Transform _player;
    private Transform _moveGoal;

    private readonly List<Collider2D> _candidates = new List<Collider2D>(8);

    private Collider2D _attackTargetCol;
    private Health _attackTargetHp;

    private float _nextAttackAllowedTime;
    private float _scheduledHitTime;
    private bool _hitPending;

    private bool _baseCaptured;
    private float _baseMoveSpeed;
    private int _baseWallDamage;

    private float _forcedAggroUntil;
    private bool _proximityAggro;
    private float _nextPlayerFindTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _seeker = GetComponent<Seeker>();
        if (animator == null) animator = GetComponent<Animator>();

        CaptureBaseIfNeeded();

        if (sensorTrigger == null)
            Debug.LogWarning($"{name}: sensorTrigger Î´°ó¶¨(AttackSensor µÄ Trigger Collider)");
    }

    private void CaptureBaseIfNeeded()
    {
        if (_baseCaptured) return;
        _baseCaptured = true;

        _baseMoveSpeed = Mathf.Max(0f, moveSpeed);
        _baseWallDamage = Mathf.Max(0, damageToWall);
    }

    private void OnEnable()
    {
        CacheHouse();
        CachePlayer(true);

        _moveGoal = _house;
        SetState(State.MoveToGoal);
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
        CachePlayer(false);

        PruneCandidates();

        UpdateReactiveAggro();

        bool chasePlayer = ShouldChasePlayer();
        Transform desiredGoal = chasePlayer ? _player : _house;

        if (_moveGoal != desiredGoal)
        {
            _moveGoal = desiredGoal;
            _path = null;
            _waypointIndex = 0;
            _nextRepathTime = 0f;
        }

        bool forcePlayerAttack = chasePlayer;
        ResolveAttackTarget(forcePlayerAttack);

        bool playerInAttackRange = _attackTargetCol != null && canTargetPlayer && _attackTargetCol.CompareTag(playerTag);

        if (chasePlayer)
        {
            if (playerInAttackRange)
            {
                if (_state != State.Attack) SetState(State.Attack);
            }
            else
            {
                if (breakWallAttackWhenAggroPlayer && _state == State.Attack)
                {
                    if (_attackTargetCol != null && (_attackTargetCol.CompareTag(wallTag) || _attackTargetCol.CompareTag(coreTag)))
                    {
                        _attackTargetCol = null;
                        _attackTargetHp = null;
                    }
                }

                if (_state != State.MoveToGoal) SetState(State.MoveToGoal);
            }
        }
        else
        {
            if (_attackTargetHp != null)
            {
                if (_state != State.Attack) SetState(State.Attack);
            }
            else
            {
                if (_state != State.MoveToGoal) SetState(State.MoveToGoal);
            }
        }

        if (!useAnimationEventForHit && _hitPending && Time.time >= _scheduledHitTime)
        {
            _hitPending = false;
            TryDealDamage();
        }

        if (_state == State.MoveToGoal && _moveGoal != null && Time.time >= _nextRepathTime)
        {
            _nextRepathTime = Time.time + repathInterval;
            RequestPathTo(_moveGoal);
        }

        if (animator != null && !string.IsNullOrWhiteSpace(animSpeedParam))
        {
            float speed01 = (_state == State.MoveToGoal && _attackTargetHp == null) ? 1f : 0f;
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
            case State.MoveToGoal:
                TickMove();
                break;

            case State.Attack:
                TickAttack();
                break;
        }
    }

    private void TickMove()
    {
        if (_moveGoal == null)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (_path == null || _path.vectorPath == null || _path.vectorPath.Count == 0)
        {
            Vector2 dirFallback = ((Vector2)_moveGoal.position - _rb.position).normalized;
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

    private void RequestPathTo(Transform goal)
    {
        if (_seeker == null || !_seeker.IsDone() || goal == null) return;

        Vector3 start = _rb.position;
        Vector3 end = goal.position;

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
        else if (canTargetCore && _attackTargetCol.CompareTag(coreTag))
            dmg = damageToCore;
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

    private void ResolveAttackTarget(bool forcePlayer)
    {
        Collider2D chosen = ChooseTargetByPriority(forcePlayer);
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

    private Collider2D ChooseTargetByPriority(bool forcePlayer)
    {
        Collider2D bestPlayer = null;
        Collider2D bestWall = null;
        Collider2D bestCore = null;

        float bestPlayerDist = float.MaxValue;
        float bestWallDist = float.MaxValue;
        float bestCoreDist = float.MaxValue;

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
            else if (canTargetCore && c.CompareTag(coreTag))
            {
                if (d < bestCoreDist)
                {
                    bestCoreDist = d;
                    bestCore = c;
                }
            }
        }

        if (forcePlayer)
            return bestPlayer != null ? bestPlayer : null;

        if (preferPlayerOverWall && bestPlayer != null)
            return bestPlayer;

        if (preferWallOverCore)
            return bestWall != null ? bestWall : bestCore;
        else
            return bestCore != null ? bestCore : bestWall;
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
        bool isCore = canTargetCore && other.CompareTag(coreTag);

        if (!isPlayer && !isWall && !isCore) return;

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

    private void CachePlayer(bool immediate)
    {
        if (!canTargetPlayer) { _player = null; return; }

        if (_player != null) return;

        if (!immediate && Time.time < _nextPlayerFindTime) return;
        _nextPlayerFindTime = Time.time + 0.5f;

        GameObject p = null;
        try { p = GameObject.FindGameObjectWithTag(playerTag); } catch { p = null; }
        _player = p != null ? p.transform : null;
    }

    private void UpdateReactiveAggro()
    {
        if (!canTargetPlayer || _player == null)
        {
            _proximityAggro = false;
            return;
        }

        float sqr = ((Vector2)_player.position - _rb.position).sqrMagnitude;
        float enter = Mathf.Max(0f, playerAggroRange);
        float exit = Mathf.Max(enter, playerDisengageRange);

        float enterSqr = enter * enter;
        float exitSqr = exit * exit;

        if (_proximityAggro)
            _proximityAggro = sqr <= exitSqr;
        else
            _proximityAggro = sqr <= enterSqr;
    }

    private bool ShouldChasePlayer()
    {
        if (!canTargetPlayer || _player == null) return false;

        bool hitAggro = false;
        if (Time.time < _forcedAggroUntil)
        {
            float maxD = Mathf.Max(0f, hitAggroMaxDistance);
            float sqr = ((Vector2)_player.position - _rb.position).sqrMagnitude;
            hitAggro = sqr <= (maxD * maxD);
        }

        return _proximityAggro || hitAggro;
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
            _waypointIndex = 0;
            _hitPending = false;
        }
        else if (_state == State.MoveToGoal)
        {
            _hitPending = false;
        }
    }

    public void Die()
    {
        if (_state == State.Dead) return;
        _state = State.Dead;
        _rb.linearVelocity = Vector2.zero;
    }

    public void NotifyAttacked(GameObject attacker)
    {
        if (!canTargetPlayer) return;

        if (attacker != null)
        {
            if (_player == null && attacker.CompareTag(playerTag))
                _player = attacker.transform;

            if (_player == null && attacker.transform != null)
            {
                if (attacker.CompareTag(playerTag))
                    _player = attacker.transform;
            }
        }

        if (_player == null) CachePlayer(true);
        if (_player == null) return;

        _forcedAggroUntil = Time.time + Mathf.Max(0f, hitAggroDuration);
    }

    public void SetBaseMoveSpeed(float baseSpeed)
    {
        CaptureBaseIfNeeded();
        _baseMoveSpeed = Mathf.Max(0f, baseSpeed);
        moveSpeed = _baseMoveSpeed;
    }

    public void SetBaseWallDamage(int baseDamage)
    {
        CaptureBaseIfNeeded();
        _baseWallDamage = Mathf.Max(0, baseDamage);
        damageToWall = _baseWallDamage;
    }

    public void ApplySpeedMultiplier(float multiplier)
    {
        CaptureBaseIfNeeded();
        if (multiplier <= 0f) multiplier = 0.01f;
        moveSpeed = _baseMoveSpeed * multiplier;
    }

    public void ApplyWallDamageMultiplier(float multiplier)
    {
        CaptureBaseIfNeeded();
        if (multiplier <= 0f) multiplier = 0.01f;
        damageToWall = Mathf.Max(0, Mathf.RoundToInt(_baseWallDamage * multiplier));
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawReactiveGizmos) return;

        Vector3 pos = transform.position;
        pos.z += gizmoZOffset;

        float rAggro = Mathf.Max(0f, playerAggroRange);
        float rDis = Mathf.Max(rAggro, playerDisengageRange);
        float rHit = Mathf.Max(0f, hitAggroMaxDistance);

        Gizmos.color = gizmoAggroColor;
        Gizmos.DrawWireSphere(pos, rAggro);

        Gizmos.color = gizmoDisengageColor;
        Gizmos.DrawWireSphere(pos, rDis);

        Gizmos.color = gizmoHitAggroMaxColor;
        Gizmos.DrawWireSphere(pos, rHit);

        if (drawTargetLines)
        {
            Transform player = _player;
            if (player == null)
            {
                GameObject p = null;
                try { p = GameObject.FindGameObjectWithTag(playerTag); } catch { p = null; }
                player = p != null ? p.transform : null;
            }

            Transform house = _house;
            if (house == null)
            {
                if (houseOverride != null) house = houseOverride;
                else if (HouseObjective.Instance != null)
                    house = HouseObjective.Instance.targetPoint != null ? HouseObjective.Instance.targetPoint : HouseObjective.Instance.transform;
            }

            if (player != null)
            {
                Gizmos.color = gizmoLineToPlayerColor;
                Vector3 ppos = player.position;
                ppos.z = pos.z;
                Gizmos.DrawLine(pos, ppos);
                Handles.Label(pos + Vector3.up * 0.25f, $"Aggro:{rAggro:F1}  Dis:{rDis:F1}  HitMax:{rHit:F1}");
            }

            if (house != null)
            {
                Gizmos.color = gizmoLineToHouseColor;
                Vector3 hpos = house.position;
                hpos.z = pos.z;
                Gizmos.DrawLine(pos, hpos);
            }
        }
    }
#endif
}