using UnityEngine;
using UnityEngine.UI;

public class WallDurabilityHUD : MonoBehaviour
{
    [Header("Refs")]
    public WoodenWallDurability wall;
    public Health health;

    [Header("UI")]
    public Image fillImage;
    public Text valueText;
    public GameObject lowWarningRoot;

    [Header("Behavior")]
    public bool showAlways = true;
    public bool hideWhenFull = false;
    [Min(0f)] public float autoHideDelay = 2f;

    [Header("Low Warning")]
    public bool blinkWhenLow = true;
    [Min(0.1f)] public float blinkSpeed = 6f;

    [Header("Billboard")]
    public bool faceMainCamera = false;

    private CanvasGroup _cg;
    private float _hideTimer;

    private void Awake()
    {
        if (wall == null) wall = GetComponentInParent<WoodenWallDurability>();
        if (health == null && wall != null) health = wall.health;
        if (health == null) health = GetComponentInParent<Health>();

        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnHealthChanged += HandleHealthChanged;

        if (wall != null)
            wall.OnStateChanged += HandleWallStateChanged;

        RefreshNow();
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnHealthChanged -= HandleHealthChanged;

        if (wall != null)
            wall.OnStateChanged -= HandleWallStateChanged;
    }

    private void Update()
    {
        if (faceMainCamera && Camera.main != null)
            transform.forward = Camera.main.transform.forward;

        if (wall != null && !wall.IsBuiltState)
        {
            _cg.alpha = 0f;
            return;
        }

        if (!showAlways && !hideWhenFull)
        {
            if (_hideTimer > 0f)
            {
                _hideTimer -= Time.unscaledDeltaTime;
                if (_hideTimer <= 0f)
                    _cg.alpha = 0f;
            }
        }

        if (blinkWhenLow && lowWarningRoot != null && lowWarningRoot.activeSelf)
        {
            float a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * blinkSpeed));
            _cg.alpha = showAlways ? 1f : Mathf.Max(_cg.alpha, a);
        }
    }

    private void HandleWallStateChanged(WallBuildState state)
    {
        RefreshNow();
    }

    private void RefreshNow()
    {
        if (wall != null && !wall.IsBuiltState)
        {
            if (fillImage != null) fillImage.fillAmount = 0f;
            if (valueText != null) valueText.text = "";
            if (lowWarningRoot != null) lowWarningRoot.SetActive(false);
            _cg.alpha = 0f;
            return;
        }

        if (health != null)
            HandleHealthChanged(health.currentHP, health.maxHP);
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (wall != null && !wall.IsBuiltState)
        {
            _cg.alpha = 0f;
            return;
        }

        float frac = max <= 0 ? 0f : Mathf.Clamp01(current / (float)max);

        if (fillImage != null)
            fillImage.fillAmount = frac;

        if (valueText != null)
            valueText.text = $"{current}/{max}";

        bool low = false;
        if (wall != null) low = wall.IsLowDurability;
        else low = frac <= 0.25f;

        if (lowWarningRoot != null)
            lowWarningRoot.SetActive(low);

        if (showAlways)
        {
            _cg.alpha = 1f;
        }
        else if (hideWhenFull)
        {
            _cg.alpha = current >= max ? 0f : 1f;
        }
        else
        {
            _cg.alpha = 1f;
            _hideTimer = autoHideDelay;
        }
    }
}