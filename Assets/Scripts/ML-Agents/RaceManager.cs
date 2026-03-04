using UnityEngine;
using System.Collections.Generic;

public class RaceManager : MonoBehaviour
{
    public static bool isTraining = true;

    [SerializeField] private GameObject botPrefab;
    [SerializeField] private int botCount = 1;

    private List<GameObject> spawnedBots = new List<GameObject>();

    void Start()
    {
        Debug.Log("[RaceManager] Start called.");
        SpawnBots();
    }

    void SpawnBots()
    {
        Debug.Log($"[RaceManager] SpawnBots called. botPrefab is {(botPrefab != null ? botPrefab.name : "NULL")}");
        if (botPrefab == null)
        {
            return;
        }

        // Search for spawn points within the parent object's hierarchy using the "SpawnPoint" tag
        Transform searchRoot = transform.parent != null ? transform.parent : transform;
        List<Transform> foundSpawnPoints = new List<Transform>();
        
        foreach (Transform t in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.CompareTag("SpawnPoint"))
            {
                foundSpawnPoints.Add(t);
            }
        }

        Debug.Log($"[RaceManager] Found {foundSpawnPoints.Count} spawn points.");
        if (foundSpawnPoints.Count == 0)
        {
            return;
        }

        Debug.Log($"[RaceManager] Spawning {botCount} bots.");
        for (int i = 0; i < botCount; i++)
        {
            // Use modulo to cycle through spawn points if botCount > foundSpawnPoints.Count
            Transform spawnPoint = foundSpawnPoints[i % foundSpawnPoints.Count];
            
            GameObject bot = Instantiate(botPrefab, spawnPoint.position, spawnPoint.rotation, transform);
            Debug.Log($"[RaceManager] Instantiated {bot.name} at {spawnPoint.position}");
            
            // Deactivate the FishNet component as requested to prevent it from interfering locally
            if (bot.TryGetComponent<FishNet.Object.NetworkObject>(out var networkObject))
            {
                networkObject.enabled = false;
            }

            var runner = bot.GetComponent<BaseRunner>();
            if (runner != null)
            {
                runner.SetId(i);
                runner.UnFreeze();
            }

            spawnedBots.Add(bot);
        }
    }
}
