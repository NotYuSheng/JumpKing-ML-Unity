using UnityEngine;
using UnityEngine.SceneManagement;

public enum AIAction
{
    JumpLeft,
    JumpUp,
    JumpRight,
    MoveLeft,
    MoveRight,
    Wait
}

[System.Serializable]
public class AIMove
{
    public AIAction action;
    public float chargeDuration;
    public float moveDuration;

    public AIMove(AIAction action, float chargeDuration, float moveDuration)
    {
        this.action = action;
        this.chargeDuration = chargeDuration;
        this.moveDuration = moveDuration;
    }
}

[System.Serializable]
public class AIGene
{
    public AIMove[] moves = new AIMove[5];
}

public interface IPlayerInput
{
    float GetHorizontal();
    bool IsChargingJump();
    bool IsJumpReleased();
    void UpdateMove();
    int GetCurrentMoveIndex();
    void AdvanceMove();
}

public class KeyboardInput : IPlayerInput
{
    public float GetHorizontal() { return Input.GetAxisRaw("Horizontal"); }
    public bool IsChargingJump() { return Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S); }
    public bool IsJumpReleased() { return Input.GetKeyUp(KeyCode.DownArrow) || Input.GetKeyUp(KeyCode.S); }
    public void UpdateMove() { }
    public int GetCurrentMoveIndex() { return 0; }
    public void AdvanceMove() { }
}

public class AIInput : IPlayerInput
{
    public AIGene gene;
    private int currentMoveIndex = 0;
    private float moveStartTime;

    public AIInput(AIGene geneParameters)
    {
        gene = geneParameters;
        moveStartTime = Time.time;
    }

    public float GetHorizontal()
    {
        if (currentMoveIndex >= gene.moves.Length)
            return 0f;

        AIMove currentMove = gene.moves[currentMoveIndex];
        switch (currentMove.action)
        {
            case AIAction.MoveLeft: return -1f;
            case AIAction.MoveRight: return 1f;
            case AIAction.Wait: return 0f;
            case AIAction.JumpLeft: return -1f;
            case AIAction.JumpRight: return 1f;
            case AIAction.JumpUp: return 0f;
            default: return 0f;
        }
    }

    public bool IsChargingJump()
    {
        if (currentMoveIndex >= gene.moves.Length)
            return false;

        AIMove currentMove = gene.moves[currentMoveIndex];
        if (currentMove.action == AIAction.JumpLeft ||
            currentMove.action == AIAction.JumpUp ||
            currentMove.action == AIAction.JumpRight)
        {
            float elapsed = Time.time - moveStartTime;
            return elapsed < currentMove.chargeDuration;
        }
        return false;
    }

    public bool IsJumpReleased()
    {
        if (currentMoveIndex >= gene.moves.Length)
            return false;

        AIMove currentMove = gene.moves[currentMoveIndex];
        if (currentMove.action == AIAction.JumpLeft ||
            currentMove.action == AIAction.JumpUp ||
            currentMove.action == AIAction.JumpRight)
        {
            float elapsed = Time.time - moveStartTime;
            return (elapsed >= currentMove.chargeDuration && elapsed < currentMove.chargeDuration + 0.1f);
        }
        return false;
    }

    public void UpdateMove()
    {
        if (currentMoveIndex >= gene.moves.Length)
            return;

        AIMove currentMove = gene.moves[currentMoveIndex];
        float elapsed = Time.time - moveStartTime;
        if ((currentMove.action == AIAction.MoveLeft || currentMove.action == AIAction.MoveRight || currentMove.action == AIAction.Wait) &&
            elapsed >= currentMove.moveDuration)
        {
            Debug.Log($"AIInput: Advancing move {currentMoveIndex + 1}/{gene.moves.Length} - {currentMove.action} (Duration: {currentMove.moveDuration}s)");
            currentMoveIndex++;
            moveStartTime = Time.time;
        }
    }

    public int GetCurrentMoveIndex() { return currentMoveIndex; }

    public void AdvanceMove()
    {
        if (currentMoveIndex < gene.moves.Length)
        {
            currentMoveIndex++;
            moveStartTime = Time.time;
        }
    }
}

public class PlayerScript : MonoBehaviour
{
    // --- Public Fields & Components ---
    public Rigidbody2D myRigidbody;
    public BoxCollider2D myCollider;
    public float baseJumpForce = 9.5f;
    public float walkingSpeed = 3f;
    public float chargeRate = 1.5f;
    public float maxChargeMultiplier = 1f;
    public float horizontalChargeMultiplier = 0.5f;
    public int maxMoves = 7;
    public Vector2 boxSize = new Vector2(0.5f, 0.3f);
    public float castDistance = 0.375f;
    public LayerMask groundLayer;
    public LayerMask wallLayer;
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    public bool isAIControlled = true;

    // --- Private Fields ---
    private IPlayerInput playerInput;
    private bool isChargingJump = false;
    private float currentCharge = 0f;
    private float horizontalInput = 0f;
    public int moveCount = 0;
    public float fitness = 0f;

    private int initialSceneBuildIndex;
    private bool initialSceneLoadedFirstTime = true;
    private enum TransitionDirection { None, Up, Down }
    private TransitionDirection transitionDirection = TransitionDirection.None;
    public bool hasRegistered = false;

    // --- New Flags for Jump Logic ---
    private bool jumpExecuted = false;
    private bool wasGrounded = false;

    void Start()
    {
        myRigidbody.freezeRotation = true;
        initialSceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
        DontDestroyOnLoad(gameObject);
        // Assume playerInput is set externally or assign a default input here.
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void Update()
    {
        bool grounded = IsGrounded();
        horizontalInput = playerInput.GetHorizontal();

        // Update fitness (highest Y reached).
        if (transform.position.y > fitness)
            fitness = transform.position.y;

        // Apply horizontal movement when not charging jump.
        if (!isChargingJump && grounded)
            myRigidbody.linearVelocity = new Vector2(horizontalInput * walkingSpeed, myRigidbody.linearVelocity.y);

        // Start jump charge if grounded.
        if (!isChargingJump && grounded && playerInput.IsChargingJump())
        {
            isChargingJump = true;
            currentCharge = 0f;
            myRigidbody.linearVelocity = new Vector2(0f, myRigidbody.linearVelocity.y);

            animator.SetBool("isCharging", true);
            animator.SetBool("isLaunching", false);
            animator.SetBool("Bounced", false);
        }

        // Continue charging jump.
        if (isChargingJump && playerInput.IsChargingJump())
        {
            currentCharge += chargeRate * Time.deltaTime;
            currentCharge = Mathf.Min(currentCharge, maxChargeMultiplier);
        }

        // Execute jump when jump is released.
        if (isChargingJump && playerInput.IsJumpReleased())
        {
            animator.SetBool("isCharging", false);
            animator.SetBool("isGrounded", false);
            animator.SetBool("isLaunching", true);

            Vector2 jumpDirection = new Vector2(horizontalInput * horizontalChargeMultiplier, 1.5f).normalized;
            float finalJumpForce = baseJumpForce * currentCharge * 1.3f;
            myRigidbody.linearVelocity = jumpDirection * finalJumpForce;
            isChargingJump = false;
            jumpExecuted = true; // Mark that a jump has been performed.
        }

        // For movement/wait moves, let AIInput update the move index based on duration.
        if (playerInput is AIInput aiInput2)
        {
            aiInput2.UpdateMove();
            if (aiInput2.GetCurrentMoveIndex() > moveCount)
                moveCount = aiInput2.GetCurrentMoveIndex();
        }

        // After maxMoves, if the bot ends its turn off-screen (y position above current screen),
        // update the spawn location and move the camera up.
        if (moveCount >= maxMoves && !hasRegistered)
        {
            float currentScreenTop = Camera.main.transform.position.y + Camera.main.orthographicSize;
            if (transform.position.y > currentScreenTop)
            {
                EvolutionManager.Instance.spawnX = transform.position.x;
                EvolutionManager.Instance.spawnY = transform.position.y;

                Camera.main.transform.position = new Vector3(
                    Camera.main.transform.position.x,
                    Camera.main.transform.position.y + Camera.main.orthographicSize * 2,
                    Camera.main.transform.position.z);
            }
            hasRegistered = true;
            Debug.Log($"PlayerScript: Registering bot with fitness {fitness}, moveCount: {moveCount}");
            EvolutionManager.Instance.RegisterBot(this);
        }

        // Scene switching based on vertical position.
        float screenTop = Camera.main.transform.position.y + Camera.main.orthographicSize;
        float screenBottom = Camera.main.transform.position.y - Camera.main.orthographicSize;
        if (transform.position.y > screenTop)
            LoadNextScene();
        else if (transform.position.y < screenBottom)
            Destroy(gameObject);  // Destroy bot if it falls below the bottom of the camera view.

        if (Mathf.Abs(horizontalInput) > 0.1f && grounded && !isChargingJump)
            animator.SetBool("isWalking", true);
        else
            animator.SetBool("isWalking", false);
        animator.SetBool("isFalling", !grounded && myRigidbody.linearVelocity.y < 0);

        if (horizontalInput < 0 && grounded)
            spriteRenderer.flipX = true;
        else if (horizontalInput > 0 && grounded)
            spriteRenderer.flipX = false;

        // Advance jump move only after landing.
        bool groundedNow = IsGrounded();
        if (!wasGrounded && groundedNow && jumpExecuted)
        {
            if (playerInput is AIInput aiInput)
                aiInput.AdvanceMove();
            moveCount++;
            jumpExecuted = false;
        }
        wasGrounded = groundedNow;
    }

    void LoadNextScene()
    {
        // Load next scene is disabled for testing purposes.
        // int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        // if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        // {
        //     EvolutionManager.Instance.SetWinner(this);
        //     transitionDirection = TransitionDirection.Up;
        //     SceneManager.LoadScene(nextSceneIndex);
        // }
        // else
        // {
        //     Debug.Log("No more scenes available in Build Settings.");
        // }
    }

    void LoadPreviousScene()
    {
        // This function is no longer used since bots falling below the screen are now destroyed.
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

    public bool IsGrounded()
    {
        bool grounded = Physics2D.BoxCast(transform.position, boxSize, 0, -transform.up, castDistance, groundLayer);
        animator.SetBool("isGrounded", grounded);
        return grounded;
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
                    float bounceFactor = 2.0f;
                    if (!animator.GetBool("Bounced"))
                        animator.SetBool("Bounced", true);
                    else
                        bounceFactor = 1.0f;
                    float minBounceSpeed = 2.0f;
                    float upwardBounceSpeed = 3.0f;
                    float newXVelocity = -myRigidbody.linearVelocity.x * bounceFactor;
                    if (Mathf.Abs(newXVelocity) < minBounceSpeed)
                        newXVelocity = -Mathf.Sign(contact.normal.x) * minBounceSpeed;
                    float newYVelocity = Mathf.Max(myRigidbody.linearVelocity.y, upwardBounceSpeed);
                    myRigidbody.linearVelocity = new Vector2(newXVelocity, newYVelocity);
                }
            }
        }
    }

    public AIGene GetGene()
    {
        if (playerInput is AIInput aiInput)
            return aiInput.gene;
        return null;
    }

    public void SetGene(AIGene newGene)
    {
        playerInput = new AIInput(newGene);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position - transform.up * castDistance, boxSize);
    }
}
