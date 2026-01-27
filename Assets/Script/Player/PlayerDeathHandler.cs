using UnityEngine;

public class PlayerDeathHandler : MonoBehaviour
{
    [Header("Refs")]
    public Health playerHealth;

    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<Health>();
        if (playerHealth == null)
        {
            Debug.LogError("[PlayerDeathHandler] No Health found on Player.");
            enabled = false;
            return;
        }

        playerHealth.OnDied += HandlePlayerDied;
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.OnDied -= HandlePlayerDied;
    }

    private void HandlePlayerDied()
    {
        GameFlowManager.Instance?.TriggerDefeat("You died (HP = 0)");
    }
}
