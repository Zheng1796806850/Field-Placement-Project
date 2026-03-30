using System.Collections;
using UnityEngine;

public class TutorialStepTeleporter : MonoBehaviour
{
    public TutorialScreenFader fader;
    [Min(0.01f)] public float fadeDuration = 0.35f;

    public IEnumerator Teleport(Transform player, Transform target, float completeDelay)
    {
        if (player == null || target == null)
            yield break;

        if (completeDelay > 0f)
            yield return new WaitForSecondsRealtime(completeDelay);

        if (fader != null)
            yield return fader.FadeTo(1f, fadeDuration);

        ApplyWorldPosition(player, target);

        if (fader != null)
            yield return fader.FadeTo(0f, fadeDuration);
    }

    /// <summary>
    /// Moves transform and Rigidbody2D together so the player does not stay at the old pose until the next physics/move input.
    /// </summary>
    public static void ApplyWorldPosition(Transform player, Transform target)
    {
        if (player == null || target == null) return;

        Vector3 pos = target.position;
        pos.z = player.position.z;
        player.position = pos;

        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = new Vector2(pos.x, pos.y);
        }
    }
}
