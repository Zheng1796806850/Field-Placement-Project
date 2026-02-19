using UnityEngine;

[DisallowMultipleComponent]
public class EnemyDeathSFX2D : MonoBehaviour
{
    public Health health;

    [Header("Clip List Override")]
    public AudioClip[] deathClips;
    [Range(0f, 1f)] public float volume = 1f;
    public Vector2 pitchRange = new Vector2(1f, 1f);
    [Range(0f, 1f)] public float spatialBlend = 0f;
    [Min(0f)] public float minInterval = 0.03f;

    [Header("Fallback (Library)")]
    public SfxId deathSfxId = SfxId.Combat_EnemyDeath;

    private bool _subscribed;
    private float _lastPlayTime;

    private void Awake()
    {
        if (health == null) health = GetComponentInChildren<Health>();
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        if (health == null) return;

        health.OnDied += HandleDied;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (health != null) health.OnDied -= HandleDied;
        _subscribed = false;
    }

    private void HandleDied()
    {
        float now = Time.unscaledTime;
        if (minInterval > 0f && now - _lastPlayTime < minInterval) return;
        _lastPlayTime = now;

        if (deathClips != null && deathClips.Length > 0)
        {
            var clip = deathClips.Length == 1 ? deathClips[0] : deathClips[Random.Range(0, deathClips.Length)];
            if (clip == null) return;

            float pMin = pitchRange.x <= 0f ? 0.01f : pitchRange.x;
            float pMax = pitchRange.y <= 0f ? 0.01f : pitchRange.y;
            if (pMax < pMin) { float t = pMin; pMin = pMax; pMax = t; }
            float pitch = (pMin == pMax) ? pMin : Random.Range(pMin, pMax);

            SfxPlayer.TryPlayClip(clip, transform.position, volume, pitch, spatialBlend);
            return;
        }

        SfxPlayer.TryPlay(deathSfxId, transform.position);
    }
}
