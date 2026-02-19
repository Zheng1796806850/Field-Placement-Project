using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SfxPlayer : MonoBehaviour
{
    public static SfxPlayer Instance { get; private set; }

    [Header("Library")]
    public SfxLibrarySO library;

    [Header("Pool")]
    [Min(1)] public int initialPoolSize = 12;
    public bool dontDestroyOnLoad = true;

    private readonly List<AudioSource> _pool = new List<AudioSource>();
    private readonly Dictionary<SfxId, float> _lastPlayTime = new Dictionary<SfxId, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        WarmPool();
    }

    private void WarmPool()
    {
        for (int i = _pool.Count; i < initialPoolSize; i++)
            _pool.Add(CreateSource(i));
    }

    private AudioSource CreateSource(int index)
    {
        var go = new GameObject($"SFX_{index:00}");
        go.transform.SetParent(transform, false);

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = 15f;
        return src;
    }

    public static void TryPlay(SfxId id, Vector3 position)
    {
        if (Instance == null) return;
        Instance.Play(id, position);
    }

    public void Play(SfxId id, Vector3 position)
    {
        if (library == null) return;
        if (!library.TryGet(id, out var entry) || entry == null) return;
        if (entry.clips == null || entry.clips.Length == 0) return;

        float now = Time.unscaledTime;
        if (entry.minInterval > 0f && _lastPlayTime.TryGetValue(id, out float last))
        {
            if (now - last < entry.minInterval) return;
        }
        _lastPlayTime[id] = now;

        var src = GetFreeSource();
        if (src == null) return;

        src.transform.position = position;
        src.spatialBlend = Mathf.Clamp01(entry.spatialBlend);
        src.volume = Mathf.Clamp01(entry.volume);

        float pMin = entry.pitchRange.x;
        float pMax = entry.pitchRange.y;
        if (pMin <= 0f) pMin = 0.01f;
        if (pMax <= 0f) pMax = 0.01f;
        if (pMax < pMin) { float t = pMin; pMin = pMax; pMax = t; }
        src.pitch = (pMin == pMax) ? pMin : Random.Range(pMin, pMax);

        AudioClip clip = entry.clips.Length == 1 ? entry.clips[0] : entry.clips[Random.Range(0, entry.clips.Length)];
        src.clip = clip;
        src.loop = false;
        src.Play();
    }

    public static void TryPlayClip(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float spatialBlend = 0f)
    {
        if (Instance == null) return;
        Instance.PlayClip(clip, position, volume, pitch, spatialBlend);
    }

    public void PlayClip(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float spatialBlend = 0f)
    {
        if (clip == null) return;

        var src = GetFreeSource();
        if (src == null) return;

        src.transform.position = position;
        src.spatialBlend = Mathf.Clamp01(spatialBlend);
        src.volume = Mathf.Clamp01(volume);
        src.pitch = pitch <= 0f ? 0.01f : pitch;
        src.clip = clip;
        src.loop = false;
        src.Play();
    }

    private AudioSource GetFreeSource()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            var src = _pool[i];
            if (src == null) continue;
            if (!src.isPlaying) return src;
        }

        var extra = CreateSource(_pool.Count);
        _pool.Add(extra);
        return extra;
    }

    public bool TryGetEntry(SfxId id, out SfxLibrarySO.Entry entry)
    {
        entry = null;
        if (library == null) return false;
        return library.TryGet(id, out entry);
    }

    public AudioClip PickClip(SfxId id)
    {
        if (library == null) return null;
        if (!library.TryGet(id, out var entry) || entry == null) return null;
        if (entry.clips == null || entry.clips.Length == 0) return null;
        return entry.clips.Length == 1 ? entry.clips[0] : entry.clips[Random.Range(0, entry.clips.Length)];
    }
}
