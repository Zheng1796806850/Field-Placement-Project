using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WaterCollectorStatusHUD : MonoBehaviour
{
    [Header("Refs")]
    public WaterCollectorBuildSpot collector;

    [Header("UI")]
    public CanvasGroup canvasGroup;
    public TextMeshProUGUI storedLabel;
    public TextMeshProUGUI nextTickLabel;
    public TextMeshProUGUI durabilityLabel;
    public Image durabilityFill;
    public GameObject lowDurabilityWarningRoot;

    [Header("Behavior")]
    public bool hideWhenUnbuilt = true;
    public bool faceMainCamera = false;
    public bool showDurabilityText = false;
    public string storedPrefix = "Water ";
    public string nextTickPrefix = "Next ";
    public string durabilityPrefix = "Durability ";

    private void Awake()
    {
        if (collector == null)
            collector = GetComponentInParent<WaterCollectorBuildSpot>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        PushAll();
    }

    private void OnEnable()
    {
        if (collector == null)
            collector = GetComponentInParent<WaterCollectorBuildSpot>();

        if (collector != null)
        {
            collector.OnBuiltChanged += HandleBuiltChanged;
            collector.OnStoredWaterChanged += HandleStoredChanged;
            collector.OnDurabilityChanged += HandleDurabilityChanged;
            collector.OnLowDurabilityChanged += HandleLowChanged;
        }

        PushAll();
    }

    private void OnDisable()
    {
        if (collector != null)
        {
            collector.OnBuiltChanged -= HandleBuiltChanged;
            collector.OnStoredWaterChanged -= HandleStoredChanged;
            collector.OnDurabilityChanged -= HandleDurabilityChanged;
            collector.OnLowDurabilityChanged -= HandleLowChanged;
        }
    }

    private void Update()
    {
        if (faceMainCamera && Camera.main != null)
            transform.forward = Camera.main.transform.forward;

        RefreshNextTick();
    }

    private void HandleBuiltChanged(bool built)
    {
        ApplyVisibility();
        PushStored();
        PushDurability();
        RefreshNextTick();
    }

    private void HandleStoredChanged(int current, int max)
    {
        PushStored();
        RefreshNextTick();
    }

    private void HandleDurabilityChanged(int current, int max)
    {
        PushDurability();
        RefreshNextTick();
    }

    private void HandleLowChanged(bool low)
    {
        if (lowDurabilityWarningRoot != null)
            lowDurabilityWarningRoot.SetActive(low);
    }

    private void PushAll()
    {
        ApplyVisibility();
        PushStored();
        PushDurability();
        RefreshNextTick();
    }

    private void ApplyVisibility()
    {
        bool visible = true;
        if (hideWhenUnbuilt && collector != null)
            visible = collector.IsBuilt;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void PushStored()
    {
        if (storedLabel == null || collector == null) return;
        storedLabel.text = $"{storedPrefix}{collector.StoredWater}/{collector.StorageCap}";
    }

    private void PushDurability()
    {
        if (collector == null) return;

        if (durabilityFill != null)
            durabilityFill.fillAmount = collector.CurrentDurability / (float)Mathf.Max(1, collector.MaxDurability);

        if (durabilityLabel != null)
            durabilityLabel.text = showDurabilityText ? $"{durabilityPrefix}{collector.CurrentDurability}/{collector.MaxDurability}" : "";

        if (lowDurabilityWarningRoot != null)
            lowDurabilityWarningRoot.SetActive(collector.IsLowDurability || collector.IsBroken);
    }

    private void RefreshNextTick()
    {
        if (nextTickLabel == null || collector == null) return;
        nextTickLabel.text = $"{nextTickPrefix}{collector.GetNextTickText()}";
    }
}