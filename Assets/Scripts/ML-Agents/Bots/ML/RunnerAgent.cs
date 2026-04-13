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
        LazyInitialize();

        controller = GetComponent<BotRunner>();
        rb = GetComponent<Rigidbody>();
        raceManager = FindFirstObjectByType<RaceManager>();
        
        if (target == null)
        {
            GameObject goal = GameObject.FindWithTag("Goal");
            if (goal != null) target = goal.transform;
        }

        if (brains.Count > 0 && !training)
        {
            SetNNModel();
        }
    }

    private void SetNNModel()
    {
        int brainNum = UnityEngine.Random.Range(0, brains.Count);
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
        if (target == null) return;

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