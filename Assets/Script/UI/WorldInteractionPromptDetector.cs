using UnityEngine;

/// <summary>
/// Attach to any world object that should show a proximity prompt.
/// This script only controls prompt visibility by distance; it does not trigger interaction.
/// </summary>
public class WorldInteractionPromptDetector : MonoBehaviour
{
    [Header("Prompt Target")]
    public WorldInteractionPromptView promptView;

    [Header("Player")]
    public Transform playerTarget;
    public string playerTag = "Player";
    public bool autoFindPlayerByTag = true;

    [Header("Distance")]
    [Min(0f)] public float showDistance = 2.5f;
    [Min(0f)] public float hideDistance = 3.0f;
    public Transform distanceOrigin;

    [Header("Runtime")]
    [Min(0f)] public float checkInterval = 0.05f;
    public bool hideOnEnable = true;

    private bool _isVisible;
    private float _nextCheckTime;
    private Transform _cachedPlayer;

    private void Awake()
    {
        if (promptView == null)
            promptView = GetComponentInChildren<WorldInteractionPromptView>(true);

        if (hideDistance < showDistance)
            hideDistance = showDistance;
    }

    private void OnEnable()
    {
        _nextCheckTime = 0f;

        if (hideOnEnable)
            ApplyVisible(false, true);
    }

    private void OnDisable()
    {
        ApplyVisible(false, true);
    }

    private void Update()
    {
        if (promptView == null)
            return;

        if (checkInterval > 0f && Time.unscaledTime < _nextCheckTime)
            return;

        _nextCheckTime = Time.unscaledTime + checkInterval;

        Transform player = ResolvePlayer();
        if (player == null)
        {
            ApplyVisible(false, false);
            return;
        }

        Vector3 origin = distanceOrigin != null ? distanceOrigin.position : transform.position;
        float sqrDistance = (player.position - origin).sqrMagnitude;
        float showSqr = showDistance * showDistance;
        float hideSqr = hideDistance * hideDistance;

        if (!_isVisible)
        {
            if (sqrDistance <= showSqr)
                ApplyVisible(true, false);
        }
        else
        {
            if (sqrDistance >= hideSqr)
                ApplyVisible(false, false);
        }
    }

    private Transform ResolvePlayer()
    {
        if (playerTarget != null)
            return playerTarget;

        if (_cachedPlayer != null)
            return _cachedPlayer;

        if (!autoFindPlayerByTag || string.IsNullOrWhiteSpace(playerTag))
            return null;

        GameObject go = GameObject.FindGameObjectWithTag(playerTag);
        if (go == null)
            return null;

        _cachedPlayer = go.transform;
        return _cachedPlayer;
    }

    private void ApplyVisible(bool visible, bool force)
    {
        if (!force && _isVisible == visible)
            return;

        _isVisible = visible;
        promptView.SetVisible(visible);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (showDistance < 0f) showDistance = 0f;
        if (hideDistance < 0f) hideDistance = 0f;
        if (hideDistance < showDistance) hideDistance = showDistance;
        if (checkInterval < 0f) checkInterval = 0f;
    }
#endif
}
