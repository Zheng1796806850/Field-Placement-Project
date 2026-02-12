using UnityEngine;

public class TimedActionController : MonoBehaviour
{
    public TimedActionHUD hud;
    public bool useUnscaledTime = false;
    public KeyCode defaultCancelKey = KeyCode.Escape;
    public bool cancelIfTargetDisabled = true;

    public bool IsBusy => _active;

    private TimedActionRequest _req;
    private bool _active;
    private float _elapsed;
    private PlayerMovementController _mover;
    private bool _movementLocked;

    private void Update()
    {
        if (!_active) return;

        if (_req == null)
        {
            Cleanup();
            return;
        }

        if (_req.cancelKey != KeyCode.None && Input.GetKeyDown(_req.cancelKey))
        {
            Cancel();
            return;
        }

        if (_req.requireHold && _req.holdKey != KeyCode.None && !Input.GetKey(_req.holdKey))
        {
            Cancel();
            return;
        }

        if (_req.cancelIfPhaseNotDay)
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null && gsm.CurrentPhase != DayNightPhase.Day)
            {
                Cancel();
                return;
            }
        }

        if (_req.target != null)
        {
            if (cancelIfTargetDisabled && !_req.target.gameObject.activeInHierarchy)
            {
                Cancel();
                return;
            }

            if (_req.maxDistance > 0f)
            {
                float d = Vector2.Distance(transform.position, _req.target.position);
                if (d > _req.maxDistance)
                {
                    Cancel();
                    return;
                }
            }
        }

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _elapsed += dt;

        float dur = Mathf.Max(0.0001f, _req.duration);
        float p = Mathf.Clamp01(_elapsed / dur);

        if (hud != null) hud.SetProgress(p);
        _req.onProgress?.Invoke(p);

        if (_elapsed >= _req.duration)
        {
            Complete();
        }
    }

    public bool TryBegin(TimedActionRequest request)
    {
        if (request == null) return false;
        if (_active) return false;

        _req = request;
        _elapsed = 0f;
        _active = true;

        if (_req.cancelKey == KeyCode.None) _req.cancelKey = defaultCancelKey;

        if (_req.holdKey == KeyCode.None)
        {
            var pi = GetComponentInChildren<PlayerInteractor2D>();
            _req.holdKey = pi != null ? pi.interactKey : KeyCode.E;
        }

        if (_req.lockPlayerMovement)
        {
            _mover = GetComponentInChildren<PlayerMovementController>();
            if (_mover != null)
            {
                _mover.SetCanMove(false);
                _movementLocked = true;
            }
        }

        if (hud != null)
        {
            hud.SetLabel(_req.label);
            hud.SetProgress(0f);
            hud.SetVisible(true);
        }

        _req.onBegin?.Invoke();

        if (_req.duration <= 0f)
        {
            Complete();
        }

        return true;
    }

    public void CancelActive()
    {
        Cancel();
    }

    private void Complete()
    {
        if (!_active) return;

        var req = _req;
        Cleanup();
        req.onComplete?.Invoke();
    }

    private void Cancel()
    {
        if (!_active) return;

        var req = _req;
        Cleanup();
        req.onCancel?.Invoke();
    }

    private void Cleanup()
    {
        if (_req != null)
        {
            _req.onProgress?.Invoke(0f);
        }

        if (hud != null)
        {
            hud.SetProgress(0f);
            hud.SetVisible(false);
        }

        if (_movementLocked && _mover != null)
        {
            _mover.SetCanMove(true);
        }

        _movementLocked = false;
        _mover = null;
        _req = null;
        _active = false;
        _elapsed = 0f;
    }

    private void OnDisable()
    {
        if (_active) Cancel();
    }
}
