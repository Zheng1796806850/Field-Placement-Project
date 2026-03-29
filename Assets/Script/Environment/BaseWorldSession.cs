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
}
