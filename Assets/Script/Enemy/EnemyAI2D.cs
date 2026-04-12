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
    public EnemyAISensor2D sensorProxy;
    public LayerMask targetLayers = ~0;
    public string playerTag = "Player";
    public string wallTag = "Wall";
    public string coreTag = "Core";
    public bool canTargetPlayer = true;
    public bool canTargetCore = true;
    public bool preferPlayerOverWall = true;
    public bool preferWallOverCore = true;

    [Header("Target Collider Filters")]
    public bool ignoreTriggerTargets = true;
    public bool ignoreDisabledTargets = true;

    [Header("Attack Sensor Validation")]
    public bool requireAssignedSensorOverlapForAttack = true;

    [Header("Reactive Targeting")]
    [Min(0f)] public float playerAggroRange = 6f;
    [Min(0f)] public float playerDisengageRange = 7f;
    [Min(0f)] public float hitAggroDuration = 3f;
    [Min(0f)] public float hitAggroMaxDistance = 12f;
    public bool breakWallAttackWhenAggroPlayer = true;

    [Header("Movement")]
    [Tooltip("Base move speed on prefab. After wave spawn, final speed = this * wave speedMultiplier unless WaveSpawnController2D disables scaling.")]
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
    [Tooltip("Only used when useAnimationEventForHit is false. When true, damage timing uses AnimEvent_DealDamage only.")]
    [Min(0f)] public float attackWindup = 0.25f;
    [Tooltip("When true: use AnimEvent_DealDamage / AnimEvent_AttackFinished; windup scheduling is disabled.")]
    public bool useAnimationEventForHit = true;

    [Header("Blocked Path Wall Breaking")]
    public bool attackWallsOnlyWhenHousePathBlocked = true;
    [Min(0f)] public float houseReachThreshold = 0.6f;
    public bool keepRepathingWhileBlocked = true;

    [Header("Animation (Optional)")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    [Tooltip("Float 0..1: normalized move speed. Drives Move state's animation playback speed (0 = frozen walk pose when no Idle clip).")]
    public string animSpeedParam = "Speed";
    public string animAttackTrigger = "Attack";
    [Tooltip("Death trigger name on Controller (for reference). Death is forced via Animator.Play(animDeathStateName), not this trigger.")]
    public string animDeathTrigger = "Death";
    [Tooltip("State name in Animator Controller to force on death (Animator.Play).")]
    public string animDeathStateName = "Death";
    [Tooltip("Seconds after death to Destroy (Health.destroyOnDeath should be off on this prefab).")]
    [Min(0.05f)] public float destroyAfterDeathDelay = 1.25f;
    [Tooltip("SfxId played from AnimEvent_PlayEnemyAttackSfx on attack clip.")]
    public SfxId enemyAttackSfxId = SfxId.Combat_EnemyAttackSwing;
    [Min(0.001f)] public float flipVelocityThreshold = 0.05f;

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
    public Color gizmoBlockedHousePathColor = new Color(1f, 0.25f, 0.25f, 0.9f);
    public Color gizmoAttackSensorColor = new Color(1f, 0.5f, 0.1f, 0.9f);
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

    private bool _housePathBlocked;

    private Health _health;
    private bool _deathHandled;
    private bool _attackCycleActive;

    /// <summary>True = art mirrored (face right). Updated from movement; during Attack from target position (velocity is zero).</summary>
    private bool _facingRight;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _seeker = GetComponent<Seeker>();
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        _health = GetComponent<Health>();
        if (_health != null)
            _health.OnDied += HandleHealthDied;

        CaptureBaseIfNeeded();
        ResolveSensorRefs();

        if (sensorTrigger == null)
            Debug.LogWarning($"{name}: sensorTrigger is not assigned");
    }

    private void OnDestroy()
    {
        if (_health != null)
            _health.OnDied -= HandleHealthDied;
    }

    private void OnEnable()
    {
        ResolveSensorRefs();

        CacheHouse();
        CachePlayer(true);

        _moveGoal = _house;
        SetState(State.MoveToGoal);
        _nextRepathTime = 0f;
        _housePathBlocked = false;

        _attackCycleActive = false;
        if (_health != null && !_health.dead)
            _deathHandled = false;
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(DelayedDestroyEnemy));
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
    }

    private void ResolveSensorRefs()
    {
        if (sensorTrigger == null && sensorProxy != null)
            sensorTrigger = sensorProxy.GetComponent<Collider2D>();

        if (sensorProxy == null && sensorTrigger != null)
            sensorProxy = sensorTrigger.GetComponent<EnemyAISensor2D>();

        if (sensorProxy == null)
            sensorProxy = GetComponentInChildren<EnemyAISensor2D>(true);

        if (sensorTrigger == null && sensorProxy != null)
            sensorTrigger = sensorProxy.GetComponent<Collider2D>();
    }

    private void CaptureBaseIfNeeded()
    {
        if (_baseCaptured) return;
        _baseCaptured = true;

        _baseMoveSpeed = Mathf.Max(0f, moveSpeed);
        _baseWallDamage = Mathf.Max(0, damageToWall);
    }

    private void Update()
    {
        if (_state == State.Dead) return;

        ResolveSensorRefs();
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

            if (chasePlayer)
                _housePathBlocked = false;
        }

        bool allowWallAttack = !chasePlayer && ShouldAttackWallsToReachHouse();
        bool forcePlayerAttack = chasePlayer;

        ResolveAttackTarget(forcePlayerAttack, allowWallAttack);

        bool playerInAttackRange = _attackTargetCol != null && IsPlayerCollider(_attackTargetCol) && IsTargetInsideAttackSensor(_attackTargetCol);
        bool wallInAttackRange = _attackTargetCol != null && IsWallCollider(_attackTargetCol) && IsTargetInsideAttackSensor(_attackTargetCol);
        bool coreInAttackRange = _attackTargetCol != null && IsCoreCollider(_attackTargetCol) && IsTargetInsideAttackSensor(_attackTargetCol);

        bool canChangeLocomotionAttackState =
            !useAnimationEventForHit || !_attackCycleActive || _state != State.Attack;

        if (canChangeLocomotionAttackState)
        {
            if (chasePlayer)
            {
                if (playerInAttackRange)
                {
                    if (_state != State.Attack) SetState(State.Attack);
                }
                else
                {
                    if (_state != State.MoveToGoal) SetState(State.MoveToGoal);
                }
            }
            else
            {
                bool shouldAttack = false;

                if (coreInAttackRange)
                    shouldAttack = true;
                else if (wallInAttackRange && allowWallAttack)
                    shouldAttack = true;

                if (shouldAttack)
                {
                    if (_state != State.Attack) SetState(State.Attack);
                }
                else
                {
                    if (_state != State.MoveToGoal) SetState(State.MoveToGoal);
                }
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
            float speed01 = 0f;
            if (_state == State.MoveToGoal)
            {
                float denom = Mathf.Max(0.01f, moveSpeed);
                speed01 = Mathf.Clamp01(_rb.linearVelocity.magnitude / denom);
            }

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

        UpdateSpriteFacing();
    }

    /// <summary>Art faces left by default; mirror when moving right or when attacking a target to the right.</summary>
    private void UpdateSpriteFacing()
    {
        if (spriteRenderer == null) return;

        if (_state == State.Attack && _attackTargetCol != null)
        {
            float dx = _attackTargetCol.bounds.center.x - _rb.position.x;
            _facingRight = dx > flipVelocityThreshold;
        }
        else
        {
            float vx = _rb.linearVelocity.x;
            if (Mathf.Abs(vx) > flipVelocityThreshold)
                _facingRight = vx > flipVelocityThreshold;
        }

        spriteRenderer.flipX = _facingRight;
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
        if (p == null)
        {
            if (_moveGoal == _house)
                _housePathBlocked = true;
            return;
        }

        if (p.error)
        {
            if (_moveGoal == _house)
                _housePathBlocked = true;
            return;
        }

        _path = p;
        _waypointIndex = 0;

        if (_moveGoal == _house)
            RefreshHouseBlockedStateFromPath(p);
        else
            _housePathBlocked = false;
    }

    private void RefreshHouseBlockedStateFromPath(Path p)
    {
        if (_house == null)
        {
            _housePathBlocked = false;
            return;
        }

        if (p == null || p.vectorPath == null || p.vectorPath.Count == 0)
        {
            _housePathBlocked = true;
            return;
        }

        Vector3 endPoint = p.vectorPath[p.vectorPath.Count - 1];
        float distToHouse = Vector2.Distance(endPoint, _house.position);
        _housePathBlocked = distToHouse > Mathf.Max(0f, houseReachThreshold);
    }

    private bool ShouldAttackWallsToReachHouse()
    {
        if (!attackWallsOnlyWhenHousePathBlocked)
            return true;

        if (_moveGoal != _house)
            return false;

        return _housePathBlocked;
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

        if (!IsTargetInsideAttackSensor(_attackTargetCol))
        {
            _attackTargetCol = null;
            _attackTargetHp = null;
            return;
        }

        if (IsWallCollider(_attackTargetCol) && !ShouldAttackWallsToReachHouse())
        {
            _attackTargetCol = null;
            _attackTargetHp = null;
            return;
        }

        if (Time.time < _nextAttackAllowedTime) return;
        if (useAnimationEventForHit && _attackCycleActive) return;

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
            _attackCycleActive = true;
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
        if (!IsTargetInsideAttackSensor(_attackTargetCol)) return;

        int dmg = 0;

        if (canTargetPlayer && IsPlayerCollider(_attackTargetCol))
            dmg = damageToPlayer;
        else if (IsWallCollider(_attackTargetCol))
            dmg = damageToWall;
        else if (canTargetCore && IsCoreCollider(_attackTargetCol))
            dmg = damageToCore;
        else
            return;

        _attackTargetHp.TakeDamage(dmg);

        if (IsWallCollider(_attackTargetCol) && keepRepathingWhileBlocked)
            _nextRepathTime = 0f;
    }

    public void AnimEvent_DealDamage()
    {
        if (_state == State.Dead) return;
        if (!useAnimationEventForHit) return;
        if (_state != State.Attack || !_attackCycleActive) return;
        TryDealDamage();
    }

    private void ResolveAttackTarget(bool forcePlayer, bool allowWallAttack)
    {
        Collider2D chosen = ChooseTargetByPriority(forcePlayer, allowWallAttack);
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

    private Collider2D ChooseTargetByPriority(bool forcePlayer, bool allowWallAttack)
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
            if (!IsValidTargetCollider(c)) continue;
            if (!IsTargetInsideAttackSensor(c)) continue;

            float d = GetColliderDistance(c);

            if (canTargetPlayer && IsPlayerCollider(c))
            {
                if (d < bestPlayerDist)
                {
                    bestPlayerDist = d;
                    bestPlayer = c;
                }
            }
            else if (allowWallAttack && IsWallCollider(c))
            {
                if (d < bestWallDist)
                {
                    bestWallDist = d;
                    bestWall = c;
                }
            }
            else if (canTargetCore && IsCoreCollider(c))
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

    private float GetColliderDistance(Collider2D c)
    {
        if (c == null) return float.MaxValue;
        Vector2 from = _rb != null ? _rb.position : (Vector2)transform.position;
        Vector2 point = c.bounds.ClosestPoint(from);
        return Vector2.Distance(from, point);
    }

    private bool IsTargetInsideAttackSensor(Collider2D target)
    {
        if (target == null) return false;
        if (!requireAssignedSensorOverlapForAttack) return true;

        if (sensorProxy != null)
            return sensorProxy.Contains(target);

        if (sensorTrigger != null)
            return sensorTrigger.IsTouching(target);

        return false;
    }

    private bool IsValidTargetCollider(Collider2D c)
    {
        if (c == null) return false;

        if (ignoreDisabledTargets)
        {
            if (!c.enabled) return false;
            if (!c.gameObject.activeInHierarchy) return false;
        }

        if (ignoreTriggerTargets && c.isTrigger)
            return false;

        return true;
    }

    private void PruneCandidates()
    {
        for (int i = _candidates.Count - 1; i >= 0; i--)
        {
            Collider2D c = _candidates[i];

            if (!IsValidTargetCollider(c))
            {
                if (_attackTargetCol == c)
                {
                    _attackTargetCol = null;
                    _attackTargetHp = null;
                }

                _candidates.RemoveAt(i);
                continue;
            }

            var hp = c.GetComponentInParent<Health>();
            if (hp != null && hp.dead)
            {
                if (_attackTargetCol == c)
                {
                    _attackTargetCol = null;
                    _attackTargetHp = null;
                }

                _candidates.RemoveAt(i);
            }
        }
    }

    public void SensorEnter(Collider2D other)
    {
        if (!IsValidTargetCollider(other)) return;
        if (((1 << other.gameObject.layer) & targetLayers.value) == 0) return;

        bool isPlayer = canTargetPlayer && IsPlayerCollider(other);
        bool isWall = IsWallCollider(other);
        bool isCore = canTargetCore && IsCoreCollider(other);

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

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null) return false;
        return HasTagOnHierarchy(other.transform, playerTag);
    }

    private bool IsWallCollider(Collider2D other)
    {
        if (other == null) return false;

        if (HasTagOnHierarchy(other.transform, wallTag))
            return true;

        if (other.GetComponentInParent<WoodenWallDurability>() != null)
            return true;

        return false;
    }

    private bool IsCoreCollider(Collider2D other)
    {
        if (other == null) return false;
        return HasTagOnHierarchy(other.transform, coreTag);
    }

    private bool HasTagOnHierarchy(Transform t, string tagName)
    {
        if (t == null || string.IsNullOrWhiteSpace(tagName)) return false;

        Transform cur = t;
        while (cur != null)
        {
            if (cur.CompareTag(tagName))
                return true;
            cur = cur.parent;
        }

        return false;
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
        if (!canTargetPlayer)
        {
            _player = null;
            return;
        }

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
            if (useAnimationEventForHit)
                _attackCycleActive = false;
        }
        else if (_state == State.MoveToGoal)
        {
            _hitPending = false;
        }
    }

    private void HandleHealthDied()
    {
        BeginDeathSequence();
    }

    private void BeginDeathSequence()
    {
        if (_deathHandled) return;
        _deathHandled = true;

        _hitPending = false;
        _attackCycleActive = false;
        _state = State.Dead;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;

        if (logStateChanges)
            Debug.Log($"[EnemyAI2D] {name} -> {_state}");

        CancelInvoke(nameof(DelayedDestroyEnemy));

        if (animator != null)
        {
            animator.enabled = true;
            if (!string.IsNullOrWhiteSpace(animAttackTrigger))
                animator.ResetTrigger(animAttackTrigger);

            if (!string.IsNullOrWhiteSpace(animDeathStateName))
                animator.Play(Animator.StringToHash(animDeathStateName), 0, 0f);
        }

        Invoke(nameof(DelayedDestroyEnemy), Mathf.Max(0.05f, destroyAfterDeathDelay));
    }

    private void DelayedDestroyEnemy()
    {
        if (this != null && gameObject != null)
            Destroy(gameObject);
    }

    public void Die()
    {
        BeginDeathSequence();
    }

    public void AnimEvent_PlayEnemyAttackSfx()
    {
        if (_state == State.Dead) return;
        if (_state != State.Attack || !_attackCycleActive) return;
        SfxPlayer.TryPlay(enemyAttackSfxId, transform.position);
    }

    public void AnimEvent_AttackFinished()
    {
        if (!useAnimationEventForHit) return;
        _attackCycleActive = false;
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

        if (sensorTrigger != null)
        {
            Gizmos.color = gizmoAttackSensorColor;
            Bounds b = sensorTrigger.bounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }

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
                Gizmos.color = _housePathBlocked ? gizmoBlockedHousePathColor : gizmoLineToHouseColor;
                Vector3 hpos = house.position;
                hpos.z = pos.z;
                Gizmos.DrawLine(pos, hpos);
            }
        }
    }
#endif
}