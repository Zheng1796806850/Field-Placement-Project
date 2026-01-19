using UnityEngine;

public class PlayerMovementController : MonoBehaviour
{
    public float speed;

    private Rigidbody2D rb;
    private Animator animator;
    private float inputX, inputY;
    private float stopX, stopY;

    private Vector3 offset;

    void Start()
    {
        offset = Camera.main.transform.position - transform.position;
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        inputX = Input.GetAxisRaw("Horizontal");
        inputY = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(inputX, inputY).normalized;
        rb.linearVelocity = input * speed;

        if (input != Vector2.zero)
        {
            animator.SetBool("isMoving", true);
            stopX = inputX;
            stopY = inputY;
        }
        else
        {
            animator.SetBool("isMoving", false);
        }
        animator.SetFloat("InputX", stopX);
        animator.SetFloat("InputY", stopY);

        //Camera.main.transform.position = transform.position + offset;
    }
}
