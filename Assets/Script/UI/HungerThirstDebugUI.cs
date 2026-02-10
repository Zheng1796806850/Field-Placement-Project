using TMPro;
using UnityEngine;

public class HungerThirstDebugUI : MonoBehaviour
{
    public TextMeshProUGUI label;
    public PlayerHungerThirst system;

    private float _moveMult = 1f;
    private float _atkMult = 1f;

    private void Reset()
    {
        label = GetComponent<TextMeshProUGUI>();
    }

    private void Awake()
    {
        if (system == null)
            system = FindFirstObjectByType<PlayerHungerThirst>();
    }

    private void OnEnable()
    {
        if (system == null)
            system = FindFirstObjectByType<PlayerHungerThirst>();

        if (system != null)
        {
            system.OnHungerChanged += HandleChanged;
            system.OnThirstChanged += HandleChanged;
            system.OnDebuffChanged += HandleDebuff;
        }
    }

    private void OnDisable()
    {
        if (system != null)
        {
            system.OnHungerChanged -= HandleChanged;
            system.OnThirstChanged -= HandleChanged;
            system.OnDebuffChanged -= HandleDebuff;
        }
    }

    private void HandleChanged(float _, float __) => Refresh();
    private void HandleDebuff(float m, float a) { _moveMult = m; _atkMult = a; Refresh(); }

    private void Start() => Refresh();

    private void Refresh()
    {
        if (label == null || system == null) return;

        float h = system.Hunger;
        float hm = system.hungerMax;
        float t = system.Thirst;
        float tm = system.thirstMax;

        label.text =
            $"<b>Hunger / Thirst</b>\n" +
            $"Hunger: {h:0}/{hm:0} ({system.Hunger01:P0})\n" +
            $"Thirst: {t:0}/{tm:0} ({system.Thirst01:P0})\n" +
            $"Move Mult: {_moveMult:0.00}\n" +
            $"Attack Mult: {_atkMult:0.00}\n" +
            $"[1] Eat Food  |  [2] Drink Water";
    }
}
