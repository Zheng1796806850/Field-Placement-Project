using UnityEngine;

public class HouseObjective : MonoBehaviour
{
    public static HouseObjective Instance { get; private set; }

    public Transform targetPoint;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public Vector3 Position => targetPoint != null ? targetPoint.position : transform.position;
}
