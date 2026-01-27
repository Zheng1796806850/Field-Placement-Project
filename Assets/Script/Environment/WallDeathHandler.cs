using UnityEngine;

public class WallDeathHandler : MonoBehaviour
{
    public Health health;

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        health.OnDied += OnWallDestroyed;
    }

    private void OnWallDestroyed()
    {

    }
}
