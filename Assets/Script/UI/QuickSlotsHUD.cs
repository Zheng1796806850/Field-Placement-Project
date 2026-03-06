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
        public Image cooldownFill;
        public TextMeshProUGUI countLabel;
    }

    [Header("Refs")]
    public PlayerHungerThirst controller;

    [Header("UI")]
    public List<SlotUI> slots = new List<SlotUI>();

    [Header("Behavior")]
    public bool hideIconWhenEmpty = true;
    public bool showCountWhenZero = false;
    public string countPrefix = "";

    private void Awake()
    {
        ResolveController();
        RefreshStatic();
        RefreshDynamic();
    }

    private void OnEnable()
    {
        ResolveController();
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

    private void ResolveController()
    {
        if (controller == null) controller = FindFirstObjectByType<PlayerHungerThirst>(FindObjectsInactive.Include);
    }

    private void RefreshStatic()
    {
        if (controller == null) return;

        int max = Mathf.Min(slots.Count, controller.QuickSlotCount);
        for (int i = 0; i < max; i++)
        {
            var ui = slots[i];
            var item = controller.GetQuickSlotItem(i);

            if (ui == null) continue;

            if (ui.icon != null)
            {
                ui.icon.sprite = item != null ? item.icon : null;
                ui.icon.enabled = !hideIconWhenEmpty || (item != null && ui.icon.sprite != null);
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

            if (ui.highlight != null) ui.highlight.enabled = (i == selected);

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
                var item = controller.GetQuickSlotItem(i);
                if (item == null)
                {
                    ui.countLabel.text = "";
                }
                else
                {
                    int count = controller.GetQuickSlotAvailableCount(i);
                    ui.countLabel.text = (count > 0 || showCountWhenZero) ? $"{countPrefix}{count}" : "";
                }
            }
        }
    }
}