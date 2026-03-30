using UnityEngine;

public class MoveKeyHoldObjective : TutorialObjective
{
    [Min(0.05f)] public float requiredHoldSecondsPerKey = 0.35f;
    public KeyCode keyW = KeyCode.W;
    public KeyCode keyA = KeyCode.A;
    public KeyCode keyS = KeyCode.S;
    public KeyCode keyD = KeyCode.D;

    private float _w;
    private float _a;
    private float _s;
    private float _d;

    protected override void OnBegin()
    {
        _w = _a = _s = _d = 0f;
    }

    private void Update()
    {
        if (IsCompleted) return;

        float dt = Time.unscaledDeltaTime;
        if (Input.GetKey(keyW)) _w += dt;
        if (Input.GetKey(keyA)) _a += dt;
        if (Input.GetKey(keyS)) _s += dt;
        if (Input.GetKey(keyD)) _d += dt;

        if (_w >= requiredHoldSecondsPerKey &&
            _a >= requiredHoldSecondsPerKey &&
            _s >= requiredHoldSecondsPerKey &&
            _d >= requiredHoldSecondsPerKey)
        {
            Complete();
        }
    }

    public override string GetProgressText()
    {
        string req = $"{requiredHoldSecondsPerKey:F1}s";
        return
            $"W: {_w:F1}s/{req}\n" +
            $"A: {_a:F1}s/{req}\n" +
            $"S: {_s:F1}s/{req}\n" +
            $"D: {_d:F1}s/{req}";
    }
}
