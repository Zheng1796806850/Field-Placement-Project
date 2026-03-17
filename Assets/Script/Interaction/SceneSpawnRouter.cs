using System.Collections;
using UnityEngine;

public class SceneSpawnRouter : MonoBehaviour
{
    [Header("Player")]
    public Transform playerOverride;
    public string playerTag = "Player";
    public bool searchByTagFirst = true;
    [Min(1)] public int resolveFrames = 5;

    [Header("Routing")]
    public bool applyOnStart = true;
    public string defaultEntryPointId = "";
    public bool useFallbackEntryPoint = true;
    public bool preservePlayerZ = true;
    public bool clearTransitionContextAfterApply = true;

    [Header("State Restore")]
    public bool restorePlayerVitalsFromTransition = true;

    private bool applied;

    private void Start()
    {
        if (applyOnStart)
            StartCoroutine(ApplyRoutine());
    }

    private IEnumerator ApplyRoutine()
    {
        int tries = Mathf.Max(1, resolveFrames);

        for (int i = 0; i < tries; i++)
        {
            if (TryApply())
                yield break;

            yield return null;
        }

        if (clearTransitionContextAfterApply)
            SceneTransitionContext.Clear();
    }

    public bool TryApply()
    {
        if (applied)
            return true;

        Transform player = ResolvePlayer();
        if (player == null)
            return false;

        SceneEntryPoint entryPoint = ResolveEntryPoint();
        if (entryPoint != null)
        {
            Vector3 pos = entryPoint.transform.position;
            if (preservePlayerZ)
                pos.z = player.position.z;
            player.position = pos;
        }

        if (restorePlayerVitalsFromTransition)
            RestoreVitals(player);

        applied = true;

        if (clearTransitionContextAfterApply)
            SceneTransitionContext.Clear();

        return true;
    }

    private Transform ResolvePlayer()
    {
        if (playerOverride != null)
            return playerOverride;

        if (searchByTagFirst && !string.IsNullOrWhiteSpace(playerTag))
        {
            GameObject byTag = GameObject.FindGameObjectWithTag(playerTag);
            if (byTag != null)
                return byTag.transform;
        }

        var movement = Object.FindFirstObjectByType<PlayerMovementController>();
        if (movement != null)
            return movement.transform;

        var health = Object.FindFirstObjectByType<Health>();
        if (health != null)
            return health.transform;

        return null;
    }

    private SceneEntryPoint ResolveEntryPoint()
    {
        SceneEntryPoint[] entryPoints = Object.FindObjectsByType<SceneEntryPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (entryPoints == null || entryPoints.Length == 0)
            return null;

        string requestedId = "";
        bool hasRequestedId = SceneTransitionContext.TryGetEntryPointId(out requestedId);

        if ((!hasRequestedId || string.IsNullOrWhiteSpace(requestedId)) && !string.IsNullOrWhiteSpace(defaultEntryPointId))
        {
            requestedId = defaultEntryPointId;
            hasRequestedId = true;
        }

        if (hasRequestedId)
        {
            for (int i = 0; i < entryPoints.Length; i++)
            {
                if (entryPoints[i] != null && entryPoints[i].entryPointId == requestedId)
                    return entryPoints[i];
            }
        }

        if (useFallbackEntryPoint)
        {
            for (int i = 0; i < entryPoints.Length; i++)
            {
                if (entryPoints[i] != null && entryPoints[i].fallbackIfNoRoute)
                    return entryPoints[i];
            }
        }

        if (entryPoints.Length == 1)
            return entryPoints[0];

        return null;
    }

    private void RestoreVitals(Transform player)
    {
        if (!SceneTransitionContext.TryGetPlayerVitalsSnapshot(out int healthCurrent, out float hunger, out float thirst))
            return;

        Health health = player.GetComponent<Health>();
        if (health == null)
            health = player.GetComponentInChildren<Health>();
        if (health == null)
            health = Object.FindFirstObjectByType<Health>();

        if (health != null && healthCurrent >= 0)
            health.SetCurrentHP(healthCurrent, true);

        PlayerHungerThirst vitals = player.GetComponent<PlayerHungerThirst>();
        if (vitals == null)
            vitals = player.GetComponentInChildren<PlayerHungerThirst>();
        if (vitals == null)
            vitals = Object.FindFirstObjectByType<PlayerHungerThirst>();

        if (vitals != null)
        {
            if (hunger >= 0f)
                vitals.SetHunger(hunger);

            if (thirst >= 0f)
                vitals.SetThirst(thirst);
        }
    }
}
