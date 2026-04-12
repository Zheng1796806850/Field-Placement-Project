using UnityEngine;

/// <summary>
/// Raises <see cref="GameplayEventHub.RaiseEnemyKilled"/> from <see cref="Health.OnDied"/>.
/// Use on enemies that do not use <see cref="WaveEnemyAgent"/> (avoid attaching both on the same actor).
/// </summary>
public class QuestKillReporter2D : MonoBehaviour
{
    public Health health;

    [Tooltip("If set, used as quest kill id; otherwise gameObject.tag.")]
    public string questKillTagOverride;

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();
        if (health == null)
            health = GetComponentInChildren<Health>();

        if (health != null)
            health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        string tag = !string.IsNullOrEmpty(questKillTagOverride) ? questKillTagOverride : gameObject.tag;
        GameplayEventHub.RaiseEnemyKilled(tag, gameObject.GetInstanceID());
    }
}
