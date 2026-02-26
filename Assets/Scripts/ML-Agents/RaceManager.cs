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

        if (foundSpawnPoints.Count == 0)
        {
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
