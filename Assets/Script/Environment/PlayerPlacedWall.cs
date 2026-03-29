using UnityEngine;

public class PlayerPlacedWall : MonoBehaviour
{
    [SerializeField] private string wallPlacementId = "";
    [SerializeField] private Vector3Int gridCell;

    public string WallPlacementId => wallPlacementId;
    public Vector3Int GridCell => gridCell;

    public void SetPlacement(string id, Vector3Int cell)
    {
        wallPlacementId = string.IsNullOrEmpty(id) ? "wooden_wall_default" : id;
        gridCell = cell;
    }
}
