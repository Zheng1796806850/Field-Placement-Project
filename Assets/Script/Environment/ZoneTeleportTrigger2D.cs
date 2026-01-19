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

    public bool CanInteract(GameObject interactor)
    {
        if (interactor == null) return false;
        if (!interactor.CompareTag(playerTag)) return false;
        return teleportTarget != null;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        interactor.transform.position = teleportTarget.position;

        if (cameraController != null)
            cameraController.SetBounds(switchToBounds, snapCameraInstant);
    }
}
