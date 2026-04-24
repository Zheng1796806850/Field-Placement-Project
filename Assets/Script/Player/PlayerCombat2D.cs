using UnityEngine;
using UnityEngine.EventSystems;
using System.Globalization;
using UnityEngine.Animations;

public class PlayerCombat2D : MonoBehaviour
{
    private const string AxeUpgradeGrantedKeyLocal = "player_axe_upgrade_granted";
    private const string AxeDamageMultiplierKeyLocal = "player_axe_damage_multiplier";

    [Header("Refs")]
    public PlayerMovementController movement;
    public Animator animator;

    [Header("Attack Colliders (left / right only; size in editor to cover slight vertical reach)")]
    public BoxCollider2D attackLeft;
    public BoxCollider2D attackRight;

    [Header("Attack Settings")]
    public KeyCode attackKey = KeyCode.Mouse0;
    public float attackLockTime = 0.35f;

    [Header("Input State")]
    public bool localInputEnabled = true;

    [Header("Quest Upgrade")]
    [SerializeField, Min(1f)] private float axeDamageMultiplier = 1f;
    [SerializeField] private bool axeUpgradeGranted;
    [SerializeField] private RuntimeAnimatorController axeUpgradeAnimatorController;

    private bool isAttacking;
    private float attackTimer;
    private BoxCollider2D currentCollider;
    private int externalInputBlockCount;

    public bool IsAttacking => isAttacking;
    public bool InputEnabled => localInputEnabled && externalInputBlockCount <= 0;
    public float AxeDamageMultiplier => Mathf.Max(1f, axeDamageMultiplier);
    public bool AxeUpgradeGranted => axeUpgradeGranted;

    private void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerMovementController>();
        if (animator == null) animator = GetComponent<Animator>();

        LoadAxeUpgradeState();
        ApplyAxeUpgradeAnimatorIfNeeded();
        DisableAllColliders();
    }

    private static bool ShouldSuppressAttackForUiOrDrag()
    {
        if (BackpackSlotUI.DragPayload.active)
            return true;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return true;

        return false;
    }

    private void Update()
    {
        if (!isAttacking && InputEnabled && Input.GetKeyDown(attackKey))
        {
            if (!ShouldSuppressAttackForUiOrDrag())
                StartAttack();
        }

        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
                EndAttack();
        }
    }

    public void SetInputEnabled(bool enabled)
    {
        localInputEnabled = enabled;
        if (!InputEnabled && isAttacking)
            EndAttack();
    }

    public void PushExternalInputBlock()
    {
        externalInputBlockCount++;
        if (externalInputBlockCount < 0)
            externalInputBlockCount = 0;

        if (!InputEnabled && isAttacking)
            EndAttack();
    }

    public void PopExternalInputBlock()
    {
        externalInputBlockCount--;
        if (externalInputBlockCount < 0)
            externalInputBlockCount = 0;
    }

    private void StartAttack()
    {
        if (!InputEnabled) return;

        isAttacking = true;
        attackTimer = attackLockTime;

        if (movement != null)
            movement.SetCanMove(false);

        Vector2 dir = movement != null ? movement.GetFacingDir() : Vector2.down;

        if (animator != null)
        {
            float sx = movement != null ? movement.FacingSignX : Mathf.Sign(dir.x);
            if (Mathf.Abs(sx) < 1e-4f) sx = 1f;
            animator.SetFloat("InputX", sx);
            animator.SetFloat("InputY", 0f);
            animator.SetTrigger("Attack");
        }

        SelectAttackCollider(dir);
    }

    private void EndAttack()
    {
        isAttacking = false;

        if (movement != null)
            movement.SetCanMove(true);

        DisableAllColliders();
    }

    private void SelectAttackCollider(Vector2 dir)
    {
        DisableAllColliders();

        float sx = movement != null ? movement.FacingSignX : Mathf.Sign(dir.x);
        if (Mathf.Abs(sx) < 1e-4f) sx = 1f;
        currentCollider = sx > 0f ? attackRight : attackLeft;
    }

    private void DisableAllColliders()
    {
        if (attackLeft != null) attackLeft.enabled = false;
        if (attackRight != null) attackRight.enabled = false;
        currentCollider = null;
    }

    /// <summary>Call from attack animation (Animation Event) to play swing SFX in sync with the clip.</summary>
    public void AnimEvent_PlayAttackSwing()
    {
        SfxPlayer.TryPlay(SfxId.Combat_AttackSwing, transform.position);
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

    public bool GrantAxeUpgrade(float multiplier, RuntimeAnimatorController overrideAnimatorController = null)
    {
        float next = Mathf.Max(1f, multiplier);
        bool changed = false;

        if (!axeUpgradeGranted)
        {
            axeUpgradeGranted = true;
            changed = true;
        }

        if (next > axeDamageMultiplier)
        {
            axeDamageMultiplier = next;
            changed = true;
        }

        if (overrideAnimatorController != null)
            axeUpgradeAnimatorController = overrideAnimatorController;

        if (!changed)
        {
            ApplyAxeUpgradeAnimatorIfNeeded();
            return false;
        }

        SaveAxeUpgradeState();
        ApplyAxeUpgradeAnimatorIfNeeded();
        NotifyDamagePipelineChanged();
        return true;
    }

    private void NotifyDamagePipelineChanged()
    {
        var vitals = GetComponent<PlayerHungerThirst>();
        if (vitals == null)
            vitals = GetComponentInChildren<PlayerHungerThirst>(true);
        vitals?.RefreshDebuffsNow();
    }

    private void LoadAxeUpgradeState()
    {
        string grantedKey = BaseWorldSession.ScopePlayerPrefsKey(AxeUpgradeGrantedKeyLocal);
        string multKey = BaseWorldSession.ScopePlayerPrefsKey(AxeDamageMultiplierKeyLocal);

        axeUpgradeGranted = PlayerPrefs.GetInt(grantedKey, 0) == 1;
        string s = PlayerPrefs.GetString(multKey, "");
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            axeDamageMultiplier = Mathf.Max(1f, parsed);
        else
            axeDamageMultiplier = Mathf.Max(1f, axeDamageMultiplier);
    }

    private void SaveAxeUpgradeState()
    {
        string grantedKey = BaseWorldSession.ScopePlayerPrefsKey(AxeUpgradeGrantedKeyLocal);
        string multKey = BaseWorldSession.ScopePlayerPrefsKey(AxeDamageMultiplierKeyLocal);

        PlayerPrefs.SetInt(grantedKey, axeUpgradeGranted ? 1 : 0);
        PlayerPrefs.SetString(multKey, axeDamageMultiplier.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    private void ApplyAxeUpgradeAnimatorIfNeeded()
    {
        if (!axeUpgradeGranted || animator == null || axeUpgradeAnimatorController == null)
            return;

        if (animator.runtimeAnimatorController != axeUpgradeAnimatorController)
            animator.runtimeAnimatorController = axeUpgradeAnimatorController;
    }
}