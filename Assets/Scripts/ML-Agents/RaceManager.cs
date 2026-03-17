using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RaceManager : MonoBehaviour
{
    public static bool isTraining = true;

    [SerializeField] private GameObject botPrefab;
    [SerializeField] private int botCount = 1;
    [SerializeField] private float spawnHeightOffset = 0.5f; // Elevación mínima para no chocar con el suelo

    private List<GameObject> spawnedBots = new List<GameObject>();
    private List<Transform> foundSpawnPoints = new List<Transform>();

    void Start()
    {
        FindAllSpawnPointsInScene(); 
        SpawnBots();
    }

    public void FindAllSpawnPointsInScene()
    {
        foundSpawnPoints.Clear();
        GameObject[] spawnObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");

        foreach (GameObject go in spawnObjects)
        {
            foundSpawnPoints.Add(go.transform);
        }

        if (foundSpawnPoints.Count == 0)
            Debug.LogError("[RaceManager] No hay objetos con el tag 'SpawnPoint' en la escena.");
    }

    void SpawnBots()
    {
        if (botPrefab == null || foundSpawnPoints.Count == 0) return;

        for (int i = 0; i < botCount; i++)
        {
            // Spawneamos lejos para evitar parpadeos y luego movemos
            GameObject bot = Instantiate(botPrefab, new Vector3(0,-100,0), Quaternion.identity);
            spawnedBots.Add(bot);
            
            var runner = bot.GetComponent<BaseRunner>();
            if (runner != null) runner.SetId(i);
            
            RespawnBot(bot); 
        }
    }

    public void RespawnBot(GameObject bot)
    {
        StartCoroutine(SafeRespawn(bot));
    }

    // Corrutina para asegurar que el posicionamiento sea limpio
    private IEnumerator SafeRespawn(GameObject bot)
    {
        if (foundSpawnPoints.Count == 0) FindAllSpawnPointsInScene();
        if (foundSpawnPoints.Count == 0) yield break;

        Transform randomSpawn = foundSpawnPoints[Random.Range(0, foundSpawnPoints.Count)];

        if (bot.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; 
        }

        // Aplicamos posición + un pequeño margen de altura para que no se "entierre"
        bot.transform.position = randomSpawn.position + (Vector3.up * spawnHeightOffset);
        bot.transform.rotation = randomSpawn.rotation;

        // Esperamos al final del frame para que el motor de física registre la posición
        yield return new WaitForFixedUpdate();

        if (rb != null) rb.isKinematic = false;

        var runner = bot.GetComponent<BaseRunner>();
        if (runner != null) runner.UnFreeze();
    }
}