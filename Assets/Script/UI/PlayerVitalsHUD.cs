using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerVitalsHUD : MonoBehaviour
{
    [Header("Refs")]
    public Health health;
    public PlayerHungerThirst hungerThirst;

    [Header("HP Bar")]
    public Image hpFill;
    public TextMeshProUGUI hpLabel;
    public bool showHpText = false;

    [Header("Hunger Bar")]
    public Image hungerFill;
    public TextMeshProUGUI hungerLabel;
    public bool showHungerText = false;

    [Header("Thirst Bar")]
    public Image thirstFill;
    public TextMeshProUGUI thirstLabel;
    public bool showThirstText = false;

    private void Awake()
    {
        if (hungerThirst == null) hungerThirst = FindFirstObjectByType<PlayerHungerThirst>(FindObjectsInactive.Include);
        if (health == null)
        {
            if (hungerThirst != null && hungerThirst.health != null) health = hungerThirst.health;
            else health = FindFirstObjectByType<Health>(FindObjectsInactive.Include);
        }
        PushAll();
    }

    private void OnEnable()
    {
        if (hungerThirst == null) hungerThirst = FindFirstObjectByType<PlayerHungerThirst>(FindObjectsInactive.Include);
        if (health == null)
        {
            if (hungerThirst != null && hungerThirst.health != null) health = hungerThirst.health;
            else health = FindFirstObjectByType<Health>(FindObjectsInactive.Include);
        }

        if (health != null) health.OnHealthChanged += HandleHealthChanged;

        if (hungerThirst != null)
        {
            hungerThirst.OnHungerChanged += HandleHungerChanged;
            hungerThirst.OnThirstChanged += HandleThirstChanged;
        }

        PushAll();
    }

    private void OnDisable()
    {
        if (health != null) health.OnHealthChanged -= HandleHealthChanged;

        if (hungerThirst != null)
        {
            hungerThirst.OnHungerChanged -= HandleHungerChanged;
            hungerThirst.OnThirstChanged -= HandleThirstChanged;
        }
    }

    private void HandleHealthChanged(int cur, int max) => SetHP(cur, max);
    private void HandleHungerChanged(float cur, float max) => SetHunger(cur, max);
    private void HandleThirstChanged(float cur, float max) => SetThirst(cur, max);

    private void PushAll()
    {
        if (health != null) SetHP(health.currentHP, health.maxHP);

        if (hungerThirst != null)
        {
            SetHunger(hungerThirst.Hunger, hungerThirst.hungerMax);
            SetThirst(hungerThirst.Thirst, hungerThirst.thirstMax);
        }
    }

    private void SetHP(int cur, int max)
    {
        max = Mathf.Max(1, max);
        cur = Mathf.Clamp(cur, 0, max);

        if (hpFill != null) hpFill.fillAmount = cur / (float)max;
        if (hpLabel != null) hpLabel.text = showHpText ? $"{cur}/{max}" : "";
    }

    private void SetHunger(float cur, float max)
    {
        max = Mathf.Max(1f, max);
        cur = Mathf.Clamp(cur, 0f, max);

        if (hungerFill != null) hungerFill.fillAmount = cur / max;
        if (hungerLabel != null) hungerLabel.text = showHungerText ? $"{cur:0}/{max:0}" : "";
    }

    private void SetThirst(float cur, float max)
    {
        max = Mathf.Max(1f, max);
        cur = Mathf.Clamp(cur, 0f, max);

        if (thirstFill != null) thirstFill.fillAmount = cur / max;
        if (thirstLabel != null) thirstLabel.text = showThirstText ? $"{cur:0}/{max:0}" : "";
    }
}