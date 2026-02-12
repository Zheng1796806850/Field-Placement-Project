using UnityEngine;

public class ZoneTeleportTrigger2D : MonoBehaviour, IInteractable
{
    [Header("Teleport")]
    public Transform teleportTarget;
    public string playerTag = "Player";

    [Header("Camera Switch")]
    public CameraFollowBounds2D cameraController;
    public CameraBounds2D switchToBounds;
    public bool snapCameraInstant = true;

    [Header("Interaction")]
    [TextArea] public string promptText = "Press E to Enter";
    public int priority = 100;

    public int Priority => priority;

    private void Reset()
    {
        if (Camera.main != null)
            cameraController = Camera.main.GetComponent<CameraFollowBounds2D>();
    }

    public string GetPrompt() => promptText;

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
        if (teleportTarget == null) return false;

        var playerT = ResolvePlayerTransform(interactor);
        if (playerT == null) return false;

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            if (!playerT.CompareTag(playerTag))
                return false;
        }

        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        var playerT = ResolvePlayerTransform(interactor);
        if (playerT == null) return;

        playerT.position = teleportTarget.position;

        if (cameraController == null && Camera.main != null)
            cameraController = Camera.main.GetComponent<CameraFollowBounds2D>();

        if (cameraController != null)
            cameraController.SetBounds(switchToBounds, snapCameraInstant);
    }
}
