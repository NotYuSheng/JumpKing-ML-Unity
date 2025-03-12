using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class EvolutionManager : MonoBehaviour
{
    // Singleton instance.
    public static EvolutionManager Instance;

    [Header("Settings")]
    public GameObject playerPrefab;
    public int generationSize = 500;
    public int maxMoves = 7;
    public float spawnX = 0f;
    public float spawnY = 0f;
    public float mutationRate = 0.1f; // Mutation chance per move (10%)

    [Header("UI")]
    public TMP_Text generationText;

    // Internal state.
    private List<PlayerScript> currentGeneration = new List<PlayerScript>();
    private int generationCount = 0;
    private int botsFinished = 0;
    private float generationStartTime; // For generation timeout tracking

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // Disable collisions among AI players before spawning any bots.
        int aiLayer = LayerMask.NameToLayer("AIPlayer");
        Physics2D.IgnoreLayerCollision(aiLayer, aiLayer, true);

        SpawnNewGeneration();
    }

    void Update()
    {
        // If the generation takes more than 10 seconds and not all bots are finished, force evaluation.
        if (Time.time - generationStartTime > 10f && botsFinished < generationSize)
        {
            Debug.Log("Generation timed out. Forcing evaluation.");
            EvaluateGeneration(true);
        }
    }

    /// <summary>
    /// Check if a bot is already registered.
    /// </summary>
    public bool IsBotRegistered(PlayerScript bot)
    {
        return currentGeneration.Contains(bot);
    }

    /// <summary>
    /// Called by a bot when it has finished its moves.
    /// </summary>
    public void RegisterBot(PlayerScript bot)
    {
        if (!currentGeneration.Contains(bot))
            currentGeneration.Add(bot);

        botsFinished++;
        Debug.Log($"RegisterBot: Bot finished with fitness {bot.fitness}. Total bots finished: {botsFinished}/{generationSize}");

        if (botsFinished >= generationSize)
            EvaluateGeneration();
    }

    /// <summary>
    /// Checks if all bots have executed the maximum number of moves.
    /// </summary>
    private bool AllBotsFinished()
    {
        if (currentGeneration.Count == 0)
        {
            Debug.LogWarning("AllBotsFinished: No bots in the current generation!");
            return false;
        }

        foreach (var bot in currentGeneration)
        {
            Debug.Log($"AllBotsFinished: Bot fitness = {bot.fitness}, moveCount = {bot.moveCount}/{maxMoves}");
            if (bot.moveCount < maxMoves)
                return false;
        }

        Debug.Log("AllBotsFinished: All bots have completed their moves.");
        return true;
    }

    /// <summary>
    /// Evaluates the generation’s performance and then spawns a new generation.
    /// If force is true, evaluation happens even if not all bots finished.
    /// </summary>
    private void EvaluateGeneration(bool force = false)
    {
        if (!force && !AllBotsFinished())
        {
            Debug.LogWarning("EvaluateGeneration: Called before all bots finished! Aborting.");
            return;
        }

        Debug.Log("Evaluating generation:");
        foreach (var bot in currentGeneration)
        {
            Debug.Log($"Bot fitness: {bot.fitness}, Moves Executed: {bot.moveCount}");
        }

        SpawnNewGeneration();
    }

    /// <summary>
    /// Destroys the current generation and spawns a new generation of bots.
    /// </summary>
    public void SpawnNewGeneration()
    {
        botsFinished = 0;
        generationCount++;
        UpdateGenerationText();

        // Set the start time for the new generation.
        generationStartTime = Time.time;

        // If not the first generation, select top-performing genes.
        List<AIGene> topGenes = generationCount > 1 ? SelectTopBots() : new List<AIGene>();

        // Destroy all existing bots.
        foreach (PlayerScript bot in FindObjectsByType<PlayerScript>(FindObjectsSortMode.None))
        {
            Destroy(bot.gameObject);
        }
        currentGeneration.Clear();

        for (int i = 0; i < generationSize; i++)
        {
            GameObject newBot = Instantiate(playerPrefab, new Vector2(spawnX, spawnY), Quaternion.identity);
            PlayerScript ps = newBot.GetComponent<PlayerScript>();

            if (ps != null)
            {
                ps.isAIControlled = true;
                ps.hasRegistered = false;

                if (topGenes.Count > 1)
                {
                    // Crossover: pick two random parents from the selected top genes and apply mutation.
                    AIGene parent1 = topGenes[Random.Range(0, topGenes.Count)];
                    AIGene parent2 = topGenes[Random.Range(0, topGenes.Count)];
                    ps.SetGene(Crossover(parent1, parent2));
                }
                else
                {
                    ps.SetGene(GenerateRandomGene());
                }
            }
        }
    }

    /// <summary>
    /// Generates a random gene with moves.
    /// </summary>
    private AIGene GenerateRandomGene()
    {
        AIGene gene = new AIGene();
        // Assuming the AIAction enum has 6 values: JumpLeft, JumpUp, JumpRight, MoveLeft, MoveRight, Wait.
        int totalActions = System.Enum.GetValues(typeof(AIAction)).Length;

        for (int i = 0; i < gene.moves.Length; i++)
        {
            // JumpUp and Wait actions were initially added for the snow level, but has been disabled
            //AIAction action = (AIAction)Random.Range(0, totalActions);
            AIAction action = (AIAction)Random.Range(0, 3);
            float chargeDuration = 0f;
            float moveDuration = 0f;

            switch (action)
            {
                // For jump actions, use a random charge duration.
                case AIAction.JumpLeft:
                //case AIAction.JumpUp:
                case AIAction.JumpRight:
                    chargeDuration = Random.Range(0.2f, 1.5f);
                    break;
                // For horizontal moves and waiting, use a random move duration.
                case AIAction.MoveLeft:
                case AIAction.MoveRight:
                //case AIAction.Wait:
                    moveDuration = Random.Range(1f, 3f);
                    break;
            }

            gene.moves[i] = new AIMove(action, chargeDuration, moveDuration);
        }
        return gene;
    }

    /// <summary>
    /// Sets the winning bot by destroying all other bots.
    /// </summary>
    public void SetWinner(PlayerScript winner)
    {
        foreach (PlayerScript bot in FindObjectsByType<PlayerScript>(FindObjectsSortMode.None))
        {
            if (bot != winner)
                Destroy(bot.gameObject);
        }
    }

    /// <summary>
    /// Updates the UI to reflect the current generation count.
    /// </summary>
    private void UpdateGenerationText()
    {
        if (generationText != null)
            generationText.text = "Generation: " + generationCount;
    }

    /// <summary>
    /// Selects the top-performing bots and returns their genes.
    /// </summary>
    private List<AIGene> SelectTopBots()
    {
        if (currentGeneration.Count == 0)
        {
            Debug.LogWarning("SelectTopBots: No bots available for selection.");
            return new List<AIGene>();
        }

        // Sort bots by fitness in descending order.
        currentGeneration.Sort((a, b) => b.fitness.CompareTo(a.fitness));

        int numSelected = Mathf.Max(1, generationSize / 2);
        List<AIGene> selectedGenes = new List<AIGene>();

        Debug.Log($"SelectTopBots: Selecting {numSelected} bots from {currentGeneration.Count} total bots.");
        for (int i = 0; i < numSelected && i < currentGeneration.Count; i++)
        {
            selectedGenes.Add(currentGeneration[i].GetGene());
            Debug.Log($"Selected Bot {i + 1}: Fitness = {currentGeneration[i].fitness}");
        }
        return selectedGenes;
    }

    /// <summary>
    /// Performs a simple crossover between two parent genes to produce an offspring gene.
    /// Applies mutation on each move with a chance defined by mutationRate.
    /// </summary>
    private AIGene Crossover(AIGene parent1, AIGene parent2)
    {
        AIGene offspring = new AIGene();
        int crossoverPoint = Random.Range(0, parent1.moves.Length);
        for (int i = 0; i < offspring.moves.Length; i++)
        {
            // Choose move from one of the parents.
            AIMove move = i < crossoverPoint ? parent1.moves[i] : parent2.moves[i];

            // Increase mutation chance for the early moves if desired.
            float adjustedMutationRate = mutationRate;
            if (i < 2)
                adjustedMutationRate *= 1.5f; // 50% more likely to mutate early moves

            // Apply mutation based on the adjusted probability.
            if (Random.value < adjustedMutationRate)
            {
                move = MutateMove(move);
            }

            offspring.moves[i] = move;
        }
        return offspring;
    }

    /// <summary>
    /// Returns a mutated version of the given move by generating a new random move.
    /// </summary>
    private AIMove MutateMove(AIMove move)
    {
        int totalActions = System.Enum.GetValues(typeof(AIAction)).Length;
        AIAction newAction = (AIAction)Random.Range(0, totalActions);
        float newChargeDuration = 0f;
        float newMoveDuration = 0f;

        switch (newAction)
        {
            case AIAction.JumpLeft:
            case AIAction.JumpUp:
            case AIAction.JumpRight:
                newChargeDuration = Random.Range(0.5f, 1.5f);
                break;
            case AIAction.MoveLeft:
            case AIAction.MoveRight:
            case AIAction.Wait:
                newMoveDuration = Random.Range(1f, 3f);
                break;
        }
        return new AIMove(newAction, newChargeDuration, newMoveDuration);
    }

    /// <summary>
    /// Recursively sets the layer of a GameObject and all its children.
    /// </summary>
    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child != null)
                SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
