using UnityEngine;

/// <summary>用于「在前院/后院区域内」布尔状态（进出触发器），供 <see cref="LinearStoryDirector"/> 累计移动时间等。</summary>
[RequireComponent(typeof(Collider2D))]
public class StoryAreaPresenceVolume2D : MonoBehaviour
{
    [Tooltip("与 LinearStoryDirector 上配置的 id 一致，例如 story_front_yard / story_backyard。")]
    public string presenceAreaId = "story_front_yard";

    public bool requirePlayerTag = true;
    public string playerTag = "Player";

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        LinearStoryDirector.NotifyAreaPresence(presenceAreaId, true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        LinearStoryDirector.NotifyAreaPresence(presenceAreaId, false);
    }

    bool IsPlayer(Collider2D other)
    {
        if (other == null) return false;
        if (requirePlayerTag && !string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag))
            return false;
        return true;
    }
}
