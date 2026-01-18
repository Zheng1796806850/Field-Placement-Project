using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollowBounds2D : MonoBehaviour
{
    [Header("Follow")]
    public Transform target;
    public Vector3 followOffset = new Vector3(0f, 0f, -10f);
    public float followSmooth = 12f;

    [Header("Bounds")]
    public CameraBounds2D currentBounds;

    [Header("Clamp Plane")]
    public bool useTargetZPlane = true;
    public float fixedPlaneZ = 0f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredCamPos = target.position + followOffset;

        if (currentBounds != null)
            desiredCamPos = ClampToBounds_Perspective(desiredCamPos, currentBounds.BoundsWorld);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredCamPos,
            1f - Mathf.Exp(-followSmooth * Time.deltaTime)
        );
    }

    public void SetBounds(CameraBounds2D bounds, bool snapInstant = false)
    {
        currentBounds = bounds;

        if (snapInstant && target != null)
        {
            Vector3 desired = target.position + followOffset;
            if (currentBounds != null)
                desired = ClampToBounds_Perspective(desired, currentBounds.BoundsWorld);

            transform.position = desired;
        }
    }

    private Vector3 ClampToBounds_Perspective(Vector3 camPos, Bounds boundsWorld)
    {
        float planeZ = useTargetZPlane ? target.position.z : fixedPlaneZ;

        if (!TryGetViewportHitOnZPlane(camPos, new Vector2(0.5f, 0.5f), planeZ, out Vector3 centerHit))
            return camPos;

        if (!TryGetViewportHitOnZPlane(camPos, new Vector2(0f, 0f), planeZ, out Vector3 bl))
            return camPos;
        if (!TryGetViewportHitOnZPlane(camPos, new Vector2(1f, 1f), planeZ, out Vector3 tr))
            return camPos;

        float halfW = Mathf.Abs(tr.x - bl.x) * 0.5f;
        float halfH = Mathf.Abs(tr.y - bl.y) * 0.5f;

        float minX = boundsWorld.min.x + halfW;
        float maxX = boundsWorld.max.x - halfW;
        float minY = boundsWorld.min.y + halfH;
        float maxY = boundsWorld.max.y - halfH;

        float clampedX = (minX > maxX) ? boundsWorld.center.x : Mathf.Clamp(centerHit.x, minX, maxX);
        float clampedY = (minY > maxY) ? boundsWorld.center.y : Mathf.Clamp(centerHit.y, minY, maxY);

        Vector3 delta = new Vector3(clampedX - centerHit.x, clampedY - centerHit.y, 0f);
        camPos += delta;

        return camPos;
    }

    private bool TryGetViewportHitOnZPlane(Vector3 camPos, Vector2 viewport, float planeZ, out Vector3 hitPoint)
    {
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));

        Ray r = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
        r.origin = camPos;

        if (plane.Raycast(r, out float enter))
        {
            hitPoint = r.GetPoint(enter);
            return true;
        }

        hitPoint = default;
        return false;
    }
}
