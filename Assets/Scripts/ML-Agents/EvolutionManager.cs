using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

public class EvolutionManager : MonoBehaviour
{
    public static EvolutionManager Instance { get; private set; }
    [Header("Evolution Settings")]
    [SerializeField] private int hiddenLayerSize = 10;
    [SerializeField] private float mutationRate = 0.1f;
    [SerializeField] private float mutationStrength = 0.2f;

    [Header("Difficulty Checkpoints")]
    [SerializeField] private float easyThreshold = 10f;
    [SerializeField] private float mediumThreshold = 50f;
    [SerializeField] private float hardThreshold = 150f;
    [SerializeField] private string folderName = "GeneticBrains";

    [Header("Generation Info")]
    [SerializeField] private int currentGeneration = 0;
    [SerializeField] private float timer = 0f;
    [SerializeField] private float bestReward = -Mathf.Infinity;
    [SerializeField]    private float averageReward = 0f;
    private float lastMaxReward = 0f;
    private float statsTimer = 0f;

    private List<RunnerAgent> spawnedAgents = new List<RunnerAgent>();
    private List<BotBrain> currentBrains = new List<BotBrain>();
    private BotBrain hallOfFameBrain; // Best brain ever found

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // We no longer auto-initialize evolution because it hijacks ONNX training.
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (currentGeneration > 0)
        {
            // We are continuing an evolution
            Invoke("AssignStoredBrains", 0.5f);
        }
    }

    void InitializeEvolution()
    {
        spawnedAgents = FindObjectsOfType<RunnerAgent>().ToList();
        
        if (spawnedAgents.Count == 0) return;

        currentBrains.Clear();
        foreach (var agent in spawnedAgents)
        {
            // RunnerAgent has 7 observations now
            BotBrain brain = new BotBrain(7, hiddenLayerSize, 2);
            // agent.brain = brain; // Note: RunnerAgent doesn't have a .brain property yet. 
            // Only adding it if needed, but for now just replacing the type.
            currentBrains.Add(brain);
        }

        timer = 0f;
    }

    void AssignStoredBrains()
    {
        spawnedAgents = FindObjectsOfType<RunnerAgent>().ToList();
        
        if (spawnedAgents.Count == 0) return;

        // Shuffle nextGenBrains array before assigning
        for (int i = 0; i < currentBrains.Count; i++)
        {
            BotBrain temp = currentBrains[i];
            int randomIndex = Random.Range(i, currentBrains.Count);
            currentBrains[i] = currentBrains[randomIndex];
            currentBrains[randomIndex] = temp;
        }

        for (int i = 0; i < spawnedAgents.Count && i < currentBrains.Count; i++)
        {
            // spawnedAgents[i].brain = currentBrains[i];
        }

        timer = 0f;
    }

    void Update()
    {
        if (spawnedAgents == null || spawnedAgents.Count == 0 || spawnedAgents.All(a => a == null)) 
        {
            spawnedAgents = FindObjectsOfType<RunnerAgent>().ToList();
        }
        
        if (spawnedAgents.Count == 0) return;

        timer += Time.deltaTime;
        statsTimer -= Time.deltaTime;

        if (statsTimer <= 0 && spawnedAgents != null && spawnedAgents.Count > 0)
        {
            statsTimer = 1.0f; // Update stats once per second
            
            var activeAgents = spawnedAgents.Where(a => a != null).ToList();
            if (activeAgents.Count > 0)
            {
                averageReward = activeAgents.Average(a => a.GetCumulativeReward());
                lastMaxReward = activeAgents.Max(a => a.GetCumulativeReward());

                if (lastMaxReward > bestReward)
                {
                    bestReward = lastMaxReward;
                    
                    var bestAgent = activeAgents.OrderByDescending(a => a.GetCumulativeReward()).FirstOrDefault();
                    // if (bestAgent != null && bestAgent.brain != null)
                    // {
                    //     CheckAndSaveMilestones(bestReward, bestAgent.brain);
                    // }
                }
            }
        }
    }

    public void NextGeneration()
    {
        if (currentGeneration == 0 && (spawnedAgents == null || spawnedAgents.Count == 0)) return;
        
        currentGeneration++;
        
        var sortedAgents = spawnedAgents.Where(a => a != null).OrderByDescending(a => a.GetCumulativeReward()).ToList();
        
        int selectCount = Mathf.Max(1, Mathf.RoundToInt(spawnedAgents.Count * 0.1f));
        // List<BotBrain> bestBrains = sortedAgents.Take(selectCount)
        //     .Select(a => a.brain)
        //     .Where(b => b != null)
        //     .ToList();
            
        // if (bestBrains.Count == 0) 
        // {
        //     foreach (var agent in spawnedAgents.Where(a => a != null)) agent.EndEpisode();
        //     return;
        // }

        // Rest of genetic logic...
    }

    private void OnGUI()
    {
        GUI.color = Color.black;
        GUILayout.BeginArea(new Rect(10, 10, 300, 150), GUI.skin.box);
        GUILayout.Label($"<b>Generation:</b> {currentGeneration}");
        GUILayout.Label($"<b>Elapsed Time:</b> {timer:F1}s");
        GUILayout.Label($"<b>Last Best:</b> {lastMaxReward:F2}");
        GUILayout.Label($"<b>All-Time Best:</b> {bestReward:F2}");
        GUILayout.Label($"<b>Average:</b> {averageReward:F2}");
        if (GUILayout.Button("Full Scene Reset (New Brains)"))
        {
            NextGeneration();
        }
        GUILayout.EndArea();
    }
}
