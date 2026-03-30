using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum TutorialProgressDisplayMode
{
    [Tooltip("Each objective: marker + GetProgressText(), separated by Progress Between Objectives.")]
    PerObjectiveLegacy = 0,
    [Tooltip("One header line [done/total] + headline, then each objective's GetProgressText() (e.g. multi-line WASD).")]
    StepFractionHeaderThenObjectives = 1
}

public class TutorialManager : MonoBehaviour
{
    [Header("Steps")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    [Header("Refs")]
    public TutorialAbilityGate abilityGate;
    public TutorialStepTeleporter teleporter;
    public TutorialScreenFader fader;
    public Transform player;

    [Header("UI")]
    public TextMeshProUGUI stepTitleLabel;
    public TextMeshProUGUI stepDescLabel;
    public TextMeshProUGUI objectiveProgressLabel;
    public TutorialProgressDisplayMode progressDisplayMode = TutorialProgressDisplayMode.StepFractionHeaderThenObjectives;
    [Tooltip("{0} completed objectives, {1} total in step, {2} headline (Progress Headline or Title).")]
    public string progressStepFractionFormat = "[{0}/{1}] {2}";
    [Tooltip("Fallback when a TutorialStep leaves progress line format empty (Legacy mode only).")]
    public string defaultProgressLineFormat = "{0}{1}";

    [Header("Finish")]
    public bool loadGameplayAfterComplete = true;
    public string gameplaySceneName = "BaseScene";
    public int gameplaySceneBuildIndex = -1;
    public bool useLoadingScene = true;
    public string loadingSceneName = "Loading";
    public int loadingSceneBuildIndex = -1;
    public string loadingTitle = "Entering Game";
    public string readyPrompt = "Click Anywhere To Start";
    [Min(0f)] public float minimumLoadingScreenTime = 0.15f;
    [Tooltip("WaterCollectorBuildSpot collectorSaveKey values — cleared when loading Base after tutorial so tutorial session state is not kept (match Main Menu list).")]
    public List<string> waterCollectorSaveKeysToClearBeforeGameplay = new List<string> { "wc_base_01" };

    private int _stepIndex = -1;
    private TutorialStep _currentStep;
    private TutorialObjective[] _currentObjectives = new TutorialObjective[0];
    private bool _transitioning;
    private CameraFollowBounds2D _cameraFollowSuspendedForPoints;
    private Grid _wallPlacementDefaultGrid;
    private bool _wallPlacementGridCaptured;
    private PlayerWallPlacementController _wallPlacementController;

    public TutorialStep GetCurrentStep() => _currentStep;

    private void Awake()
    {
        ResolveRefs();
        if (steps.Count == 0)
            steps.AddRange(FindObjectsByType<TutorialStep>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }

    private void Start()
    {
        ActivateStep(0);
    }

    private void Update()
    {
        if (_currentStep == null) return;
        RefreshProgressText();
    }

    private void ResolveRefs()
    {
        if (abilityGate == null) abilityGate = FindFirstObjectByType<TutorialAbilityGate>(FindObjectsInactive.Include);
        if (teleporter == null) teleporter = FindFirstObjectByType<TutorialStepTeleporter>(FindObjectsInactive.Include);
        if (fader == null) fader = FindFirstObjectByType<TutorialScreenFader>(FindObjectsInactive.Include);

        if (player == null)
        {
            var m = FindFirstObjectByType<PlayerMovementController>(FindObjectsInactive.Include);
            if (m != null) player = m.transform;
        }

        if (teleporter != null && teleporter.fader == null && fader != null)
            teleporter.fader = fader;
    }

    private void ActivateStep(int index)
    {
        if (index < 0 || index >= steps.Count)
        {
            HandleTutorialCompleted();
            return;
        }

        StopCurrentStep();

        _stepIndex = index;
        _currentStep = steps[index];
        if (_currentStep == null)
        {
            ActivateStep(index + 1);
            return;
        }

        if (abilityGate != null)
            abilityGate.ApplyStepAbilities(_currentStep);

        _currentStep.gameObject.SetActive(true);
        _currentStep.InvalidateObjectiveCache();
        var list = _currentStep.Objectives;
        _currentObjectives = new TutorialObjective[list.Count];

        for (int i = 0; i < list.Count; i++)
        {
            _currentObjectives[i] = (TutorialObjective)list[i];
            if (_currentObjectives[i] == null) continue;
            _currentObjectives[i].OnCompleted += HandleObjectiveCompleted;
            _currentObjectives[i].Begin(this, _currentStep);
        }

        ApplyStepLabels();
        RefreshProgressText();
        ApplyCurrentStepCamera(snapInstant: true);
        ApplyWallPlacementGridForCurrentStep();
    }

    private void LateUpdate()
    {
        if (_transitioning || _currentStep == null) return;
        if (_currentStep.wallPlacementGrid == null) return;

        _wallPlacementController ??= FindFirstObjectByType<PlayerWallPlacementController>(FindObjectsInactive.Include);
        if (_wallPlacementController != null && _wallPlacementController.grid != _currentStep.wallPlacementGrid)
        {
            _wallPlacementController.SetGridForTutorial(_currentStep.wallPlacementGrid, suppressAutoResolve: true);
        }
    }

    private void StopCurrentStep()
    {
        if (_currentObjectives != null)
        {
            for (int i = 0; i < _currentObjectives.Length; i++)
            {
                var o = _currentObjectives[i];
                if (o == null) continue;
                o.OnCompleted -= HandleObjectiveCompleted;
                o.End();
            }
        }

        _currentObjectives = new TutorialObjective[0];
    }

    private void HandleObjectiveCompleted(TutorialObjective obj)
    {
        if (_transitioning) return;
        if (!AreAllObjectivesCompleted()) return;
        StartCoroutine(AdvanceStepRoutine());
    }

    private IEnumerator AdvanceStepRoutine()
    {
        if (_transitioning) yield break;
        _transitioning = true;

        int next = _stepIndex + 1;
        TutorialStep prev = _currentStep;

        StopCurrentStep();

        if (next >= steps.Count)
        {
            _transitioning = false;
            HandleTutorialCompleted();
            yield break;
        }

        TutorialStep nextStep = steps[next];
        float delay = prev != null ? Mathf.Max(0f, prev.completeDelaySeconds) : 0f;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        TutorialScreenFader stepFader = teleporter != null ? teleporter.fader : fader;
        float fadeDur = teleporter != null ? teleporter.fadeDuration : (fader != null ? fader.defaultFadeDuration : 0.35f);

        if (player != null && nextStep != null && nextStep.teleportTarget != null && stepFader != null)
        {
            yield return stepFader.FadeTo(1f, fadeDur);
            TutorialStepTeleporter.ApplyWorldPosition(player, nextStep.teleportTarget);
        }
        else if (player != null && nextStep != null && nextStep.teleportTarget != null)
            TutorialStepTeleporter.ApplyWorldPosition(player, nextStep.teleportTarget);

        _transitioning = false;
        ActivateStep(next);

        if (player != null && nextStep != null && nextStep.teleportTarget != null && stepFader != null)
            yield return stepFader.FadeTo(0f, fadeDur);

        _transitioning = false;
    }

    private bool AreAllObjectivesCompleted()
    {
        if (_currentObjectives == null || _currentObjectives.Length == 0)
            return true;

        for (int i = 0; i < _currentObjectives.Length; i++)
        {
            if (_currentObjectives[i] == null) continue;
            if (!_currentObjectives[i].IsCompleted)
                return false;
        }

        return true;
    }

    private void ApplyStepLabels()
    {
        if (_currentStep == null) return;
        if (stepTitleLabel != null) stepTitleLabel.text = _currentStep.title;
        if (stepDescLabel != null) stepDescLabel.text = _currentStep.description;
    }

    private void ApplyWallPlacementGridForCurrentStep()
    {
        _wallPlacementController ??= FindFirstObjectByType<PlayerWallPlacementController>(FindObjectsInactive.Include);
        if (_wallPlacementController == null) return;

        if (!_wallPlacementGridCaptured)
        {
            _wallPlacementDefaultGrid = _wallPlacementController.grid;
            _wallPlacementGridCaptured = true;
        }

        if (_currentStep != null && _currentStep.wallPlacementGrid != null)
            _wallPlacementController.SetGridForTutorial(_currentStep.wallPlacementGrid, suppressAutoResolve: true);
        else
            _wallPlacementController.SetGridForTutorial(_wallPlacementDefaultGrid, suppressAutoResolve: false);
    }

    private void ApplyCurrentStepCamera(bool snapInstant)
    {
        if (_currentStep == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        if (_currentStep.cameraPoint != null)
        {
            CameraFollowBounds2D follow = cam.GetComponent<CameraFollowBounds2D>();
            if (follow != null && follow.enabled)
            {
                follow.enabled = false;
                if (_cameraFollowSuspendedForPoints == null)
                    _cameraFollowSuspendedForPoints = follow;
            }

            cam.transform.position = _currentStep.cameraPoint.position;
        }
        else
        {
            ReleaseTutorialCameraPointLock(snapInstant);
        }
    }

    private void ReleaseTutorialCameraPointLock(bool snapInstant)
    {
        if (_cameraFollowSuspendedForPoints != null)
        {
            _cameraFollowSuspendedForPoints.enabled = true;
            if (snapInstant)
                _cameraFollowSuspendedForPoints.SnapToFollowTarget();
            _cameraFollowSuspendedForPoints = null;
        }
        else if (snapInstant && Camera.main != null)
        {
            var follow = Camera.main.GetComponent<CameraFollowBounds2D>();
            if (follow != null)
                follow.SnapToFollowTarget();
        }
    }

    private void RefreshProgressText()
    {
        if (_currentStep == null) return;

        TextMeshProUGUI target = _currentStep.objectiveHintLabel != null ? _currentStep.objectiveHintLabel : objectiveProgressLabel;
        if (target == null) return;

        if (_currentObjectives == null || _currentObjectives.Length == 0)
        {
            target.text = _currentStep.progressWhenEmptyObjectives;
            return;
        }

        if (progressDisplayMode == TutorialProgressDisplayMode.StepFractionHeaderThenObjectives)
        {
            target.text = BuildProgressTextStepHeader();
            return;
        }

        string lineFormat = string.IsNullOrEmpty(_currentStep.progressLineFormat)
            ? defaultProgressLineFormat
            : _currentStep.progressLineFormat;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < _currentObjectives.Length; i++)
        {
            var o = _currentObjectives[i];
            if (o == null) continue;
            if (sb.Length > 0) sb.Append(_currentStep.progressBetweenObjectives);
            string marker = o.IsCompleted ? _currentStep.progressCompletedMarker : _currentStep.progressIncompleteMarker;
            string line = string.Format(lineFormat, marker, o.GetProgressText());
            sb.Append(line);
        }

        target.text = sb.ToString();
    }

    private string BuildProgressTextStepHeader()
    {
        int total = 0;
        int done = 0;
        for (int i = 0; i < _currentObjectives.Length; i++)
        {
            var o = _currentObjectives[i];
            if (o == null) continue;
            total++;
            if (o.IsCompleted) done++;
        }

        string headline = string.IsNullOrEmpty(_currentStep.progressHeadline)
            ? _currentStep.title
            : _currentStep.progressHeadline;

        var sb = new StringBuilder();
        sb.AppendFormat(progressStepFractionFormat, done, total, headline);

        bool firstBody = true;
        for (int i = 0; i < _currentObjectives.Length; i++)
        {
            var o = _currentObjectives[i];
            if (o == null) continue;
            if (!firstBody)
                sb.Append(_currentStep.progressBetweenObjectives);
            firstBody = false;
            sb.Append('\n');
            sb.Append(o.GetProgressText());
        }

        return sb.ToString();
    }

    private void HandleTutorialCompleted()
    {
        if (abilityGate != null)
            abilityGate.RestoreDefaults();

        if (_wallPlacementController != null)
            _wallPlacementController.SetGridForTutorial(_wallPlacementDefaultGrid, suppressAutoResolve: false);

        ReleaseTutorialCameraPointLock(snapInstant: true);

        if (!loadGameplayAfterComplete)
            return;

        if (waterCollectorSaveKeysToClearBeforeGameplay != null && waterCollectorSaveKeysToClearBeforeGameplay.Count > 0)
            BaseWorldSession.DeleteWaterCollectorKeysForAllRuns(waterCollectorSaveKeysToClearBeforeGameplay);

        if (useLoadingScene)
        {
            SceneLoadRequest.SetRequest(
                gameplaySceneName,
                gameplaySceneBuildIndex,
                LoadSceneMode.Single,
                loadingTitle,
                readyPrompt,
                minimumLoadingScreenTime
            );

            if (loadingSceneBuildIndex >= 0)
                SceneManager.LoadScene(loadingSceneBuildIndex, LoadSceneMode.Single);
            else
                SceneManager.LoadScene(loadingSceneName, LoadSceneMode.Single);

            return;
        }

        if (gameplaySceneBuildIndex >= 0)
            SceneManager.LoadScene(gameplaySceneBuildIndex, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }
}
