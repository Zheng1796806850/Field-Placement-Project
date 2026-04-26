using System;
using UnityEngine;

/// <summary>线性剧情检查点持久化（按 <see cref="BaseWorldSession"/> 作用域）。</summary>
public static class StoryProgressStore
{
    const string LocalKey = "linear_story_checkpoint_v1";
    const int SaveVersion = 1;

    [Serializable]
    class Payload
    {
        public int version = SaveVersion;
        public int checkpoint;
        public bool day2HandoffComplete;
        public bool hasTriggeredBackyardPitObservation;
        public bool hasPlayedPitIntroDialogue;
        public bool endingTriggered;
        public int endingType;
    }

    static string ScopedKey => BaseWorldSession.ScopePlayerPrefsKey(LocalKey);

    public static int LoadCheckpoint(int defaultIfMissing = 0)
    {
        if (!PlayerPrefs.HasKey(ScopedKey))
            return defaultIfMissing;

        try
        {
            var p = JsonUtility.FromJson<Payload>(PlayerPrefs.GetString(ScopedKey, ""));
            if (p == null) return defaultIfMissing;
            return Mathf.Max(0, p.checkpoint);
        }
        catch
        {
            return defaultIfMissing;
        }
    }

    public static bool LoadDay2HandoffComplete()
    {
        if (!PlayerPrefs.HasKey(ScopedKey))
            return false;

        try
        {
            var p = JsonUtility.FromJson<Payload>(PlayerPrefs.GetString(ScopedKey, ""));
            return p != null && p.day2HandoffComplete;
        }
        catch
        {
            return false;
        }
    }

    public static void Save(int checkpoint, bool day2HandoffComplete)
    {
        Save(checkpoint, day2HandoffComplete, false, false, false, EndingType.None);
    }

    public static bool LoadEndingTriggered()
    {
        if (!PlayerPrefs.HasKey(ScopedKey))
            return false;

        try
        {
            var p = JsonUtility.FromJson<Payload>(PlayerPrefs.GetString(ScopedKey, ""));
            return p != null && p.endingTriggered;
        }
        catch
        {
            return false;
        }
    }

    public static EndingType LoadEndingType()
    {
        if (!PlayerPrefs.HasKey(ScopedKey))
            return EndingType.None;

        try
        {
            var p = JsonUtility.FromJson<Payload>(PlayerPrefs.GetString(ScopedKey, ""));
            if (p == null)
                return EndingType.None;
            if (!Enum.IsDefined(typeof(EndingType), p.endingType))
                return EndingType.None;
            return (EndingType)p.endingType;
        }
        catch
        {
            return EndingType.None;
        }
    }

    public static bool LoadHasTriggeredBackyardPitObservation()
    {
        if (!PlayerPrefs.HasKey(ScopedKey))
            return false;

        try
        {
            var p = JsonUtility.FromJson<Payload>(PlayerPrefs.GetString(ScopedKey, ""));
            return p != null && p.hasTriggeredBackyardPitObservation;
        }
        catch
        {
            return false;
        }
    }

    public static bool LoadHasPlayedPitIntroDialogue()
    {
        if (!PlayerPrefs.HasKey(ScopedKey))
            return false;

        try
        {
            var p = JsonUtility.FromJson<Payload>(PlayerPrefs.GetString(ScopedKey, ""));
            return p != null && p.hasPlayedPitIntroDialogue;
        }
        catch
        {
            return false;
        }
    }

    public static void Save(int checkpoint, bool day2HandoffComplete, bool endingTriggered, EndingType endingType)
    {
        Save(checkpoint, day2HandoffComplete, false, false, endingTriggered, endingType);
    }

    public static void Save(int checkpoint, bool day2HandoffComplete, bool hasTriggeredBackyardPitObservation, bool hasPlayedPitIntroDialogue, bool endingTriggered, EndingType endingType)
    {
        var p = new Payload
        {
            version = SaveVersion,
            checkpoint = Mathf.Max(0, checkpoint),
            day2HandoffComplete = day2HandoffComplete,
            hasTriggeredBackyardPitObservation = hasTriggeredBackyardPitObservation,
            hasPlayedPitIntroDialogue = hasPlayedPitIntroDialogue,
            endingTriggered = endingTriggered,
            endingType = (int)endingType
        };
        PlayerPrefs.SetString(ScopedKey, JsonUtility.ToJson(p));
        PlayerPrefs.Save();
    }
}
