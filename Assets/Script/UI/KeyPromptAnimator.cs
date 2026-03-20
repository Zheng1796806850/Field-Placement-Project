using UnityEngine;
using UnityEngine.UI;

public class KeyPromptAnimator : MonoBehaviour
{
    public Image image;
    public Sprite[] frames;
    public float frameRate = 10f;

    private int index = 0;
    private float timer = 0f;

    void Update()
    {
        if (frames == null || frames.Length == 0) return;

        timer += Time.unscaledDeltaTime;

        if (timer >= 1f / frameRate)
        {
            timer = 0f;
            index = (index + 1) % frames.Length;
            image.sprite = frames[index];
        }
    }
}