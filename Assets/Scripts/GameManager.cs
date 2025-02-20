using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject aiPrefab; // Assign AIPlayer prefab in Inspector
    public int populationSize = 10;
    private List<AIPlayerScript> agents = new List<AIPlayerScript>();
    private float generationTime = 10f; // Time per generation
    private float timer = 0f;
    private int generation = 1;

    void Start()
    {
        CreateInitialPopulation();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= generationTime)
        {
            timer = 0f;
            EvaluateAndEvolve();
        }
    }

    void CreateInitialPopulation()
    {
        for (int i = 0; i < populationSize; i++)
        {
            GameObject newAgent = Instantiate(aiPrefab, new Vector2(Random.Range(-5f, 5f), 0f), Quaternion.identity);
            agents.Add(newAgent.GetComponent<AIPlayerScript>());
        }
    }

    void EvaluateAndEvolve()
    {
        // Sort by fitness (higher is better)
        agents.Sort((a, b) => b.fitness.CompareTo(a.fitness));

        Debug.Log($"Generation {generation}: Best Fitness = {agents[0].fitness}");

        List<AIPlayerScript> newGeneration = new List<AIPlayerScript>();

        // Keep top 2 agents as they are (elitism)
        newGeneration.Add(Instantiate(agents[0].gameObject).GetComponent<AIPlayerScript>());
        newGeneration.Add(Instantiate(agents[1].gameObject).GetComponent<AIPlayerScript>());

        // Mutate the rest
        for (int i = 2; i < populationSize; i++)
        {
            AIPlayerScript parent = agents[Random.Range(0, 2)]; // Select one of the best
            AIPlayerScript offspring = Instantiate(parent.gameObject).GetComponent<AIPlayerScript>();
            Mutate(offspring);
            newGeneration.Add(offspring);
        }

        // Destroy old generation
        foreach (var agent in agents)
        {
            Destroy(agent.gameObject);
        }

        agents = newGeneration;
        generation++;
    }

    void Mutate(AIPlayerScript agent)
    {
        agent.moveSpeed += Random.Range(-0.5f, 0.5f);
        agent.jumpForce += Random.Range(-1f, 1f);
        agent.decisionRate += Random.Range(-0.1f, 0.1f);

        agent.moveSpeed = Mathf.Clamp(agent.moveSpeed, 1f, 5f);
        agent.jumpForce = Mathf.Clamp(agent.jumpForce, 5f, 15f);
        agent.decisionRate = Mathf.Clamp(agent.decisionRate, 0.1f, 1f);
    }
}