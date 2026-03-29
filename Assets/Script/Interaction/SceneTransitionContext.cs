using UnityEngine;

public static class SceneTransitionContext
{
    public static bool HasPendingEntryRoute { get; private set; }
    public static string EntryPointId { get; private set; } = "";

    public static bool HasClockSnapshot { get; private set; }
    public static DayNightPhase Phase { get; private set; } = DayNightPhase.Day;
    public static float PhaseTimeRemaining { get; private set; }
    public static float PhaseElapsed { get; private set; }

    public static bool HasPlayerVitalsSnapshot { get; private set; }
    public static int HealthCurrent { get; private set; } = -1;
    public static float Hunger { get; private set; } = -1f;
    public static float Thirst { get; private set; } = -1f;
    public static bool ForceInvokePhaseStartEventOnRestore { get; private set; }

    public static void Prepare(string entryPointId, bool carryClock, bool carryPlayerVitals)
    {
        HasPendingEntryRoute = !string.IsNullOrWhiteSpace(entryPointId);
        EntryPointId = entryPointId ?? "";

        ClearClockSnapshot();
        ClearPlayerVitalsSnapshot();
        ForceInvokePhaseStartEventOnRestore = false;

        if (carryClock)
            CaptureClockSnapshot();

        if (carryPlayerVitals)
            CapturePlayerVitalsSnapshot();
    }

    public static bool TryGetEntryPointId(out string entryPointId)
    {
        entryPointId = EntryPointId;
        return HasPendingEntryRoute && !string.IsNullOrWhiteSpace(entryPointId);
    }

    public static bool TryGetClockSnapshot(out DayNightPhase phase, out float timeRemaining, out float elapsed)
    {
        phase = Phase;
        timeRemaining = PhaseTimeRemaining;
        elapsed = PhaseElapsed;
        return HasClockSnapshot;
    }

    public static bool TryGetPlayerVitalsSnapshot(out int healthCurrent, out float hunger, out float thirst)
    {
        healthCurrent = HealthCurrent;
        hunger = Hunger;
        thirst = Thirst;
        return HasPlayerVitalsSnapshot;
    }

    public static void Clear()
    {
        ClearEntryRoute();
        ClearClockSnapshot();
        ClearPlayerVitalsSnapshot();
        ForceInvokePhaseStartEventOnRestore = false;
    }

    public static void ClearEntryRoute()
    {
        HasPendingEntryRoute = false;
        EntryPointId = "";
    }

    public static void ClearPlayerVitals()
    {
        ClearPlayerVitalsSnapshot();
    }

    public static void RequestPhaseStartEventOnRestore()
    {
        ForceInvokePhaseStartEventOnRestore = true;
    }

    private static void CaptureClockSnapshot()
    {
        var gsm = GameStateManager.Instance != null ? GameStateManager.Instance : Object.FindFirstObjectByType<GameStateManager>();
        if (gsm == null)
            return;

        HasClockSnapshot = true;
        Phase = gsm.CurrentPhase;
        PhaseTimeRemaining = Mathf.Max(0f, gsm.PhaseTimeRemaining);
        PhaseElapsed = Mathf.Max(0f, gsm.PhaseElapsed);
    }

    private static void CapturePlayerVitalsSnapshot()
    {
        Transform player = ResolvePlayerTransform();
        Health health = null;
        PlayerHungerThirst vitals = null;

        if (player != null)
        {
            health = player.GetComponent<Health>();
            if (health == null)
                health = player.GetComponentInChildren<Health>(true);

            vitals = player.GetComponent<PlayerHungerThirst>();
            if (vitals == null)
                vitals = player.GetComponentInChildren<PlayerHungerThirst>(true);
        }

        HasPlayerVitalsSnapshot = health != null || vitals != null;

        if (health != null)
            HealthCurrent = Mathf.Clamp(health.currentHP, 0, health.maxHP);

        if (vitals != null)
        {
            Hunger = vitals.Hunger;
            Thirst = vitals.Thirst;
        }
    }

    private static void ClearClockSnapshot()
    {
        HasClockSnapshot = false;
        Phase = DayNightPhase.Day;
        PhaseTimeRemaining = 0f;
        PhaseElapsed = 0f;
    }

    private static void ClearPlayerVitalsSnapshot()
    {
        HasPlayerVitalsSnapshot = false;
        HealthCurrent = -1;
        Hunger = -1f;
        Thirst = -1f;
    }

    private static Transform ResolvePlayerTransform()
    {
        GameObject byTag = GameObject.FindGameObjectWithTag("Player");
        if (byTag != null)
            return byTag.transform;

        var movement = Object.FindFirstObjectByType<PlayerMovementController>(FindObjectsInactive.Exclude);
        if (movement != null)
            return movement.transform;

        var vitals = Object.FindFirstObjectByType<PlayerHungerThirst>(FindObjectsInactive.Exclude);
        if (vitals != null)
            return vitals.transform.root;

        return null;
    }
}
