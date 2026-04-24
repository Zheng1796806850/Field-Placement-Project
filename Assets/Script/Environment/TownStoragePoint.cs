using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TownStoragePoint : MonoBehaviour, IInteractable
{
    [Header("Identity")]
    [Tooltip("Must be unique in Town scene for persistence.")]
    public string storageId = "";
    public string storageDisplayName = "Storage";

    [Header("Refs")]
    public StorageInventory storageInventory;
    public StoragePanelHUD storagePanelHUD;
    [Tooltip("Simple hint object (e.g. child with SpriteRenderer '!'). This object will be toggled on/off by proximity.")]
    public GameObject simpleHintObject;
    public BackpackRulesSO rulesOverride;

    [Header("Simple Hint (Sprite Object Toggle)")]
    public bool useSimpleHintToggle = true;
    public string playerTag = "Player";
    public bool autoFindPlayerByTag = true;
    [Min(0f)] public float hintShowDistance = 2.2f;
    [Min(0f)] public float hintHideDistance = 2.8f;
    public Transform hintDistanceOrigin;
    [Min(0f)] public float hintCheckInterval = 0.05f;

    [Header("Interaction")]
    [TextArea] public string promptText = "Search";
    public int priority = 5;

    public int Priority => priority;

    private static readonly HashSet<string> ActiveIds = new HashSet<string>();
    private Transform _cachedPlayer;
    private bool _hintVisible;
    private float _nextHintCheckTime;

    private void Awake()
    {
        if (storageInventory == null)
            storageInventory = GetComponent<StorageInventory>();
        if (storagePanelHUD == null)
            storagePanelHUD = FindFirstObjectByType<StoragePanelHUD>(FindObjectsInactive.Include);
        if (simpleHintObject == null)
        {
            var sr = GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null && sr.gameObject != gameObject)
                simpleHintObject = sr.gameObject;
        }

        if (storageInventory != null)
        {
            BackpackRulesSO fallbackRules = rulesOverride;
            if (fallbackRules == null && storagePanelHUD != null)
                fallbackRules = storagePanelHUD.rules;
            storageInventory.BindAndLoad(storageId, fallbackRules);
        }
    }

    private void OnEnable()
    {
        ValidateStorageId();
        if (hintHideDistance < hintShowDistance)
            hintHideDistance = hintShowDistance;
        _nextHintCheckTime = 0f;
        SetHintVisible(false, true);
    }

    private void OnDisable()
    {
        if (!string.IsNullOrWhiteSpace(storageId))
            ActiveIds.Remove(storageId);
        SetHintVisible(false, true);
    }

    private void Update()
    {
        if (!useSimpleHintToggle)
            return;

        if (hintCheckInterval > 0f && Time.unscaledTime < _nextHintCheckTime)
            return;

        _nextHintCheckTime = Time.unscaledTime + hintCheckInterval;
        Transform player = ResolvePlayer();
        if (player == null)
        {
            SetHintVisible(false, false);
            return;
        }

        Vector3 origin = hintDistanceOrigin != null ? hintDistanceOrigin.position : transform.position;
        float sqr = (player.position - origin).sqrMagnitude;
        float showSqr = hintShowDistance * hintShowDistance;
        float hideSqr = hintHideDistance * hintHideDistance;

        if (!_hintVisible)
        {
            if (sqr <= showSqr)
                SetHintVisible(true, false);
        }
        else
        {
            if (sqr >= hideSqr)
                SetHintVisible(false, false);
        }
    }

    public string GetPrompt()
    {
        return string.IsNullOrWhiteSpace(promptText) ? "Search" : promptText;
    }

    public bool CanInteract(GameObject interactor)
    {
        if (storageInventory == null || storagePanelHUD == null)
            return false;

        var gsm = GameStateManager.Instance;
        if (gsm != null && gsm.IsPaused)
            return false;

        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
            return;

        storagePanelHUD.OpenFor(this, storageInventory);
    }

    private void ValidateStorageId()
    {
        if (string.IsNullOrWhiteSpace(storageId))
        {
            Debug.LogWarning($"{nameof(TownStoragePoint)} on '{name}' has empty storageId. Persistence will be disabled.");
            return;
        }

        if (!ActiveIds.Add(storageId))
            Debug.LogWarning($"{nameof(TownStoragePoint)} duplicate storageId detected: '{storageId}'. Multiple storage points may share persistence data.");
    }

    private Transform ResolvePlayer()
    {
        if (_cachedPlayer != null)
            return _cachedPlayer;

        if (autoFindPlayerByTag && !string.IsNullOrWhiteSpace(playerTag))
        {
            GameObject go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null)
            {
                _cachedPlayer = go.transform;
                return _cachedPlayer;
            }
        }

        var mover = FindFirstObjectByType<PlayerMovementController>(FindObjectsInactive.Exclude);
        if (mover != null)
        {
            _cachedPlayer = mover.transform;
            return _cachedPlayer;
        }

        return null;
    }

    private void SetHintVisible(bool visible, bool force)
    {
        if (!force && _hintVisible == visible)
            return;

        _hintVisible = visible;
        if (simpleHintObject != null && simpleHintObject.activeSelf != visible)
            simpleHintObject.SetActive(visible);
    }
}

