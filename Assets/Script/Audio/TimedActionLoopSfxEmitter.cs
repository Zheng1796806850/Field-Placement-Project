using UnityEngine;

[DisallowMultipleComponent]
public class TimedActionLoopSfxEmitter : MonoBehaviour
{
    public AudioSource audioSource;

    public bool useLibrary = true;
    public SfxId sfxId = SfxId.Action_BuildLoop;
    public float volumeMultiplier = 1f;

    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    public Vector2 pitchRange = new Vector2(1f, 1f);
    [Range(0f, 1f)] public float spatialBlend = 0f;

    public bool loop = true;
    public bool stopOnDisable = true;

    private void Awake()
    {
        EnsureSource();
        ApplySourceSettings();
    }

    private void OnEnable()
    {
        EnsureSource();
        ApplySourceSettings();
    }

    private void OnDisable()
    {
        if (stopOnDisable) StopLoop();
    }

    private void EnsureSource()
    {
        if (audioSource != null) return;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void ApplySourceSettings()
    {
        if (audioSource == null) return;
        audioSource.loop = loop;
        audioSource.spatialBlend = Mathf.Clamp01(spatialBlend);
    }

    public void PlayLoop()
    {
        PlayLoop(sfxId);
    }

    public void PlayLoop(SfxId id)
    {
        EnsureSource();
        ApplySourceSettings();

        sfxId = id;

        AudioClip clip = null;
        float vol = volume;
        Vector2 pr = pitchRange;
        float sb = spatialBlend;

        if (useLibrary && SfxPlayer.Instance != null && SfxPlayer.Instance.TryGetEntry(id, out var entry) && entry != null)
        {
            if (entry.clips != null && entry.clips.Length > 0)
                clip = entry.clips.Length == 1 ? entry.clips[0] : entry.clips[Random.Range(0, entry.clips.Length)];

            vol = entry.volume;
            pr = entry.pitchRange;
            sb = entry.spatialBlend;
        }

        if (clip == null)
        {
            if (clips == null || clips.Length == 0) return;
            clip = clips.Length == 1 ? clips[0] : clips[Random.Range(0, clips.Length)];
            if (clip == null) return;
        }

        float pMin = pr.x <= 0f ? 0.01f : pr.x;
        float pMax = pr.y <= 0f ? 0.01f : pr.y;
        if (pMax < pMin) { float t = pMin; pMin = pMax; pMax = t; }
        float pitch = (pMin == pMax) ? pMin : Random.Range(pMin, pMax);

        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.spatialBlend = Mathf.Clamp01(sb);
        audioSource.volume = Mathf.Clamp01(vol * Mathf.Max(0f, volumeMultiplier));
        audioSource.pitch = pitch;

        if (!audioSource.isPlaying) audioSource.Play();
    }

    public void StopLoop()
    {
        if (audioSource == null) return;
        if (audioSource.isPlaying) audioSource.Stop();
        audioSource.clip = null;
    }

    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
}
