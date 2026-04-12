using UnityEngine;

public class PlayerMovementController : MonoBehaviour
{
    public float speed;

    private Rigidbody2D rb;
    private Animator animator;
    private float inputX, inputY;
    private float stopX, stopY;

    /// <summary>Last horizontal facing for 2-way (left/right) animations: -1 or +1. Pure vertical movement keeps this.</summary>
    public float FacingSignX { get; private set; } = 1f;

    //private Vector3 offset;

    private bool canMove = true;

    void Start()
    {
        //offset = Camera.main.transform.position - transform.position;
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (canMove)
        {
            inputX = Input.GetAxisRaw("Horizontal");
            inputY = Input.GetAxisRaw("Vertical");
        }
        else
        {
            inputX = 0f;
            inputY = 0f;
        }


        Vector2 input = new Vector2(inputX, inputY).normalized;
        rb.linearVelocity = input * speed;

        if (input != Vector2.zero)
        {
            animator.SetBool("isMoving", true);
            stopX = inputX;
            stopY = inputY;
            if (Mathf.Abs(inputX) >= Mathf.Abs(inputY))
            {
                if (Mathf.Abs(inputX) > 1e-4f)
                    FacingSignX = Mathf.Sign(inputX);
            }
        }
        else
        {
            animator.SetBool("isMoving", false);
        }

        // 2-way sprite / blend: drive animator with horizontal sign only; vertical clips unused.
        animator.SetFloat("InputX", FacingSignX);
        animator.SetFloat("InputY", 0f);

        //Camera.main.transform.position = transform.position + offset;
    }

    public void SetCanMove(bool value)
    {
        canMove = value;
        if (!canMove)
            rb.linearVelocity = Vector2.zero;
    }

    /// <summary>Facing for combat / flip: left or right only; vertical-only input uses last <see cref="FacingSignX"/>.</summary>
    public Vector2 GetFacingDir()
    {
        if (Mathf.Abs(stopX) < 1e-4f && Mathf.Abs(stopY) < 1e-4f)
            return new Vector2(FacingSignX, 0f);

        if (Mathf.Abs(stopX) >= Mathf.Abs(stopY))
            return new Vector2(Mathf.Sign(stopX), 0f);

        return new Vector2(FacingSignX, 0f);
    }
}