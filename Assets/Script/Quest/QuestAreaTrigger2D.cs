using UnityEngine;

/// <summary>Fires <see cref="GameplayEventHub.RaisePlayerEnteredArea"/> when the player enters this trigger (quest-driven; no polling).</summary>
[RequireComponent(typeof(Collider2D))]
public class QuestAreaTrigger2D : MonoBehaviour
{
    [Tooltip("Must match ObjectiveDefinition.targetId on a ReachArea objective.")]
    public string areaId = "area_default";

    [Tooltip("If true, only colliders with playerTag count.")]
    public bool requirePlayerTag = true;
    public string playerTag = "Player";

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c != null)
            c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(areaId)) return;
        if (requirePlayerTag && !other.CompareTag(playerTag)) return;

        GameplayEventHub.RaisePlayerEnteredArea(areaId);
    }
}
