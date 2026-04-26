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
    public bool useUnscaledTime = false;

    [Header("Start Phase")]
    public DayNightPhase startPhase = DayNightPhase.Day;

    [Header("Transition Restore")]
    public bool restorePhaseFromSceneTransition = true;
    [Tooltip("If false, OnDayStarted/OnNightStarted are not fired when restoring phase from scene transition (avoids double-advancing crops/waves).")]
    public bool invokePhaseEventsOnRestore = false;

    [Header("Debug Hotkeys")]
    public bool enableDebugHotkeys = true;
    public KeyCode togglePhaseKey = KeyCode.F1;
    public KeyCode pauseKey = KeyCode.F2;
    public bool logPauseStateChanges = false;

    public DayNightPhase CurrentPhase { get; private set; }
    public int CurrentDay { get; private set; } = 1;
    public float PhaseTimeRemaining { get; private set; }
    public float PhaseElapsed { get; private set; }
    public bool IsPaused { get; private set; }

    /// <summary>为 true 时昼夜相位倒计时与相位切换检测不推进（与 <see cref="IsPaused"/> 独立；不要用暂停菜单代替此语义）。</summary>
    public bool StoryClockFrozen { get; private set; }

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
        if (restorePhaseFromSceneTransition && SceneTransitionContext.TryGetClockSnapshot(out var phase, out var timeRemaining, out var elapsed, out var day))
        {
            bool invokePhaseStartEvents = invokePhaseEventsOnRestore || SceneTransitionContext.ForceInvokePhaseStartEventOnRestore;
            ApplyPhaseState(phase, timeRemaining, elapsed, day, invokePhaseStartEvents);
            return;
        }

        CurrentDay = Mathf.Max(1, CurrentDay);
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
        if (StoryClockFrozen) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        PhaseTimeRemaining -= dt;
        PhaseElapsed += dt;

        if (PhaseTimeRemaining <= 0f)
        {
            if (CurrentPhase == DayNightPhase.Day) SetPhase(DayNightPhase.Night);
            else SetPhase(DayNightPhase.Day);
        }
    }

    public void SetStoryClockFrozen(bool frozen)
    {
        StoryClockFrozen = frozen;
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

    public void ApplyPhaseState(DayNightPhase phase, float timeRemaining, float elapsed, bool invokeEvents)
    {
        ApplyPhaseState(phase, timeRemaining, elapsed, CurrentDay, invokeEvents);
    }

    public void ApplyPhaseState(DayNightPhase phase, float timeRemaining, float elapsed, int day, bool invokeEvents)
    {
        CurrentPhase = phase;
        CurrentDay = Mathf.Max(1, day);

        float total = phase == DayNightPhase.Day ? dayDuration : nightDuration;
        if (total <= 0f)
            total = 1f;

        // Keep transferred clock values as-is (non-negative) so cross-scene travel
        // preserves exact remaining phase time instead of being capped by local scene defaults.
        PhaseTimeRemaining = Mathf.Max(0f, timeRemaining);
        PhaseElapsed = Mathf.Max(0f, elapsed);

        if (PhaseTimeRemaining <= 0f && PhaseElapsed <= 0f)
        {
            PhaseTimeRemaining = total;
            PhaseElapsed = 0f;
        }
        else if (PhaseTimeRemaining <= 0f)
        {
            PhaseTimeRemaining = Mathf.Max(0f, total - PhaseElapsed);
        }
        else if (PhaseElapsed <= 0f)
        {
            PhaseElapsed = Mathf.Max(0f, total - PhaseTimeRemaining);
        }

        OnPhaseChanged?.Invoke(phase);

        if (!invokeEvents)
            return;

        if (phase == DayNightPhase.Day) OnDayStarted?.Invoke();
        else OnNightStarted?.Invoke();
    }

    private void SetPhaseInternal(DayNightPhase next, bool force)
    {
        if (!force && next == CurrentPhase) return;

        DayNightPhase previous = CurrentPhase;
        CurrentPhase = next;
        PhaseElapsed = 0f;
        PhaseTimeRemaining = (next == DayNightPhase.Day) ? dayDuration : nightDuration;

        OnPhaseChanged?.Invoke(next);

        if (next == DayNightPhase.Day)
        {
            OnDayStarted?.Invoke();
            if (previous == DayNightPhase.Night)
            {
                CurrentDay = Mathf.Max(1, CurrentDay + 1);
                GameplayEventHub.RaiseNightSurvived();
            }
        }
        else
            OnNightStarted?.Invoke();
    }

    public void TogglePause()
    {
        SetPaused(!IsPaused);
    }

    public void SetPaused(bool paused)
    {
        if (logPauseStateChanges && IsPaused != paused)
        {
            Debug.Log($"[GameStateManager] SetPaused({paused}) called.\n{System.Environment.StackTrace}");
        }

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
