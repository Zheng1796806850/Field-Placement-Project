using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dedicated key prompt frame animator for world interaction prompts.
/// Similar to KeyPromptAnimator, but pauses animation when game is paused.
/// </summary>
public class WorldInteractionPromptKeyAnimator : MonoBehaviour
{
    [Header("Animation")]
    public Image image;
    public Sprite[] frames;
    [Min(0.01f)] public float frameRate = 10f;

    [Header("Pause Behavior")]
    public bool stopWhenGamePaused = true;

    private int _index;
    private float _timer;

    private void Awake()
    {
        if (image == null)
            image = GetComponent<Image>();

        ApplyCurrentFrame();
    }

    private void OnEnable()
    {
        _timer = 0f;
        _index = 0;
        ApplyCurrentFrame();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0 || image == null)
            return;

        if (stopWhenGamePaused)
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null && gsm.IsPaused)
                return;
        }

        _timer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(0.01f, frameRate);

        if (_timer < frameDuration)
            return;

        _timer -= frameDuration;
        _index = (_index + 1) % frames.Length;
        ApplyCurrentFrame();
    }

    private void ApplyCurrentFrame()
    {
        if (image == null || frames == null || frames.Length == 0)
            return;

        _index = Mathf.Clamp(_index, 0, frames.Length - 1);
        image.sprite = frames[_index];
    }
}
