using UnityEngine;

public class PlayerCombat2D : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMovementController movement;
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    [Header("Attack Colliders")]
    public BoxCollider2D attackUp;
    public BoxCollider2D attackDown;
    public BoxCollider2D attackLeft;
    public BoxCollider2D attackRight;

    [Header("Attack Settings")]
    public KeyCode attackKey = KeyCode.Mouse0;
    public float attackLockTime = 0.35f;

    private bool isAttacking;
    private float attackTimer;
    private BoxCollider2D currentCollider;

    private void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovementController>();
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        DisableAllColliders();
    }

    private void Update()
    {
        if (!isAttacking && Input.GetKeyDown(attackKey))
        {
            StartAttack();
        }

        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
                EndAttack();
        }
    }

    private void StartAttack()
    {
        isAttacking = true;
        attackTimer = attackLockTime;

        movement.SetCanMove(false);

        Vector2 dir = movement.GetFacingDir();

        animator.SetFloat("InputX", dir.x);
        animator.SetFloat("InputY", dir.y);
        animator.SetTrigger("Attack");

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            spriteRenderer.flipX = dir.x < 0;

        SelectAttackCollider(dir);

        SfxPlayer.TryPlay(SfxId.Combat_AttackSwing, transform.position);
    }

    private void EndAttack()
    {
        isAttacking = false;
        movement.SetCanMove(true);
        DisableAllColliders();
    }

    private void SelectAttackCollider(Vector2 dir)
    {
        DisableAllColliders();

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            currentCollider = dir.x > 0 ? attackRight : attackLeft;
        }
        else
        {
            currentCollider = dir.y > 0 ? attackUp : attackDown;
        }
    }

    private void DisableAllColliders()
    {
        attackUp.enabled = false;
        attackDown.enabled = false;
        attackLeft.enabled = false;
        attackRight.enabled = false;
        currentCollider = null;
    }

    public void AnimEvent_EnableHitbox()
    {
        if (currentCollider != null)
            currentCollider.enabled = true;
    }

    public void AnimEvent_DisableHitbox()
    {
        if (currentCollider != null)
            currentCollider.enabled = false;
    }
}
