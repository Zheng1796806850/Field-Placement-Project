using UnityEngine;

public class FacingCamera : MonoBehaviour
{
    Transform[] childs;
    public bool facingParent = false;

    void Start()
    {
        childs = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            childs[i] = transform.GetChild(i);
        }

        if (facingParent)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }


    void Update()
    {
        for (int i = 0; i < childs.Length; i++)
        {
            childs[i].rotation = Camera.main.transform.rotation;
        }
    }
}
