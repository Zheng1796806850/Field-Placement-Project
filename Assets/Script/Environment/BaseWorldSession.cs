using System.Collections.Generic;
using UnityEngine;

public static class BaseWorldSession
{
    const string RunGenerationKey = "FGCP_BaseWorldRunGen_v1";

    public static int CurrentRunGeneration => PlayerPrefs.GetInt(RunGenerationKey, 0);

    public static void AdvanceRunGeneration()
    {
        int next = CurrentRunGeneration + 1;
        PlayerPrefs.SetInt(RunGenerationKey, next);
        PlayerPrefs.Save();
    }

    public static string ScopePlayerPrefsKey(string localKey)
    {
        if (string.IsNullOrWhiteSpace(localKey))
            return localKey;

        return $"FGCP_BW_{CurrentRunGeneration}_{localKey}";
    }

    /// <summary>
    /// Deletes scoped keys for every run index up to the current generation (plus a small cushion)
    /// and the legacy unscoped key. Used when starting a new session without full save-game support.
    /// </summary>
    public static void DeleteScopedKeysForLocalKeyAcrossRuns(string localKey)
    {
        if (string.IsNullOrWhiteSpace(localKey))
            return;

        int maxGen = Mathf.Max(CurrentRunGeneration, 0);
        for (int g = 0; g <= maxGen + 16; g++)
        {
            string scoped = $"FGCP_BW_{g}_{localKey}";
            if (PlayerPrefs.HasKey(scoped))
                PlayerPrefs.DeleteKey(scoped);
        }

        if (PlayerPrefs.HasKey(localKey))
            PlayerPrefs.DeleteKey(localKey);
    }

    public static void DeleteWaterCollectorKeysForAllRuns(IReadOnlyList<string> localKeys)
    {
        if (localKeys == null) return;
        for (int i = 0; i < localKeys.Count; i++)
            DeleteScopedKeysForLocalKeyAcrossRuns(localKeys[i]);
        PlayerPrefs.Save();
    }
}
