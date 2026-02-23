using System;
using UnityEngine;

public class HouseObjective : MonoBehaviour
{
    public static HouseObjective Instance { get; private set; }

    [Header("Target Point (for enemy navigation)")]
    public Transform targetPoint;

    [Header("Core Health (CL-06)")]
    public Health coreHealth;
    [Min(1)] public int coreMaxHP = 100;
    public bool overrideHealthMaxOnAwake = true;
    public bool fillToMaxOnAwake = true;
    public bool coreDestroyOnDeath = false;

    [Header("Fail State")]
    public string defeatReason = "House core destroyed.";
    public bool showBannerOnDestroyed = true;
    public WaveEventBannerHUD banner;
    public string bannerText = "CORE DESTROYED";

    public event Action<int, int> OnCoreHealthChanged;
    public event Action OnCoreDestroyed;

    private bool _subscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (coreHealth == null)
            coreHealth = GetComponent<Health>();

        if (coreHealth == null)
            coreHealth = gameObject.AddComponent<Health>();

        coreHealth.destroyOnDeath = coreDestroyOnDeath;

        if (overrideHealthMaxOnAwake)
            coreHealth.SetMaxHP(coreMaxHP, fillToMaxOnAwake);

        if (banner == null)
            banner = FindFirstObjectByType<WaveEventBannerHUD>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        Subscribe();
        PushHealthChanged();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        if (coreHealth == null) return;

        coreHealth.OnHealthChanged += HandleHealthChanged;
        coreHealth.OnDied += HandleCoreDied;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (coreHealth != null)
        {
            coreHealth.OnHealthChanged -= HandleHealthChanged;
            coreHealth.OnDied -= HandleCoreDied;
        }
        _subscribed = false;
    }

    private void HandleHealthChanged(int current, int max)
    {
        OnCoreHealthChanged?.Invoke(current, max);
    }

    private void PushHealthChanged()
    {
        if (coreHealth == null) return;
        OnCoreHealthChanged?.Invoke(coreHealth.currentHP, coreHealth.maxHP);
    }

    private void HandleCoreDied()
    {
        if (showBannerOnDestroyed && banner != null && !string.IsNullOrWhiteSpace(bannerText))
            banner.Show(bannerText);

        OnCoreDestroyed?.Invoke();

        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.TriggerDefeat(defeatReason);
    }

    public Vector3 Position => targetPoint != null ? targetPoint.position : transform.position;
}