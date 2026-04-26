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
        var p = new Payload
        {
            version = SaveVersion,
            checkpoint = Mathf.Max(0, checkpoint),
            day2HandoffComplete = day2HandoffComplete
        };
        PlayerPrefs.SetString(ScopedKey, JsonUtility.ToJson(p));
        PlayerPrefs.Save();
    }
}
