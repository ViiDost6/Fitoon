using UnityEngine;
using System.Collections.Generic;

public class RaceManager : MonoBehaviour
{
    public static bool isTraining = true;

    [SerializeField] private GameObject botPrefab;

    private List<GameObject> spawnedBots = new List<GameObject>();

    void Start()
    {
        SpawnBots();
    }

    void SpawnBots()
    {
        if (botPrefab == null)
        {
            Debug.LogWarning("RaceManager: Missing botPrefab.");
            return;
        }

        Transform[] allTransforms = FindObjectsOfType<Transform>();
        List<Transform> foundSpawnPoints = new List<Transform>();
        
        foreach (Transform t in allTransforms)
        {
            if (t.gameObject.name == "SP" || t.gameObject.name.StartsWith("SP ("))
            {
                // Only add if it doesn't have children (the parent container has children)
                if (t.childCount == 0)
                {
                    foundSpawnPoints.Add(t);
                }
            }
        }

        if (foundSpawnPoints.Count == 0)
        {
            Debug.LogWarning("RaceManager: No spawn points found with name 'SP'.");
            return;
        }

        for (int i = 0; i < foundSpawnPoints.Count; i++)
        {
            GameObject bot = Instantiate(botPrefab, foundSpawnPoints[i].position, foundSpawnPoints[i].rotation, transform);
            
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
