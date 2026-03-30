using System.Collections.Generic;
using UnityEngine;

public class TutorialSceneBootstrap : MonoBehaviour
{
    [Header("Disable Runtime Systems")]
    public bool disableWaveSystems = true;
    public bool disableGameFlow = true;
    public bool disableTownNightReturn = true;
    public bool disableAllZoneTeleports = true;

    [Header("Allow Specific Teleports")]
    public List<ZoneTeleportTrigger2D> allowTeleports = new List<ZoneTeleportTrigger2D>();

    [Header("Extra Objects")]
    public List<Behaviour> extraBehavioursToDisable = new List<Behaviour>();
    public List<GameObject> extraObjectsToDisable = new List<GameObject>();

    private void Awake()
    {
        if (disableWaveSystems)
        {
            DisableAll<WaveProgressTracker>();
            DisableAll<WaveSpawnController2D>();
        }

        if (disableGameFlow)
            DisableAll<GameFlowManager>();

        if (disableTownNightReturn)
            DisableAll<TownNightReturnController>();

        if (disableAllZoneTeleports)
            DisableTeleportsExceptAllowed();

        for (int i = 0; i < extraBehavioursToDisable.Count; i++)
        {
            if (extraBehavioursToDisable[i] != null)
                extraBehavioursToDisable[i].enabled = false;
        }

        for (int i = 0; i < extraObjectsToDisable.Count; i++)
        {
            if (extraObjectsToDisable[i] != null)
                extraObjectsToDisable[i].SetActive(false);
        }
    }

    private void DisableAll<T>() where T : Behaviour
    {
        var all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null)
                all[i].enabled = false;
        }
    }

    private void DisableTeleportsExceptAllowed()
    {
        var all = FindObjectsByType<ZoneTeleportTrigger2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (allowTeleports != null && allowTeleports.Contains(t)) continue;
            t.enabled = false;
        }
    }
}
