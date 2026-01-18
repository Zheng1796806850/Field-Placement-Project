using UnityEngine;

public class ZoneTeleportTrigger2D : MonoBehaviour
{
    [Header("Teleport")]
    public Transform teleportTarget;
    public string playerTag = "Player";

    [Header("Camera Switch")]
    public CameraFollowBounds2D cameraController;
    public CameraBounds2D switchToBounds;
    public bool snapCameraInstant = true;

    private void Reset()
    {
        if (Camera.main != null)
            cameraController = Camera.main.GetComponent<CameraFollowBounds2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (teleportTarget == null) return;

        other.transform.position = teleportTarget.position;

        if (cameraController != null)
            cameraController.SetBounds(switchToBounds, snapCameraInstant);
    }
}
