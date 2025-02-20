using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerScript : MonoBehaviour
{
    public Rigidbody2D myRigidbody;
    public BoxCollider2D myCollider;
    
    public float baseJumpForce = 9.5f;
    public float walkingSpeed = 3f;
    public float chargeRate = 1.5f;
    public float maxChargeMultiplier = 1f;
    public float horizontalChargeMultiplier = 0.5f; // New adjustable horizontal charge multiplier

    private bool isChargingJump = false;
    private float currentCharge = 0f;
    private float horizontalInput = 0f;

    public Vector2 boxSize = new Vector2(0.5f, 0.3f);
    public float castDistance = 0.375f;
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    public SpriteRenderer spriteRenderer;

    private int initialSceneBuildIndex;
    private bool initialSceneLoadedFirstTime = true;

    public Animator animator;

    private enum TransitionDirection { None, Up, Down }
    private TransitionDirection transitionDirection = TransitionDirection.None;

    void Start()
    {
        // Ensure AI players don't collide with each other
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("AIPlayer"), LayerMask.NameToLayer("AIPlayer"), true);

        myRigidbody.freezeRotation = true;
        initialSceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
        DontDestroyOnLoad(gameObject);
    }

    void Awake()
    {
        if (FindObjectsByType<PlayerScript>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Update()
    {
        bool grounded = IsGrounded();
        horizontalInput = Input.GetAxisRaw("Horizontal");

        if (!isChargingJump && grounded)
        {
            myRigidbody.linearVelocity = new Vector2(horizontalInput * walkingSpeed, myRigidbody.linearVelocity.y);
        }

        if ((Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) && grounded)
        {
            isChargingJump = true;
            currentCharge = 0f;
            myRigidbody.linearVelocity = new Vector2(0f, myRigidbody.linearVelocity.y);

            animator.SetBool("isCharging", true);
            animator.SetBool("isLaunching", false);
            animator.SetBool("Bounced", false);
        }

        if (isChargingJump && (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)))
        {
            currentCharge += chargeRate * Time.deltaTime;
            currentCharge = Mathf.Min(currentCharge, maxChargeMultiplier);
        }

        if (isChargingJump && (Input.GetKeyUp(KeyCode.DownArrow) || Input.GetKeyUp(KeyCode.S)))
        {
            animator.SetBool("isCharging", false);
            animator.SetBool("isGrounded", false);
            animator.SetBool("isLaunching", true);

            // Apply the horizontal charge multiplier
            Vector2 jumpDirection = new Vector2(horizontalInput * horizontalChargeMultiplier, 1.5f).normalized;
            float finalJumpForce = baseJumpForce * currentCharge * 1.3f;

            myRigidbody.linearVelocity = jumpDirection * finalJumpForce;
            isChargingJump = false;

            // Calculate and log the jump angle
            float jumpAngle = Mathf.Atan2(jumpDirection.y, jumpDirection.x) * Mathf.Rad2Deg;
        }

        float screenTop = Camera.main.transform.position.y + Camera.main.orthographicSize;
        float screenBottom = Camera.main.transform.position.y - Camera.main.orthographicSize;

        if (transform.position.y > screenTop)
        {
            LoadNextScene();
        }
        else if (transform.position.y < screenBottom)
        {
            LoadPreviousScene();
        }

        if (Mathf.Abs(horizontalInput) > 0.1f && grounded && !isChargingJump)
        {
            animator.SetBool("isWalking", true);
        }
        else
        {
            animator.SetBool("isWalking", false);
        }
        animator.SetBool("isFalling", !grounded && myRigidbody.linearVelocity.y < 0);

        // Flip sprite based on input
        if (horizontalInput < 0 && grounded)
        {
            spriteRenderer.flipX = true;  // Face left
        }
        else if (horizontalInput > 0 && grounded)
        {
            spriteRenderer.flipX = false; // Face right
        }
    }

    void LoadNextScene()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            transitionDirection = TransitionDirection.Up;
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.Log("No more scenes available in Build Settings.");
        }
    }

    void LoadPreviousScene()
    {
        int previousSceneIndex = SceneManager.GetActiveScene().buildIndex - 1;
        if (previousSceneIndex >= 0)
        {
            transitionDirection = TransitionDirection.Down;
            SceneManager.LoadScene(previousSceneIndex);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex == initialSceneBuildIndex && initialSceneLoadedFirstTime)
        {
            initialSceneLoadedFirstTime = false;
            transitionDirection = TransitionDirection.None;
            return;
        }

        if (transitionDirection == TransitionDirection.Up)
        {
            float screenBottom = Camera.main.transform.position.y - Camera.main.orthographicSize;
            transform.position = new Vector2(transform.position.x, screenBottom);
        }
        else if (transitionDirection == TransitionDirection.Down)
        {
            float screenTop = Camera.main.transform.position.y + Camera.main.orthographicSize;
            transform.position = new Vector2(transform.position.x, screenTop);
        }

        transitionDirection = TransitionDirection.None;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {

        if (((1 << collision.gameObject.layer) & wallLayer) != 0)
        {
            foreach (ContactPoint2D contact in collision.contacts)
            {
                bool hitFromSide = Mathf.Abs(contact.normal.x) > Mathf.Abs(contact.normal.y);

                if (hitFromSide)
                {
                    // TODO: This might not be working, suspect that bounceFactor is always 1.0f
                    float bounceFactor = 2.0f; // Adjust for stronger bounce
                    if (!animator.GetBool("Bounced"))
                    {
                        animator.SetBool("Bounced", true);
                    }
                    else
                    {
                        bounceFactor = 1.0f;
                    }

                    float minBounceSpeed = 2.0f; // Ensure bounce even when velocity is 0
                    float upwardBounceSpeed = 3.0f; // Adjust for upward movement

                    // Invert horizontal velocity
                    float newXVelocity = -myRigidbody.linearVelocity.x * bounceFactor;

                    // Ensure minimum bounce force
                    if (Mathf.Abs(newXVelocity) < minBounceSpeed)
                    {
                        newXVelocity = -Mathf.Sign(contact.normal.x) * minBounceSpeed;
                    }

                    // Apply upward bounce force
                    float newYVelocity = Mathf.Max(myRigidbody.linearVelocity.y, upwardBounceSpeed);

                    // Apply new velocity
                    myRigidbody.linearVelocity = new Vector2(newXVelocity, newYVelocity);
                }
            }
        }
    }

    public bool IsGrounded()
    {
        if (Physics2D.BoxCast(transform.position, boxSize, 0, -transform.up, castDistance, groundLayer))
        {
            animator.SetBool("isGrounded", true);
            return true;
        }
        animator.SetBool("isGrounded", false);
        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position - transform.up * castDistance, boxSize);
    }
}
