using UnityEngine;

public class AttackObjective : TutorialObjective
{
    public enum CompletionMode
    {
        AttackCountOnly = 0,
        DefeatTargetOnly = 1,
        Both = 2
    }

    [Header("Mode")]
    public CompletionMode mode = CompletionMode.Both;

    [Header("Attack Count")]
    [Min(1)] public int requiredAttackCount = 3;

    [Header("Defeat Target")]
    public Health targetEnemy;

    private PlayerCombat2D _combat;
    private bool _lastAttacking;
    private int _attackCount;
    private bool _defeated;

    protected override void OnBegin()
    {
        _combat = FindFirstObjectByType<PlayerCombat2D>(FindObjectsInactive.Include);
        _attackCount = 0;
        _defeated = false;
        _lastAttacking = _combat != null && _combat.IsAttacking;

        if (targetEnemy != null)
        {
            targetEnemy.OnDied -= HandleTargetDied;
            targetEnemy.OnDied += HandleTargetDied;
            if (targetEnemy.dead) _defeated = true;
        }
    }

    protected override void OnEnd()
    {
        if (targetEnemy != null)
            targetEnemy.OnDied -= HandleTargetDied;
    }

    private void Update()
    {
        if (IsCompleted) return;

        if (_combat != null)
        {
            bool now = _combat.IsAttacking;
            if (!_lastAttacking && now)
                _attackCount++;
            _lastAttacking = now;
        }

        bool attacksOk = _attackCount >= requiredAttackCount;
        bool defeatOk = _defeated;

        bool done = mode == CompletionMode.AttackCountOnly
            ? attacksOk
            : mode == CompletionMode.DefeatTargetOnly
                ? defeatOk
                : attacksOk && defeatOk;

        if (done)
            Complete();
    }

    private void HandleTargetDied()
    {
        _defeated = true;
    }

    public override string GetProgressText()
    {
        if (mode == CompletionMode.AttackCountOnly)
            return $"Attack {Mathf.Clamp(_attackCount, 0, requiredAttackCount)}/{requiredAttackCount}";

        if (mode == CompletionMode.DefeatTargetOnly)
            return $"Defeat enemy: {(_defeated ? "Done" : "Pending")}";

        return $"Attack {Mathf.Clamp(_attackCount, 0, requiredAttackCount)}/{requiredAttackCount} + Defeat enemy {(_defeated ? "Done" : "Pending")}";
    }
}
