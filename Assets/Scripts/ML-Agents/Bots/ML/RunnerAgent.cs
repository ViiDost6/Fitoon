using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;


public class RunnerAgent : Agent
{
    [Header("Configuración")]
    [SerializeField] private Transform target;
    [SerializeField] private bool training = true;
    [SerializeField] private float fallLimit = -10.0f;

    public List<Unity.InferenceEngine.ModelAsset> brains;

    private BotRunner controller;
    private RaceManager raceManager;
    private Rigidbody rb;
    
    private float episodeTimer = 0f;
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private float maxDistanceReached = 0f; // Para premiar el avance real

    private List<GameObject> collectedPOIs = new List<GameObject>();
    private List<GameObject> checkpointsCrossed = new List<GameObject>();

    public override void Initialize()
    {
        controller = GetComponent<BotRunner>();
        rb = GetComponent<Rigidbody>();
        raceManager = FindFirstObjectByType<RaceManager>();
        
        if (target == null)
        {
            GameObject goal = GameObject.FindWithTag("Goal");
            if (goal != null) target = goal.transform;
        }

        // Only initialize ML-Agents if we have a valid model or are in training mode
        if (training || (brains != null && brains.Count > 0))
        {
            Debug.Log($"[RunnerAgent] Initializing ML-Agents for {gameObject.name}. Training: {training}, Brains Available: {(brains != null ? brains.Count : 0)}");
            this.LazyInitialize();
        }
        else
        {
            Debug.LogWarning($"[RunnerAgent] Skipping ML-Agents initialization for {gameObject.name}. Training: {training}, Brains: {(brains != null ? brains.Count : 0)}");
        }
    }

    private void Start()
    {
        // Aseguramos que los componentes existen
        if (rb == null) rb = GetComponent<Rigidbody>();

        if (!training && brains != null && brains.Count > 0)
        {
            Debug.Log($"[RunnerAgent] Start() for {gameObject.name} - Loading inference model. Brains: {brains.Count}");
            // 1. Ensure ML-Agents is initialized if it wasn't in Initialize()
            // (it might not have been initialized if brains was null at that time)
            try
            {
                this.LazyInitialize();
            }
            catch (System.Exception ex)
            {
                // If already initialized or initialization fails, continue
                Debug.LogWarning($"[RunnerAgent] LazyInitialize in Start() failed or already initialized for {gameObject.name}: {ex.Message}");
            }
            
            // 2. Cambiamos el modelo
            SetNNModel();
        }
        else if (!training)
        {
            Debug.LogWarning($"[RunnerAgent] Start() for {gameObject.name} - Training mode OFF but no brains available. Brains: {(brains != null ? brains.Count : 0)}");
        }
    }

    private void SetNNModel()
    {
        if (brains == null || brains.Count == 0)
        {
            Debug.LogError($"[RunnerAgent] Cannot set model for {gameObject.name}. Brains list is null or empty!");
            return;
        }

        int brainNum = UnityEngine.Random.Range(0, brains.Count);
        string brainName = brains[brainNum] != null ? brains[brainNum].name : "null";
        Debug.Log($"[RunnerAgent] Assigning brain #{brainNum} ({brainName}) to {gameObject.name}. Total brains available: {brains.Count}");
        this.SetModel("Runner", brains[brainNum]);
    }

    public override void OnEpisodeBegin()
    {
        if (raceManager != null) raceManager.RespawnBot(this.gameObject);
        
        episodeTimer = 0f;
        stuckTimer = 0f;
        maxDistanceReached = 0f; 
        lastPosition = transform.position;
        
        collectedPOIs.Clear();
        checkpointsCrossed.Clear();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (target == null || rb == null) 
        {
            return; 
        }

        // 1. Dirección y Distancia (Normalizada)
        Vector3 toTarget = target.position - transform.position;
        sensor.AddObservation(toTarget.normalized);
        sensor.AddObservation(toTarget.magnitude / 100f); 

        // 2. Velocidad propia (Crucial para saber si puede frenar)
        sensor.AddObservation(rb.linearVelocity / 15f);

        // 3. Orientación respecto al objetivo
        sensor.AddObservation(Vector3.Dot(transform.forward, toTarget.normalized));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Suavizado de entrada: evitamos giros nerviosos
        float moveV = Mathf.Clamp(actions.ContinuousActions[0], 0.0f, 1.0f);
        float moveH = Mathf.Clamp(actions.ContinuousActions[1], -1.0f, 1.0f);
        
        if (target == null || rb == null || controller == null) 
        {
            return; 
        }

        // CRITICAL: Apply movement to controller
        controller.SetMovement(moveV, moveH);

        // --- RECOMPENSAS DINÁMICAS ---

        // 1. Castigo por tiempo (Incentiva la urgencia)
        episodeTimer += Time.fixedDeltaTime;
        AddReward(-0.001f); 

        // 2. Recompensa por Progresión (Evita que den vueltas en círculos)
        float currentDist = Vector3.Distance(transform.position, target.position);
        float distanceReward = (lastPosition.magnitude - transform.position.magnitude); 
        // Si se acerca al objetivo, pequeño premio constante
        if (currentDist < Vector3.Distance(lastPosition, target.position))
            AddReward(0.01f);

        // 3. Guía de Velocidad Optimizada
        float currentSpeed = rb.linearVelocity.magnitude;
        float targetMaxSpeed = controller.GetBaseSpeed() * controller.GetSpeedMultiplier();
        
        if (currentSpeed > 1.0f) {
            float speedRatio = currentSpeed / targetMaxSpeed;
            // Premiamos ir rápido, pero solo si va en la dirección correcta (Dot Product)
            float directionLook = Vector3.Dot(transform.forward, (target.position - transform.position).normalized);
            AddReward(0.02f * speedRatio * Mathf.Clamp01(directionLook));
        }

        // 4. Control de Estancamiento Riguroso
        if (Vector3.Distance(transform.position, lastPosition) < 0.05f)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > 2f) { AddReward(-5f); EndEpisode(); }
        }
        else { stuckTimer = 0f; }

        lastPosition = transform.position;

        if (transform.position.y < fallLimit) { AddReward(-15f); EndEpisode(); }
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (collider.CompareTag("Goal"))
        {
            // Bonus por tiempo mucho más agresivo
            float timeBonus = Mathf.Max(0, 100f - (episodeTimer * 2f));
            AddReward(150f + timeBonus); 
            EndEpisode();
        }
        else if (collider.CompareTag("POI") && !collectedPOIs.Contains(collider.gameObject))
        {
            AddReward(15f); // Subimos valor de POI para incentivar desvíos calculados
            collectedPOIs.Add(collider.gameObject);
        }
        else if (collider.CompareTag("Checkpoint") && !checkpointsCrossed.Contains(collider.gameObject))
        {
            AddReward(5f); // Checkpoints ahora valen más para guiar el camino
            checkpointsCrossed.Add(collider.gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (training)
        {
            // Castigo masivo por choque para forzar aprendizaje de frenado
            if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle")) 
            {
                AddReward(-15f); 
                // Opcional: EndEpisode(); // Descomenta si quieres que aprendan a NO tocar nada nunca
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetKey(KeyCode.W) ? 1f : 0f;
        continuousActions[1] = Input.GetAxis("Horizontal");
    }
}