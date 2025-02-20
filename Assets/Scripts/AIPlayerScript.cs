using UnityEngine;

public class AIPlayerScript : MonoBehaviour
{
    public Rigidbody2D myRigidbody;
    public float moveSpeed = 3f;
    public float jumpForce = 10f;
    public float decisionRate = 0.5f; // AI makes a decision every 0.5s
    public float fitness = 0f; // AI fitness score

    private float decisionTimer = 0f;
    private bool isGrounded = false;

    void Start()
    {
        myRigidbody = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        decisionTimer -= Time.deltaTime;

        if (decisionTimer <= 0)
        {
            decisionTimer = decisionRate;
            MakeDecision();
        }

        // Track fitness (higher Y position = better)
        fitness = transform.position.y;
    }

    void MakeDecision()
    {
        float randomChoice = Random.value; // Get a random value between 0 and 1

        if (randomChoice < 0.4f)
        {
            MoveLeft();
        }
        else if (randomChoice < 0.8f)
        {
            MoveRight();
        }
        else
        {
            Jump();
        }
    }

    void MoveLeft()
    {
        myRigidbody.linearVelocity = new Vector2(-moveSpeed, myRigidbody.linearVelocity.y);
    }

    void MoveRight()
    {
        myRigidbody.linearVelocity = new Vector2(moveSpeed, myRigidbody.linearVelocity.y);
    }

    void Jump()
    {
        if (isGrounded)
        {
            myRigidbody.linearVelocity = new Vector2(myRigidbody.linearVelocity.x, jumpForce);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            isGrounded = true;
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            isGrounded = false;
        }
    }
}
