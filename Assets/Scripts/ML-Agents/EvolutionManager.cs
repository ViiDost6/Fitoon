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

    private List<GeneralistAgent> spawnedAgents = new List<GeneralistAgent>();
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
        // If you need genetic evolution, call InitializeEvolution() manually or via inspector.
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
        spawnedAgents = FindObjectsOfType<GeneralistAgent>().ToList();
        
        if (spawnedAgents.Count == 0) return;

        currentBrains.Clear();
        foreach (var agent in spawnedAgents)
        {
            BotBrain brain = new BotBrain(5, hiddenLayerSize, 2);
            agent.brain = brain;
            agent.useEvolutionBrain = true;
            currentBrains.Add(brain);
        }

        timer = 0f;
    }

    void AssignStoredBrains()
    {
        spawnedAgents = FindObjectsOfType<GeneralistAgent>().ToList();
        
        if (spawnedAgents.Count == 0) return;

        // Shuffle nextGenBrains array before assigning to give them variety in lanes
        for (int i = 0; i < currentBrains.Count; i++)
        {
            BotBrain temp = currentBrains[i];
            int randomIndex = Random.Range(i, currentBrains.Count);
            currentBrains[i] = currentBrains[randomIndex];
            currentBrains[randomIndex] = temp;
        }

        for (int i = 0; i < spawnedAgents.Count && i < currentBrains.Count; i++)
        {
            spawnedAgents[i].brain = currentBrains[i];
            spawnedAgents[i].useEvolutionBrain = true;
        }

        timer = 0f;
    }

    void Update()
    {
        if (spawnedAgents == null || spawnedAgents.Count == 0 || spawnedAgents.All(a => a == null)) 
        {
            spawnedAgents = FindObjectsOfType<GeneralistAgent>().ToList();
        }
        
        if (spawnedAgents.Count == 0) return;

        timer += Time.deltaTime;
        statsTimer -= Time.deltaTime;

        if (statsTimer <= 0 && spawnedAgents != null && spawnedAgents.Count > 0)
        {
            statsTimer = 1.0f; // Update stats once per second
            
            // Filter out nulls and agents not in play
            var activeAgents = spawnedAgents.Where(a => a != null).ToList();
            if (activeAgents.Count > 0)
            {
                averageReward = activeAgents.Average(a => a.GetCumulativeReward());
                lastMaxReward = activeAgents.Max(a => a.GetCumulativeReward());

                // Update All-Time Best
                if (lastMaxReward > bestReward)
                {
                    bestReward = lastMaxReward;
                    
                    // Trigger milestone check using the best current agent
                    var bestAgent = activeAgents.OrderByDescending(a => a.GetCumulativeReward()).FirstOrDefault();
                    if (bestAgent != null && bestAgent.brain != null)
                    {
                        CheckAndSaveMilestones(bestReward, bestAgent.brain);
                    }
                }
            }
        }
    }

    public void NextGeneration()
    {
        // Guard to prevent crashes if evolution is triggered but no brains are set up
        if (currentGeneration == 0 && (spawnedAgents == null || spawnedAgents.Count == 0)) return;
        
        currentGeneration++;
        
        // 1. Sort by performance (Filtering out nulls)
        var sortedAgents = spawnedAgents.Where(a => a != null).OrderByDescending(a => a.GetCumulativeReward()).ToList();
        
        // 2. Select top 10% (Ensuring we only take agents with valid brains)
        int selectCount = Mathf.Max(1, Mathf.RoundToInt(spawnedAgents.Count * 0.1f));
        List<BotBrain> bestBrains = sortedAgents.Take(selectCount)
            .Select(a => a.brain)
            .Where(b => b != null)
            .ToList();
            
        if (bestBrains.Count == 0) 
        {
            // If no valid brains, just reset everybody and exit
            foreach (var agent in spawnedAgents.Where(a => a != null)) agent.EndEpisode();
            return;
        }

        // 3. Generate New Population (Mutated)
        List<BotBrain> nextGenBrains = new List<BotBrain>();
        foreach (var best in bestBrains) nextGenBrains.Add(best.Clone()); // Elitism
        
        while (nextGenBrains.Count < spawnedAgents.Count)
        {
            BotBrain parent = bestBrains[Random.Range(0, bestBrains.Count)];
            BotBrain child = parent.Clone();
            child.Mutate(mutationRate, mutationStrength);
            nextGenBrains.Add(child);
        }

        // 4. Force Reset All Agents with their new mutated brains
        timer = 0f;
        
        // Safety: ensure our reference list is up to date
        spawnedAgents = FindObjectsOfType<GeneralistAgent>().ToList();

        for (int i = 0; i < spawnedAgents.Count; i++)
        {
            if (i < nextGenBrains.Count)
            {
                spawnedAgents[i].brain = nextGenBrains[i];
                spawnedAgents[i].useEvolutionBrain = true;
            }
            spawnedAgents[i].EndEpisode(); // Teleport back to start
        }
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

    [ContextMenu("Save Best Brain")]
    public void SaveBestBrain()
    {
        SaveBrainToFile(hallOfFameBrain, "BestGeneralistBrain.json");
    }

    private void CheckAndSaveMilestones(float reward, BotBrain brain)
    {
        if (reward >= easyThreshold && !PlayerPrefs.HasKey("Checkpoint_Easy"))
        {
            SaveBrainToFile(brain, "Brain_Easy.json");
            PlayerPrefs.SetInt("Checkpoint_Easy", 1);
        }
        if (reward >= mediumThreshold && !PlayerPrefs.HasKey("Checkpoint_Medium"))
        {
            SaveBrainToFile(brain, "Brain_Medium.json");
            PlayerPrefs.SetInt("Checkpoint_Medium", 1);
        }
        if (reward >= hardThreshold && !PlayerPrefs.HasKey("Checkpoint_Hard"))
        {
            SaveBrainToFile(brain, "Brain_Hard.json");
            PlayerPrefs.SetInt("Checkpoint_Hard", 1);
        }
    }

    private void SaveBrainToFile(BotBrain brain, string filename)
    {
        if (brain == null) return;
        
        // Save into the project folder for easier access and version control
        string mlFolder = "Assets/Scripts/ML-Agents/Bots/ML";
        string directoryPath = System.IO.Path.Combine(Application.dataPath, "..", mlFolder, folderName);
        
        // Ensure path is normalized for the OS
        directoryPath = System.IO.Path.GetFullPath(directoryPath);

        if (!System.IO.Directory.Exists(directoryPath))
        {
            System.IO.Directory.CreateDirectory(directoryPath);
        }

        string json = JsonUtility.ToJson(brain);
        string path = System.IO.Path.Combine(directoryPath, filename);
        System.IO.File.WriteAllText(path, json);
        
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    [ContextMenu("Load Easy Brain")] public void LoadEasy() => LoadBrainFromFile("Brain_Easy.json");
    [ContextMenu("Load Medium Brain")] public void LoadMedium() => LoadBrainFromFile("Brain_Medium.json");
    [ContextMenu("Load Hard Brain")] public void LoadHard() => LoadBrainFromFile("Brain_Hard.json");

    private void LoadBrainFromFile(string filename)
    {
        string mlFolder = "Assets/Scripts/ML-Agents/Bots/ML";
        string directoryPath = System.IO.Path.Combine(Application.dataPath, "..", mlFolder, folderName);
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(directoryPath, filename));
        
        if (System.IO.File.Exists(path))
        {
            string json = System.IO.File.ReadAllText(path);
            BotBrain loadedBrain = JsonUtility.FromJson<BotBrain>(json);
            
            // Distribute to all agents
            foreach (var agent in spawnedAgents)
            {
                agent.brain = loadedBrain.Clone();
                agent.useEvolutionBrain = true;
            }
        }
        else
        {
        }
    }
}
