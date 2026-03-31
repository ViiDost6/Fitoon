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
        if (botPrefab == null || foundSpawnPoints.Count == 0) return;

        for (int i = 0; i < botCount; i++)
        {
            // Instanciamos temporalmente fuera de vista
            GameObject bot = Instantiate(botPrefab, new Vector3(0, -100, 0), Quaternion.identity);
            spawnedBots.Add(bot);
            
            var runner = bot.GetComponent<BaseRunner>();
            if (runner != null) runner.SetId(i);
            
            RespawnBot(bot); 
        }
    }

    public void RespawnBot(GameObject bot)
    {
        if (this.gameObject.activeInHierarchy)
        {
            StartCoroutine(SafeRespawn(bot));
        }
    }

    private IEnumerator SafeRespawn(GameObject bot)
    {
        if (foundSpawnPoints.Count == 0) FindAllSpawnPointsInScene();
        if (foundSpawnPoints.Count == 0) yield break;

        // Selección de punto aleatorio
        Transform randomSpawn = foundSpawnPoints[Random.Range(0, foundSpawnPoints.Count)];

        if (bot.TryGetComponent<Rigidbody>(out var rb))
        {
            // CORRECCIÓN CRÍTICA: Solo reseteamos físicas si NO es cinemático
            // Esto evita el error "Setting angular velocity of a kinematic body is not supported"
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            
            // Lo hacemos cinemático temporalmente para moverlo sin interferencias
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
            // Doble limpieza post-movimiento para evitar "balas" por inercia acumulada
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        var runner = bot.GetComponent<BaseRunner>();
        if (runner != null) runner.UnFreeze();
    }
}