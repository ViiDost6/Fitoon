using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic; // Necesario para usar List

public class RunnerAgent : Agent
{
    [Header("Configuración de Referencias")]
    [SerializeField] private Transform target;
    [SerializeField] private bool training = true;
    [SerializeField] private float fallLimit = -10.0f;

    private BotRunner controller;
    private RaceManager raceManager;
    
    private float episodeTimer = 0f;
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private const float stuckThreshold = 0.1f;

    // LISTA PARA CONTROLAR LOS POI RECOGIDOS
    private List<GameObject> collectedPOIs = new List<GameObject>();
    private List<GameObject> checkpointsCrossed = new List<GameObject>();

    public override void Initialize()
    {
        controller = GetComponent<BotRunner>();
        raceManager = FindFirstObjectByType<RaceManager>();
        
        if (target == null)
        {
            GameObject goal = GameObject.FindWithTag("Goal");
            if (goal != null) target = goal.transform;
        }
    }

    public override void OnEpisodeBegin()
    {
        if (raceManager != null) raceManager.RespawnBot(this.gameObject);

        episodeTimer = 0f;
        stuckTimer = 0f;
        lastPosition = transform.position;

        // LIMPIAR LA LISTA AL INICIO DE CADA EPISODIO
        collectedPOIs.Clear();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (target == null) return;
        sensor.AddObservation((target.position - transform.position).normalized);
        Rigidbody rb = controller.GetComponent<Rigidbody>();
        sensor.AddObservation(rb.linearVelocity / 10f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveV = Mathf.Clamp(actions.ContinuousActions[0], 0.0f, 1.0f);
        float moveH = Mathf.Clamp(actions.ContinuousActions[1], -1.0f, 1.0f);
        controller.SetMovement(moveV, moveH);

        episodeTimer += Time.fixedDeltaTime;
        AddReward(-0.1f * Time.fixedDeltaTime);

        if (Vector3.Distance(transform.position, lastPosition) < stuckThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > 5.0f)
            {
                AddReward(-1.0f);
                EndEpisode();
            }
        }
        else
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
        }

        if (transform.position.y < fallLimit)
        {
            AddReward(-2.0f);
            EndEpisode();
        }
    }

    // CONSOLIDADO: Solo un método OnTriggerEnter
    private void OnTriggerEnter(Collider collider)
    {
        if (collider.CompareTag("Goal"))
        {
            float baseReward = 100f;
            float timeBonus = Mathf.Max(0, 50f - (episodeTimer * 0.5f));
            AddReward(baseReward + timeBonus);
            EndEpisode();
        }
        else if (collider.CompareTag("POI"))
        {
            // VERIFICACIÓN DEL ARRAY (LISTA)
            if (!collectedPOIs.Contains(collider.gameObject))
            {
                // Es un POI nuevo
                AddReward(10f);
                collectedPOIs.Add(collider.gameObject);
            }
        }
        else if (collider.CompareTag("Checkpoint"))
        {
            if (!checkpointsCrossed.Contains(collider.gameObject))
            {
                AddReward(0.5f);
                checkpointsCrossed.Add(collider.gameObject);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (training)
        {
            if (collision.gameObject.CompareTag("Wall")) AddReward(-0.2f);
            if (collision.gameObject.CompareTag("Obstacle")) AddReward(-0.5f);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Vertical");
        continuousActions[1] = Input.GetAxisRaw("Horizontal");
    }
}