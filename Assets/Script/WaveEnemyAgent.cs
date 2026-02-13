using UnityEngine;

public class WaveEnemyAgent : MonoBehaviour
{
    public WaveProgressTracker waveProgress;
    public int waveId;

    public Health health;

    private bool _registered;
    private bool _died;

    public void Initialize(WaveProgressTracker tracker, int waveId)
    {
        waveProgress = tracker;
        this.waveId = waveId;

        if (health == null) health = GetComponentInChildren<Health>();

        Register();

        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDied += HandleDied;
        }
    }

    private void Register()
    {
        if (_registered) return;
        if (waveProgress == null) return;

        waveProgress.RegisterEnemy(gameObject, waveId);
        _registered = true;
    }

    private void Unregister()
    {
        if (!_registered) return;
        if (waveProgress == null) return;

        waveProgress.UnregisterEnemy(gameObject, waveId);
        _registered = false;
    }

    private void HandleDied()
    {
        if (_died) return;
        _died = true;
        Unregister();
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDied -= HandleDied;
        Unregister();
    }
}
