using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RaceManager : MonoBehaviour
{
    public static bool isTraining = true;

    [SerializeField] private GameObject botPrefab;
    [SerializeField] private int botCount = 1;
    [SerializeField] private float spawnHeightOffset = 0.5f; 

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
        if (botPrefab == null)
        {
            Debug.LogError("[RaceManager] botPrefab is null! Assign it in the inspector.");
            return;
        }
        if (foundSpawnPoints.Count == 0) return;

        for (int i = 0; i < botCount; i++)
        {
            // Instanciamos temporalmente fuera de vista
            GameObject bot = Instantiate(botPrefab, new Vector3(0, -100, 0), Quaternion.identity);
            if (bot == null) continue;

            spawnedBots.Add(bot);
            
            var runner = bot.GetComponent<BaseRunner>();
            if (runner != null) 
            {
                runner.SetId(i);
                // Ensure character model is loaded immediately so bot is visible
                runner.PickRandomBotCharacter();
            }
            
            RespawnBot(bot); 
        }
    }

    public void RespawnBot(GameObject bot)
    {
        if (bot == null) return;
        if (this.gameObject.activeInHierarchy)
        {
            StartCoroutine(SafeRespawn(bot));
        }
    }

    private IEnumerator SafeRespawn(GameObject bot)
    {
        if (bot == null) yield break;
        if (foundSpawnPoints.Count == 0) FindAllSpawnPointsInScene();
        if (foundSpawnPoints.Count == 0) yield break;

        // Selección de punto aleatorio
        Transform randomSpawn = foundSpawnPoints[Random.Range(0, foundSpawnPoints.Count)];

        if (bot.TryGetComponent<Rigidbody>(out var rb))
        {
            // CORRECCIÓN CRÍTICA: Solo reseteamos físicas si NO es cinemático
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            rb.isKinematic = true; 
        }

        // Posicionamiento
        bot.transform.position = randomSpawn.position + (Vector3.up * spawnHeightOffset);
        bot.transform.rotation = randomSpawn.rotation;

        // Esperamos un frame físico para que Unity asiente la nueva posición
        yield return new WaitForFixedUpdate();

        // Devolvemos el control físico al bot
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        var runner = bot.GetComponent<BaseRunner>();
        if (runner != null) runner.UnFreeze();
    }
}