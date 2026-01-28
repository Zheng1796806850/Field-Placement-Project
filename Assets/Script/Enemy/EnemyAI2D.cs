using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI2D : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 2f;

    [Header("Attack")]
    public Collider2D attackRange;
    public string wallTag = "Wall";
    public int damageToWall = 5;
    public float attackInterval = 1.0f;

    private Rigidbody2D rb;
    private float attackTimer;

    private Health currentWall;
    private bool isAttacking;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (attackRange == null)
            Debug.LogError($"{name}: AttackRange collider not assigned!");
    }

    private void Update()
    {
        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            HandleAttack();
        }
        else
        {
            rb.linearVelocity = Vector2.left * moveSpeed;
        }
    }

    private void HandleAttack()
    {
        if (currentWall == null)
        {
            StopAttack();
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            attackTimer = attackInterval;
            currentWall.TakeDamage(damageToWall);
        }
    }

    private void StartAttack(Health wallHealth)
    {
        isAttacking = true;
        currentWall = wallHealth;
        attackTimer = 0f;
    }

    private void StopAttack()
    {
        isAttacking = false;
        currentWall = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!attackRange || other != attackRange) return;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!attackRange || !attackRange.IsTouching(other)) return;

        if (!other.CompareTag(wallTag)) return;

        if (isAttacking) return;

        Health wallHP = other.GetComponentInParent<Health>();
        if (wallHP != null)
        {
            StartAttack(wallHP);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!attackRange || !attackRange.IsTouching(other)) return;

        if (other.CompareTag(wallTag))
        {
            StopAttack();
        }
    }

    public void ApplySpeedMultiplier(float multiplier)
    {
        if (multiplier <= 0f) multiplier = 0.01f;
        moveSpeed *= multiplier;
    }
}
