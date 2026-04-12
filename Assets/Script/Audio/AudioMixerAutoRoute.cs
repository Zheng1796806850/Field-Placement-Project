using UnityEngine;

/// <summary>
/// Assign on any GameObject with an <see cref="AudioSource"/> that is placed in the scene or prefab
/// (not created by gameplay scripts). Routes output to Music or SFX group from <see cref="GameAudioSettings"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class AudioMixerAutoRoute : MonoBehaviour
{
    public enum RouteTarget
    {
        Sfx,
        Music
    }

    [Tooltip("SFX = gameplay/UI one-shots; Music = BGM, ambience, menu music.")]
    public RouteTarget routeTo = RouteTarget.Sfx;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Apply()
    {
        var src = GetComponent<AudioSource>();
        if (src == null) return;

        if (routeTo == RouteTarget.Music)
            GameAudioSettings.ApplyMusicRoute(src);
        else
            GameAudioSettings.ApplySfxRoute(src);
    }
}
