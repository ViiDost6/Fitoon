using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class RunnerAgent : Agent
{
    [SerializeField] private Transform target;
    [SerializeField] private bool training = false;
    [SerializeField] private float fallLimit = -1.0f; // Límite de caída

    private BotRunner controller;
    private RaceManager raceManager;
    private Vector3 diff;
    private Vector3 lastDiff;
    private float backwards = 0f;

    // Variables para detección de estancamiento
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private const float stuckThreshold = 0.2f; 
    private const float maxStuckTime = 2.0f;   

    public override void Initialize()
    {
        controller = GetComponent<BotRunner>();
        raceManager = FindFirstObjectByType<RaceManager>();
        
        GameObject goal = GameObject.FindWithTag("Goal");
        if (goal != null) target = goal.transform;
    }

    public override void OnEpisodeBegin()
    {
        if (raceManager != null)
        {
            raceManager.RespawnBot(this.gameObject);
        }

        controller.enabled = true;
        controller.canMove = true;
        backwards = 0f;
        stuckTimer = 0f;
        lastPosition = transform.position;

        if (target != null)
        {
            lastDiff = (target.transform.localPosition - transform.localPosition) / 20;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (target == null) return;

        diff = (target.transform.localPosition - transform.localPosition) / 20;
        sensor.AddObservation(diff);

        if (diff.magnitude < lastDiff.magnitude) AddReward(0.01f);
        if (diff.magnitude > lastDiff.magnitude)
        {
            AddReward(-0.01f);
            backwards += 0.1f;
        }
        lastDiff = diff;

        AddReward(-0.005f); 
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveV = Mathf.Clamp(actions.ContinuousActions[0], 0.0f, 1.0f);
        float moveH = Mathf.Clamp(actions.ContinuousActions[1], -1.0f, 1.0f);
        controller.SetMovement(moveV, moveH);

        // PENALIZACIÓN POR CAÍDA DEL MAPA
        if (transform.position.y < fallLimit)
        {
            AddReward(-2.0f); // Penalización mayor que la de atascarse
            EndEpisode();
            return; // Salimos para evitar ejecutar el resto de la lógica este frame
        }

        if (moveV == 0 && moveH == 0)
        {
            AddReward(-0.01f);
        }

        // Lógica de estancamiento
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        if (distanceMoved < stuckThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
        }
        else
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
        }

        if (stuckTimer > maxStuckTime)
        {
            AddReward(-1.0f); 
            EndEpisode(); 
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Vertical");
        continuousActions[1] = Input.GetAxisRaw("Horizontal");
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.CompareTag("Goal"))
        {
            AddReward(500f);
            EndEpisode();
        }
        if (collider.gameObject.CompareTag("POI"))
        {
            AddReward(20f);
            collider.gameObject.SetActive(false);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (training)
        {
            if (collision.gameObject.CompareTag("Wall")) AddReward(-0.05f);
            if (collision.gameObject.CompareTag("Obstacle")) AddReward(-0.1f);
        }
    }
}