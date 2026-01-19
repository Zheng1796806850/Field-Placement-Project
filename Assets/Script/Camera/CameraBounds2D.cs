using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class CameraBounds2D : MonoBehaviour
{
    [Header("Gizmo")]
    public Color gizmoColor = new Color(0f, 1f, 1f, 0.35f);
    public bool drawSolid = true;

    private BoxCollider2D box;

    public Bounds BoundsWorld
    {
        get
        {
            if (box == null) box = GetComponent<BoxCollider2D>();
            return box.bounds;
        }
    }

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        var b = GetComponent<BoxCollider2D>();
        if (b == null) return;

        Gizmos.color = gizmoColor;
        var bounds = b.bounds;

        if (drawSolid)
            Gizmos.DrawCube(bounds.center, bounds.size);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
