using UnityEngine;
using UnityEngine.SceneManagement;

public class ZoneTeleportTrigger2D : MonoBehaviour, IInteractable
{
    public enum TeleportMode
    {
        LocalTeleport = 0,
        SceneTransition = 1
    }

    [Header("Teleport Mode")]
    public TeleportMode teleportMode = TeleportMode.LocalTeleport;

    [Header("Local Teleport")]
    public Transform teleportTarget;
    public string playerTag = "Player";

    [Header("Camera Switch")]
    public CameraFollowBounds2D cameraController;
    public CameraBounds2D switchToBounds;
    public bool snapCameraInstant = true;

    [Header("Scene Transition Target")]
    public string targetSceneName = "";
    public int targetSceneBuildIndex = -1;
    public string targetEntryPointId = "";

    [Header("Scene Transition Loading")]
    public bool useLoadingScene = true;
    public string loadingSceneName = "";
    public int loadingSceneBuildIndex = -1;
    public string loadingTitle = "Loading";
    public string readyPrompt = "Click Anywhere To Start";
    [Min(0f)] public float minimumLoadingScreenTime = 0.25f;

    [Header("State Carryover")]
    public bool carryPlayerVitals = true;
    public bool carryDayNightPhase = false;

    [Header("Phase Access Restriction")]
    public bool restrictByPhase = false;
    public bool allowInDay = true;
    public bool allowInNight = true;

    [Header("Denied Feedback")]
    public bool showDenyMessage = true;
    [TextArea] public string denyMessageByPhase = "Cannot travel now.";

    [Header("Interaction")]
    [TextArea] public string promptText = "Press E to Enter";
    public int priority = 100;

    public int Priority => priority;

    private void Reset()
    {
        if (Camera.main != null)
            cameraController = Camera.main.GetComponent<CameraFollowBounds2D>();
    }

    public string GetPrompt()
    {
        if (!restrictByPhase)
            return promptText;

        return IsPhaseAllowed() ? promptText : denyMessageByPhase;
    }

    private Transform ResolvePlayerTransform(GameObject interactor)
    {
        if (interactor == null) return null;

        var mover = interactor.GetComponentInParent<PlayerMovementController>();
        if (mover != null) return mover.transform;

        var root = interactor.transform.root;
        return root != null ? root : interactor.transform;
    }

    public bool CanInteract(GameObject interactor)
    {
        var playerT = ResolvePlayerTransform(interactor);
        if (playerT == null) return false;

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            if (!playerT.CompareTag(playerTag))
                return false;
        }

        if (teleportMode == TeleportMode.LocalTeleport)
            return teleportTarget != null;

        if (!HasValidTargetScene())
            return false;

        if (useLoadingScene && !HasValidLoadingScene())
            return false;

        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        var playerT = ResolvePlayerTransform(interactor);
        if (playerT == null) return;

        if (!IsPhaseAllowed())
        {
            PushDenyFeedback(interactor);
            return;
        }

        if (teleportMode == TeleportMode.LocalTeleport)
        {
            playerT.position = teleportTarget.position;

            if (cameraController == null && Camera.main != null)
                cameraController = Camera.main.GetComponent<CameraFollowBounds2D>();

            if (cameraController != null)
                cameraController.SetBounds(switchToBounds, snapCameraInstant);

            return;
        }

        BeginSceneTransition();
    }

    private bool IsPhaseAllowed()
    {
        if (!restrictByPhase)
            return true;

        var gsm = GameStateManager.Instance;
        if (gsm == null)
            return true;

        if (gsm.CurrentPhase == DayNightPhase.Day)
            return allowInDay;

        if (gsm.CurrentPhase == DayNightPhase.Night)
            return allowInNight;

        return true;
    }

    private void PushDenyFeedback(GameObject interactor)
    {
        if (!showDenyMessage)
            return;

        string msg = string.IsNullOrWhiteSpace(denyMessageByPhase) ? "Cannot travel now." : denyMessageByPhase;

        var inv = interactor != null ? interactor.GetComponentInParent<PlayerResourceInventory>() : null;
        if (inv == null) inv = PlayerResourceInventory.Instance;

        if (inv != null)
        {
            inv.PushMessage(msg);
            return;
        }

        Debug.Log(msg);
    }

    private void BeginSceneTransition()
    {
        SceneTransitionContext.Prepare(targetEntryPointId, carryDayNightPhase, carryPlayerVitals);

        if (useLoadingScene)
        {
            SceneLoadRequest.SetRequest(
                targetSceneName,
                targetSceneBuildIndex,
                LoadSceneMode.Single,
                loadingTitle,
                readyPrompt,
                minimumLoadingScreenTime
            );

            AsyncOperation loadingOp = CreateLoadingSceneOperation();
            if (loadingOp == null)
            {
                SceneLoadRequest.Clear();
                SceneTransitionContext.Clear();
            }

            return;
        }

        SceneLoadRequest.Clear();
        CreateTargetSceneOperation();
    }

    private bool HasValidTargetScene()
    {
        if (targetSceneBuildIndex >= 0)
            return targetSceneBuildIndex < SceneManager.sceneCountInBuildSettings;

        if (!string.IsNullOrWhiteSpace(targetSceneName))
            return Application.CanStreamedLevelBeLoaded(targetSceneName);

        return false;
    }

    private bool HasValidLoadingScene()
    {
        if (loadingSceneBuildIndex >= 0)
            return loadingSceneBuildIndex < SceneManager.sceneCountInBuildSettings;

        if (!string.IsNullOrWhiteSpace(loadingSceneName))
            return Application.CanStreamedLevelBeLoaded(loadingSceneName);

        return false;
    }

    private AsyncOperation CreateLoadingSceneOperation()
    {
        if (loadingSceneBuildIndex >= 0)
            return SceneManager.LoadSceneAsync(loadingSceneBuildIndex, LoadSceneMode.Single);

        if (!string.IsNullOrWhiteSpace(loadingSceneName))
            return SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Single);

        return null;
    }

    private AsyncOperation CreateTargetSceneOperation()
    {
        if (targetSceneBuildIndex >= 0)
            return SceneManager.LoadSceneAsync(targetSceneBuildIndex, LoadSceneMode.Single);

        if (!string.IsNullOrWhiteSpace(targetSceneName))
            return SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);

        return null;
    }
}
