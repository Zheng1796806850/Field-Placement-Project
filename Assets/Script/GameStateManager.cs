using System;
using UnityEngine;

public enum DayNightPhase
{
    Day,
    Night
}

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("Cycle Settings (seconds)")]
    [Min(5f)] public float dayDuration = 120f;
    [Min(5f)] public float nightDuration = 120f;

    [Header("Clock Options")]
    [Tooltip("If true, uses unscaled time for the day/night clock (pause won't advance time).")]
    public bool useUnscaledTime = false;

    [Header("Start Phase")]
    public DayNightPhase startPhase = DayNightPhase.Day;

    [Header("Debug Hotkeys")]
    public bool enableDebugHotkeys = true;
    public KeyCode togglePhaseKey = KeyCode.F1;
    public KeyCode pauseKey = KeyCode.F2;

    public DayNightPhase CurrentPhase { get; private set; }
    public float PhaseTimeRemaining { get; private set; }
    public float PhaseElapsed { get; private set; }
    public bool IsPaused { get; private set; }

    public event Action<DayNightPhase> OnPhaseChanged;
    public event Action OnDayStarted;
    public event Action OnNightStarted;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        SetPhaseInternal(startPhase, force: true);
    }

    private void Update()
    {
        if (enableDebugHotkeys)
        {
            if (Input.GetKeyDown(togglePhaseKey)) TogglePhase();
            if (Input.GetKeyDown(pauseKey)) TogglePause();
        }

        if (IsPaused) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        PhaseTimeRemaining -= dt;
        PhaseElapsed += dt;

        if (PhaseTimeRemaining <= 0f)
        {
            if (CurrentPhase == DayNightPhase.Day) SetPhase(DayNightPhase.Night);
            else SetPhase(DayNightPhase.Day);
        }
    }

    public void TogglePhase()
    {
        SetPhase(CurrentPhase == DayNightPhase.Day ? DayNightPhase.Night : DayNightPhase.Day);
    }

    public void SetPhase(DayNightPhase next)
    {
        if (next == CurrentPhase) return;
        SetPhaseInternal(next, force: false);
    }

    public void ForceDay() => SetPhaseInternal(DayNightPhase.Day, force: true);
    public void ForceNight() => SetPhaseInternal(DayNightPhase.Night, force: true);

    private void SetPhaseInternal(DayNightPhase next, bool force)
    {
        if (!force && next == CurrentPhase) return;

        CurrentPhase = next;
        PhaseElapsed = 0f;
        PhaseTimeRemaining = (next == DayNightPhase.Day) ? dayDuration : nightDuration;

        OnPhaseChanged?.Invoke(next);

        if (next == DayNightPhase.Day) OnDayStarted?.Invoke();
        else OnNightStarted?.Invoke();
    }

    public void TogglePause()
    {
        SetPaused(!IsPaused);
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;

        Time.timeScale = paused ? 0f : 1f;
    }

    public float GetPhaseProgress01()
    {
        float total = (CurrentPhase == DayNightPhase.Day) ? dayDuration : nightDuration;
        if (total <= 0.01f) return 1f;
        return Mathf.Clamp01(PhaseElapsed / total);
    }
}
