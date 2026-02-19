using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/SFX Library", fileName = "SFXLibrary")]
public class SfxLibrarySO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public SfxId id;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Min(0f)] public float minInterval = 0f;
        public Vector2 pitchRange = new Vector2(1f, 1f);
        [Range(0f, 1f)] public float spatialBlend = 0f;
    }

    public Entry[] entries = Array.Empty<Entry>();

    private Dictionary<SfxId, Entry> _map;

    public bool TryGet(SfxId id, out Entry entry)
    {
        if (_map == null) BuildMap();
        return _map.TryGetValue(id, out entry);
    }

    private void BuildMap()
    {
        _map = new Dictionary<SfxId, Entry>();
        if (entries == null) return;

        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            _map[e.id] = e;
        }
    }

    private void OnEnable()
    {
        BuildMap();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        BuildMap();
    }
#endif
}
