using UnityEngine;
using Pathfinding;

public class PlayerWallPlacementController : MonoBehaviour
{
    [Header("Refs")]
    public Grid grid;
    public Transform placementOrigin;
    public PlayerMovementController movement;
    public PlayerInteractor2D interactor;
    public PlayerCombat2D combat;
    public TimedActionController timedAction;
    public PlayerResourceInventory inventory;

    [Header("Behavior")]
    public bool disableInteractorInputWhilePlacing = true;
    public bool disableCombatInputWhilePlacing = true;
    public bool allowCancelKey = true;
    public KeyCode cancelPlacementKey = KeyCode.Escape;
    public bool autoSaveInventoryAfterBuild = true;
    public bool stayInPlacementModeAfterBuild = true;

    [Header("Mouse Placement")]
    public bool requireMainCamera = true;
    public bool useGridPlaneRaycast = true;
    public float fallbackWorldZ = 0f;

    [Header("Grid Occupancy Validation")]
    public bool useCellOccupancyValidation = true;
    [Range(0.05f, 1f)] public float cellOccupancySampleFraction = 0.2f;
    public Vector2 additionalOccupancySampleSize = Vector2.zero;

    [Header("Range Visualization")]
    public bool drawBuildRangeGizmo = true;
    public bool drawPreviewLineGizmo = true;
    public Color buildRangeGizmoColor = new Color(0.2f, 0.9f, 1f, 0.8f);
    public Color previewLineValidColor = new Color(0.2f, 1f, 0.2f, 0.9f);
    public Color previewLineInvalidColor = new Color(1f, 0.25f, 0.25f, 0.9f);

    [Header("Runtime Debug")]
    public bool debugLogs = false;
    public float currentPreviewDistance;
    public bool currentPreviewInRange;
    public bool currentPreviewPlacementValid;

    private WallPlacementQuickUseSO _activeConfig;
    private UseContext _context;
    private GameObject _previewInstance;
    private SpriteRenderer[] _previewRenderers;
    private TimedActionLoopSfxEmitter _previewLoopSfx;

    private bool _placementActive;
    private bool _buildInProgress;
    private Vector3Int _previewCell;
    private Vector3 _previewWorld;
    private bool _previewValid;
    private bool _previewInRange;
    private bool _interactorPreviousEnabled = true;
    private bool _combatBlockApplied;

    public bool IsPlacementModeActive => _placementActive && _activeConfig != null;
    public bool IsActiveWith(WallPlacementQuickUseSO config) => IsPlacementModeActive && _activeConfig == config;

    private void Awake()
    {
        ResolveRefs(gameObject);
    }

    private void OnDisable()
    {
        CancelPlacement(false, null);
    }

    private void Update()
    {
        if (!IsPlacementModeActive) return;

        ResolveRefs(_context.user != null ? _context.user : gameObject);

        if (allowCancelKey && cancelPlacementKey != KeyCode.None && Input.GetKeyDown(cancelPlacementKey))
        {
            CancelPlacement(true, _activeConfig != null ? _activeConfig.deactivateMessage : "Wall placement cancelled");
            return;
        }

        if (!_buildInProgress)
            UpdatePreview(false);

        if (!_buildInProgress && Input.GetMouseButtonDown(0))
            TryStartBuild();
    }

    public bool TogglePlacement(WallPlacementQuickUseSO config, UseContext context)
    {
        if (config == null) return false;

        if (IsActiveWith(config))
        {
            CancelPlacement(true, string.IsNullOrWhiteSpace(config.deactivateMessage) ? "Wall placement cancelled" : config.deactivateMessage);
            return true;
        }

        if (IsPlacementModeActive)
            CancelPlacement(false, null);

        _context = context;
        ResolveRefs(context.user != null ? context.user : gameObject);

        if (inventory == null)
        {
            PushMessage("No inventory");
            return false;
        }

        if (grid == null)
        {
            PushMessage("No Grid assigned");
            return false;
        }

        if (requireMainCamera && Camera.main == null)
        {
            PushMessage("No Main Camera found");
            return false;
        }

        if (config.wallPrefab == null)
        {
            PushMessage("No wall prefab assigned");
            return false;
        }

        if (config.previewPrefab == null)
        {
            PushMessage("No preview prefab assigned");
            return false;
        }

        _activeConfig = config;
        _placementActive = true;
        _buildInProgress = false;

        if (disableInteractorInputWhilePlacing && interactor != null)
        {
            _interactorPreviousEnabled = interactor.InputEnabled;
            interactor.SetInputEnabled(false);
        }

        ApplyCombatBlock(true);

        if (!EnsurePreviewInstance())
        {
            CancelPlacement(false, null);
            PushMessage("Failed to create preview");
            return false;
        }

        UpdatePreview(true);

        if (!string.IsNullOrWhiteSpace(config.activateMessage))
            PushMessage(config.activateMessage);

        if (debugLogs)
            Debug.Log($"[WallPlacement] Activated {config.name}");

        return true;
    }

    public void CancelPlacement(bool pushMessage, string message)
    {
        if (_buildInProgress && timedAction != null && timedAction.IsBusy)
            timedAction.CancelActive();

        _buildInProgress = false;

        DestroyPreview();

        if (disableInteractorInputWhilePlacing && interactor != null)
            interactor.SetInputEnabled(_interactorPreviousEnabled);

        ApplyCombatBlock(false);

        _activeConfig = null;
        _placementActive = false;
        _previewValid = false;
        _previewInRange = false;
        currentPreviewDistance = 0f;
        currentPreviewInRange = false;
        currentPreviewPlacementValid = false;

        if (pushMessage && !string.IsNullOrWhiteSpace(message))
            PushMessage(message);
    }

    private void ResolveRefs(GameObject user)
    {
        if (grid == null)
            grid = FindFirstObjectByType<Grid>(FindObjectsInactive.Include);

        if (placementOrigin == null)
            placementOrigin = transform;

        if (movement == null)
            movement = user != null ? user.GetComponentInParent<PlayerMovementController>() : GetComponentInParent<PlayerMovementController>();

        if (interactor == null)
            interactor = user != null ? user.GetComponentInParent<PlayerInteractor2D>() : GetComponentInParent<PlayerInteractor2D>();

        if (combat == null)
            combat = user != null ? user.GetComponentInParent<PlayerCombat2D>() : GetComponentInParent<PlayerCombat2D>();

        if (timedAction == null)
            timedAction = user != null ? user.GetComponentInParent<TimedActionController>() : GetComponentInParent<TimedActionController>();

        if (inventory == null)
            inventory = user != null ? user.GetComponentInParent<PlayerResourceInventory>() : GetComponentInParent<PlayerResourceInventory>();

        if (inventory == null)
            inventory = PlayerResourceInventory.Instance;
    }

    private void ApplyCombatBlock(bool active)
    {
        if (!disableCombatInputWhilePlacing || combat == null) return;

        if (active)
        {
            if (_combatBlockApplied) return;
            combat.PushExternalInputBlock();
            _combatBlockApplied = true;
        }
        else
        {
            if (!_combatBlockApplied) return;
            combat.PopExternalInputBlock();
            _combatBlockApplied = false;
        }
    }

    private bool EnsurePreviewInstance()
    {
        if (_previewInstance != null) return true;
        if (_activeConfig == null || _activeConfig.previewPrefab == null) return false;

        _previewInstance = Instantiate(_activeConfig.previewPrefab, Vector3.zero, Quaternion.identity);
        _previewInstance.name = $"{_activeConfig.previewPrefab.name}_Preview";

        var cols = _previewInstance.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;

        var bodies = _previewInstance.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies.Length; i++)
            bodies[i].simulated = false;

        _previewRenderers = _previewInstance.GetComponentsInChildren<SpriteRenderer>(true);
        _previewLoopSfx = _previewInstance.GetComponentInChildren<TimedActionLoopSfxEmitter>(true);

        return _previewInstance != null;
    }

    private void DestroyPreview()
    {
        StopPreviewLoop();

        if (_previewInstance != null)
            Destroy(_previewInstance);

        _previewInstance = null;
        _previewRenderers = null;
        _previewLoopSfx = null;
    }

    private void UpdatePreview(bool force)
    {
        if (_activeConfig == null) return;
        if (!EnsurePreviewInstance()) return;

        Vector3Int nextCell;
        Vector3 nextWorld;

        if (!TryGetMouseCell(out nextCell, out nextWorld))
        {
            _previewInstance.SetActive(false);
            _previewValid = false;
            _previewInRange = false;
            currentPreviewDistance = 0f;
            currentPreviewInRange = false;
            currentPreviewPlacementValid = false;
            return;
        }

        float nextDistance = GetBuildDistance(nextWorld);
        bool nextInRange = IsWithinBuildDistance(nextWorld, nextDistance);
        bool nextValid = nextInRange && ValidatePlacementAt(nextWorld);

        bool sameCell = nextCell == _previewCell;
        bool sameWorld = _previewWorld == nextWorld;
        bool sameRange = _previewInRange == nextInRange;
        bool sameValid = _previewValid == nextValid;

        _previewCell = nextCell;
        _previewWorld = nextWorld;
        _previewInRange = nextInRange;
        _previewValid = nextValid;

        currentPreviewDistance = nextDistance;
        currentPreviewInRange = nextInRange;
        currentPreviewPlacementValid = nextValid;

        if (!force && sameCell && sameWorld && sameRange && sameValid && _previewInstance.activeSelf)
            return;

        _previewInstance.transform.position = _previewWorld;
        _previewInstance.transform.rotation = Quaternion.identity;
        _previewInstance.SetActive(true);

        ApplyPreviewColor(_previewValid ? _activeConfig.validPreviewColor : _activeConfig.invalidPreviewColor);
    }

    private bool TryGetMouseCell(out Vector3Int cell, out Vector3 world)
    {
        cell = default;
        world = default;

        if (grid == null) return false;

        Camera cam = Camera.main;
        if (requireMainCamera && cam == null) return false;
        if (cam == null) return false;

        Vector3 mouseWorld;
        if (!TryGetMouseWorldOnGridPlane(cam, out mouseWorld))
            return false;

        cell = grid.WorldToCell(mouseWorld);
        world = grid.GetCellCenterWorld(cell);

        if (_activeConfig != null)
            world += _activeConfig.previewWorldOffset;

        return true;
    }

    private bool TryGetMouseWorldOnGridPlane(Camera cam, out Vector3 world)
    {
        world = default;

        if (cam == null) return false;

        if (useGridPlaneRaycast)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            float planeZ = grid != null ? grid.transform.position.z : fallbackWorldZ;
            Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));

            if (plane.Raycast(ray, out float enter))
            {
                world = ray.GetPoint(enter);
                world.z = planeZ;
                return true;
            }
        }

        Vector3 screen = Input.mousePosition;
        float targetZ = Mathf.Abs((grid != null ? grid.transform.position.z : fallbackWorldZ) - cam.transform.position.z);
        screen.z = targetZ;

        world = cam.ScreenToWorldPoint(screen);
        world.z = grid != null ? grid.transform.position.z : fallbackWorldZ;
        return true;
    }

    private Vector3 GetPlacementOriginPosition()
    {
        return placementOrigin != null ? placementOrigin.position : transform.position;
    }

    private float GetBuildDistance(Vector3 world)
    {
        Vector3 origin = GetPlacementOriginPosition();
        origin.z = 0f;

        Vector3 target = GetValidationCellCenterWorld(world);
        target.z = 0f;

        return Vector2.Distance(origin, target);
    }

    private bool IsWithinBuildDistance(Vector3 world)
    {
        return IsWithinBuildDistance(world, GetBuildDistance(world));
    }

    private bool IsWithinBuildDistance(Vector3 world, float precomputedDistance)
    {
        if (_activeConfig == null) return false;
        if (_activeConfig.maxBuildDistance <= 0f) return true;
        return precomputedDistance <= _activeConfig.maxBuildDistance;
    }

    private bool ValidatePlacementAt(Vector3 world)
    {
        if (_activeConfig == null) return false;
        if (grid == null) return false;

        if (useCellOccupancyValidation)
            return ValidateCellOccupancyAt(world);

        Vector2 size = GetBroadValidationSize();
        var hits = Physics2D.OverlapBoxAll(world, size, 0f, _activeConfig.placementBlockerLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;
            if (ShouldIgnorePlacementCollider(c)) continue;
            return false;
        }

        return true;
    }

    private bool ValidateCellOccupancyAt(Vector3 world)
    {
        Vector3 cellCenter = GetValidationCellCenterWorld(world);
        Vector2 sampleSize = GetCellOccupancySampleSize();

        var hits = Physics2D.OverlapBoxAll(cellCenter, sampleSize, 0f, _activeConfig.placementBlockerLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;
            if (ShouldIgnorePlacementCollider(c)) continue;
            return false;
        }

        return true;
    }

    private Vector3 GetValidationCellCenterWorld(Vector3 placementWorld)
    {
        Vector3 baseWorld = placementWorld;
        if (_activeConfig != null)
            baseWorld -= _activeConfig.previewWorldOffset;

        Vector3Int cell = grid.WorldToCell(baseWorld);
        return grid.GetCellCenterWorld(cell);
    }

    private Vector2 GetCellOccupancySampleSize()
    {
        Vector3 cellSize3 = grid != null ? grid.cellSize : Vector3.one;
        Vector2 cellSize = new Vector2(Mathf.Abs(cellSize3.x), Mathf.Abs(cellSize3.y));

        Vector2 size = cellSize * Mathf.Clamp(cellOccupancySampleFraction, 0.05f, 1f);
        size += additionalOccupancySampleSize;

        size.x = Mathf.Max(0.05f, size.x);
        size.y = Mathf.Max(0.05f, size.y);

        return size;
    }

    private Vector2 GetBroadValidationSize()
    {
        Vector2 size = _activeConfig.previewCheckSize;
        if (size.x <= 0f || size.y <= 0f)
        {
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
            size = new Vector2(Mathf.Abs(cellSize.x), Mathf.Abs(cellSize.y)) * 0.9f;
        }
        return size;
    }

    private bool ShouldIgnorePlacementCollider(Collider2D c)
    {
        if (c == null) return true;

        if (_activeConfig != null && _activeConfig.ignoreTriggerCollidersWhenValidating && c.isTrigger)
            return true;

        if (_previewInstance != null && c.transform.IsChildOf(_previewInstance.transform))
            return true;

        GameObject user = _context.user != null ? _context.user : gameObject;
        if (user != null && c.transform.IsChildOf(user.transform))
            return true;

        return false;
    }

    private void ApplyPreviewColor(Color color)
    {
        if (_previewRenderers == null) return;

        for (int i = 0; i < _previewRenderers.Length; i++)
        {
            var r = _previewRenderers[i];
            if (r == null) continue;
            r.color = color;
        }
    }

    private void TryStartBuild()
    {
        if (_activeConfig == null) return;
        if (_buildInProgress) return;
        if (timedAction != null && timedAction.IsBusy) return;

        UpdatePreview(true);

        if (!CanStartBuild(out string failMessage))
        {
            if (!string.IsNullOrWhiteSpace(failMessage))
                PushMessage(failMessage);
            return;
        }

        Vector3Int buildCell = _previewCell;
        Vector3 buildWorld = _previewWorld;

        if (timedAction == null)
        {
            if (!inventory.Spend(_activeConfig.resourceType, _activeConfig.placementCost))
            {
                PushMessage($"Not enough {_activeConfig.resourceType}");
                return;
            }

            CompleteBuildAt(buildCell, buildWorld);

            if (autoSaveInventoryAfterBuild)
                inventory.SaveInMemory();

            return;
        }

        _buildInProgress = true;
        bool spent = false;

        var req = new TimedActionRequest();
        req.label = "Building...";
        req.duration = Mathf.Max(0.01f, _activeConfig.buildDuration);
        req.requireHold = _activeConfig.holdToBuild;
        req.holdKey = KeyCode.Mouse0;
        req.cancelKey = _activeConfig.holdToBuild ? KeyCode.None : KeyCode.Mouse0;
        req.suppressCancelInputFrames = _activeConfig.holdToBuild ? 0 : 1;
        req.lockPlayerMovement = _activeConfig.lockPlayerMovementWhileBuilding;
        req.target = _previewInstance != null ? _previewInstance.transform : transform;
        req.maxDistance = _activeConfig.maxBuildDistance;
        req.cancelIfPhaseNotDay = _activeConfig.restrictBuildToDay;

        req.onBegin = () =>
        {
            if (!IsWithinBuildDistance(buildWorld))
            {
                _buildInProgress = false;
                timedAction.CancelActive();
                PushMessage("Too far away");
                return;
            }

            spent = inventory.Spend(_activeConfig.resourceType, _activeConfig.placementCost);
            if (!spent)
            {
                _buildInProgress = false;
                timedAction.CancelActive();
                PushMessage($"Not enough {_activeConfig.resourceType}");
                return;
            }

            StartPreviewLoop();
        };

        req.onProgress = (p) =>
        {
            if (p <= 0f)
                StopPreviewLoop();
        };

        req.onCancel = () =>
        {
            StopPreviewLoop();
            _buildInProgress = false;

            if (spent)
            {
                inventory.Add(_activeConfig.resourceType, _activeConfig.placementCost);
                if (autoSaveInventoryAfterBuild)
                    inventory.SaveInMemory();
            }

            UpdatePreview(true);
        };

        req.onComplete = () =>
        {
            StopPreviewLoop();
            _buildInProgress = false;

            if (!spent) return;

            if (!IsWithinBuildDistance(buildWorld) || !ValidatePlacementAt(buildWorld))
            {
                inventory.Add(_activeConfig.resourceType, _activeConfig.placementCost);
                if (autoSaveInventoryAfterBuild)
                    inventory.SaveInMemory();

                PushMessage(GetInvalidBuildMessage());
                UpdatePreview(true);
                return;
            }

            CompleteBuildAt(buildCell, buildWorld);

            if (autoSaveInventoryAfterBuild)
                inventory.SaveInMemory();

            if (stayInPlacementModeAfterBuild)
                UpdatePreview(true);
            else
                CancelPlacement(false, null);
        };

        timedAction.TryBegin(req);
    }

    private bool CanStartBuild(out string failMessage)
    {
        failMessage = null;

        if (_activeConfig == null)
        {
            failMessage = "No wall placement item";
            return false;
        }

        if (!_previewInRange)
        {
            failMessage = "Too far away";
            return false;
        }

        if (!_previewValid)
        {
            failMessage = GetInvalidBuildMessage();
            return false;
        }

        if (inventory == null)
        {
            failMessage = "No inventory";
            return false;
        }

        if (_activeConfig.restrictBuildToDay)
        {
            var gsm = GameStateManager.Instance;
            if (gsm != null && gsm.CurrentPhase != DayNightPhase.Day)
            {
                failMessage = "Can only build during day";
                return false;
            }
        }

        if (_activeConfig.wallPrefab == null)
        {
            failMessage = "No wall prefab assigned";
            return false;
        }

        if (!inventory.CanSpend(_activeConfig.resourceType, _activeConfig.placementCost))
        {
            failMessage = $"Not enough {_activeConfig.resourceType}";
            return false;
        }

        return true;
    }

    private string GetInvalidBuildMessage()
    {
        if (!_previewInRange)
            return "Too far away";

        if (_activeConfig != null && !string.IsNullOrWhiteSpace(_activeConfig.invalidPlacementMessage))
            return _activeConfig.invalidPlacementMessage;

        return "Cannot place wall here";
    }

    private void CompleteBuildAt(Vector3Int cell, Vector3 world)
    {
        if (_activeConfig == null || _activeConfig.wallPrefab == null) return;

        Vector3 spawnPos = world + _activeConfig.builtWorldOffset;
        var go = Instantiate(_activeConfig.wallPrefab, spawnPos, Quaternion.identity);

        var wall = go.GetComponentInChildren<WoodenWallDurability>(true);
        if (wall != null)
            wall.SetPlacementCostEquivalent(_activeConfig.placementCost);

        var deathHandler = go.GetComponentInChildren<WallDeathHandler>(true);
        if (deathHandler != null)
            deathHandler.RestoreBlockingState();
        else
            RefreshGraphsForObject(go);

        if (debugLogs)
            Debug.Log($"[WallPlacement] Built wall at cell {cell}, distance={GetBuildDistance(world):F2}");

        UpdatePreview(true);
    }

    private void RefreshGraphsForObject(GameObject go)
    {
        if (go == null) return;
        if (AstarPath.active == null) return;

        var cols = go.GetComponentsInChildren<Collider2D>(true);
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null) continue;
            if (!c.enabled) continue;
            if (c.isTrigger) continue;

            if (!hasBounds)
            {
                b = c.bounds;
                hasBounds = true;
            }
            else
            {
                b.Encapsulate(c.bounds);
            }
        }

        if (!hasBounds) return;

        b.Expand(0.5f);
        AstarPath.active.UpdateGraphs(b);
        AstarPath.active.FlushGraphUpdates();
    }

    private void StartPreviewLoop()
    {
        if (_previewLoopSfx == null || _activeConfig == null) return;
        _previewLoopSfx.PlayLoop(_activeConfig.buildLoopSfxId);
    }

    private void StopPreviewLoop()
    {
        if (_previewLoopSfx == null) return;
        _previewLoopSfx.StopLoop();
    }

    private void PushMessage(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return;

        if (_context.pushMessage != null)
        {
            _context.pushMessage.Invoke(msg);
            return;
        }

        if (inventory != null)
            inventory.PushMessage(msg);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawBuildRangeGizmo) return;
        if (_activeConfig == null) return;
        if (_activeConfig.maxBuildDistance <= 0f) return;

        Vector3 origin = placementOrigin != null ? placementOrigin.position : transform.position;

        Gizmos.color = buildRangeGizmoColor;
        Gizmos.DrawWireSphere(origin, _activeConfig.maxBuildDistance);

        if (drawPreviewLineGizmo && _placementActive)
        {
            Gizmos.color = _previewValid ? previewLineValidColor : previewLineInvalidColor;
            Gizmos.DrawLine(origin, _previewWorld);
            Gizmos.DrawWireSphere(_previewWorld, 0.08f);
        }
    }
}