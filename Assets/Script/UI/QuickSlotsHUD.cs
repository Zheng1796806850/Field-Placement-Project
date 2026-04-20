using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuickSlotsHUD : MonoBehaviour
{
    [Serializable]
    public class SlotUI
    {
        public Image icon;
        public Image highlight;
        public Image modeHighlight;
        public Image cooldownFill;
        public TextMeshProUGUI countLabel;
    }

    [Header("Refs")]
    public PlayerHungerThirst controller;

    [Header("UI")]
    public List<SlotUI> slots = new List<SlotUI>();

    [Header("Mode Highlight")]
    public bool autoCreateModeHighlightIfMissing = true;
    public Color modeHighlightColor = new Color(0.35f, 1f, 0.35f, 0.42f);
    [Min(0f)] public float modeHighlightScale = 1.08f;

    [Header("Behavior")]
    public bool hideIconWhenEmpty = true;
    public bool showCountWhenZero = false;
    public string countPrefix = "";

    private void Awake()
    {
        EnsureBridgeExists();
        ResolveController();
        EnsureModeHighlightBindings();
        RefreshStatic();
        RefreshDynamic();
        ClearModeHighlights();
    }

    private void OnEnable()
    {
        EnsureBridgeExists();
        ResolveController();
        EnsureModeHighlightBindings();
        if (controller != null) controller.OnQuickSlotsLayoutChanged += RefreshStatic;
        RefreshStatic();
        RefreshDynamic();
    }

    private void OnDisable()
    {
        if (controller != null) controller.OnQuickSlotsLayoutChanged -= RefreshStatic;
    }

    private void Update()
    {
        ResolveController();
        RefreshDynamic();
    }

    public void SetModeHighlight(int slotIndex, bool on)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return;

        var ui = slots[slotIndex];
        if (ui == null)
            return;

        EnsureModeHighlightBinding(slotIndex, ui);
        if (ui.modeHighlight == null)
            return;

        ui.modeHighlight.enabled = on;
    }

    public void ClearModeHighlights()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var ui = slots[i];
            if (ui == null || ui.modeHighlight == null)
                continue;

            ui.modeHighlight.enabled = false;
        }
    }

    private void ResolveController()
    {
        if (controller == null) controller = FindFirstObjectByType<PlayerHungerThirst>(FindObjectsInactive.Include);
    }

    private void EnsureBridgeExists()
    {
        if (GetComponent<GameplayModeQuickSlotHighlightBridge>() == null)
            gameObject.AddComponent<GameplayModeQuickSlotHighlightBridge>();
    }

    private void EnsureModeHighlightBindings()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var ui = slots[i];
            if (ui == null)
                continue;

            EnsureModeHighlightBinding(i, ui);
        }
    }

    private void EnsureModeHighlightBinding(int slotIndex, SlotUI ui)
    {
        if (ui.modeHighlight != null || !autoCreateModeHighlightIfMissing)
            return;

        RectTransform host = null;
        if (ui.icon != null)
            host = ui.icon.transform.parent as RectTransform;
        if (host == null && ui.cooldownFill != null)
            host = ui.cooldownFill.transform.parent as RectTransform;
        if (host == null)
            return;

        string overlayName = $"ModeHighlight_{slotIndex + 1:00}";
        Transform existing = host.Find(overlayName);
        Image img;

        if (existing != null)
        {
            img = existing.GetComponent<Image>();
            if (img == null)
                img = existing.gameObject.AddComponent<Image>();
        }
        else
        {
            var go = new GameObject(overlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(host, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = host.sizeDelta * Mathf.Max(1f, modeHighlightScale);
            rt.SetAsFirstSibling();

            img = go.GetComponent<Image>();
        }

        if (img != null)
        {
            img.raycastTarget = false;
            img.color = modeHighlightColor;
            img.enabled = false;
            ui.modeHighlight = img;
        }
    }

    private void RefreshStatic()
    {
        if (controller == null) return;

        int max = Mathf.Min(slots.Count, controller.QuickSlotCount);
        for (int i = 0; i < max; i++)
        {
            var ui = slots[i];
            if (ui == null) continue;

            Sprite icon = controller.GetQuickSlotIcon(i);

            if (ui.icon != null)
            {
                ui.icon.sprite = icon;
                ui.icon.enabled = !hideIconWhenEmpty || icon != null;
            }
        }
    }

    private void RefreshDynamic()
    {
        if (controller == null) return;

        int max = Mathf.Min(slots.Count, controller.QuickSlotCount);
        int selected = controller.QuickSlotsSelectedIndex;

        for (int i = 0; i < max; i++)
        {
            var ui = slots[i];
            if (ui == null) continue;

            if (ui.highlight != null) ui.highlight.enabled = i == selected;

            float cd = controller.GetQuickSlotCooldownRemaining(i);
            float dur = controller.GetQuickSlotCooldownDuration(i);
            if (ui.cooldownFill != null)
            {
                if (dur <= 0f) ui.cooldownFill.fillAmount = 0f;
                else ui.cooldownFill.fillAmount = Mathf.Clamp01(cd / dur);
                ui.cooldownFill.enabled = cd > 0f;
            }

            if (ui.countLabel != null)
            {
                int count = controller.GetQuickSlotAvailableCount(i);
                ui.countLabel.text = (count > 0 || showCountWhenZero) ? $"{countPrefix}{count}" : "";
            }
        }
    }
}
