using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerFootstepSFX2D : MonoBehaviour
{
    public Rigidbody2D rb;

    [Header("Clip List Override")]
    public AudioClip[] footstepClips;
    [Range(0f, 1f)] public float volume = 0.35f;
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [Range(0f, 1f)] public float spatialBlend = 0f;

    [Header("Fallback (Library)")]
    public SfxId footstepId = SfxId.Movement_FootstepLoop;

    [Header("Movement Detection")]
    [Min(0.001f)] public float minMoveSpeed = 0.05f;
    [Min(0.01f)] public float speedForFastSteps = 2f;

    [Header("Step Rate")]
    [Min(0.05f)] public float minStepInterval = 0.16f;
    [Min(0.05f)] public float maxStepInterval = 0.42f;

    [Header("AudioSource")]
    public AudioSource audioSource;

    private float _stepTimer;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = Mathf.Clamp01(spatialBlend);
        audioSource.volume = 1f;
        audioSource.pitch = 1f;

        _stepTimer = 0f;
    }

    private void Update()
    {
        if (rb == null || audioSource == null) return;

        float speed = rb.linearVelocity.magnitude;
        bool moving = speed > minMoveSpeed;

        if (!moving)
        {
            _stepTimer = 0f;
            return;
        }

        float denom = speedForFastSteps <= 0f ? 1f : speedForFastSteps;
        float t = Mathf.Clamp01(speed / denom);
        float interval = Mathf.Lerp(maxStepInterval, minStepInterval, t);
        if (interval <= 0.01f) interval = 0.01f;

        _stepTimer += Time.unscaledDeltaTime;

        if (_stepTimer >= interval)
        {
            _stepTimer -= interval;
            PlayOneStep();
        }
    }

    private void PlayOneStep()
    {
        AudioClip clip = null;

        if (footstepClips != null && footstepClips.Length > 0)
        {
            clip = footstepClips.Length == 1 ? footstepClips[0] : footstepClips[Random.Range(0, footstepClips.Length)];
        }
        else
        {
            var sp = SfxPlayer.Instance;
            if (sp != null)
                clip = sp.PickClip(footstepId);
        }

        if (clip == null) return;

        float pMin = pitchRange.x <= 0f ? 0.01f : pitchRange.x;
        float pMax = pitchRange.y <= 0f ? 0.01f : pitchRange.y;
        if (pMax < pMin) { float tmp = pMin; pMin = pMax; pMax = tmp; }
        float pitch = (pMin == pMax) ? pMin : Random.Range(pMin, pMax);

        audioSource.spatialBlend = Mathf.Clamp01(spatialBlend);
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
