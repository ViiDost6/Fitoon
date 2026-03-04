using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class RunnerAgent : Agent
{
    [Header("Target & Debug")]
    [SerializeField] private Transform target;
    [SerializeField] private bool training = false;
    [SerializeField] private bool debugLogs = true;

    private BotRunner controller;
    private Rigidbody rb;
    private Vector3 lastDiff;
    private float backwards = 0f;

    public Vector3 spawnPosition { get; private set; }
    public Quaternion spawnRotation { get; private set; }
    public bool spawnPositionCaptured { get; private set; } = false;
    private List<Transform> localGoalsCache = null;

    private void Awake()
    {
        Debug.Log($"[RunnerAgent] Awake on {gameObject.name}. Scripts are compiling!");
    }

    public override void Initialize()
    {
        Debug.Log($"[RunnerAgent] Initialize on {gameObject.name}");
        controller = GetComponent<BotRunner>();
        rb = GetComponent<Rigidbody>();
        
        CaptureSpawn();
        FindLocalTarget();
    }

    private void CaptureSpawn()
    {
        if (!spawnPositionCaptured)
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            spawnPositionCaptured = true;
            if (debugLogs) Debug.Log($"[RunnerAgent] Spawn Captured: {spawnPosition}");
        }
    }

    private void FindLocalTarget()
    {
        Transform searchRoot = transform.parent != null ? transform.parent : transform;
        if (localGoalsCache == null || localGoalsCache.Count == 0)
        {
            localGoalsCache = new List<Transform>();
            foreach (var col in searchRoot.GetComponentsInChildren<Collider>(true))
            {
                if (col.CompareTag("Goal")) localGoalsCache.Add(col.transform);
            }
        }

        float closestDist = Mathf.Infinity;
        Transform closestGoal = null;
        foreach (Transform goal in localGoalsCache)
        {
            if (goal == null) continue;
            float dist = Vector3.Distance(transform.position, goal.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestGoal = goal;
            }
        }

        if (closestGoal != null) target = closestGoal;
        else
        {
            GameObject globalGoal = GameObject.FindWithTag("Goal");
            if (globalGoal != null) target = globalGoal.transform;
        }
        
        if (debugLogs) Debug.Log($"[RunnerAgent] Target Found: {(target != null ? target.name : "NONE")}");
    }

    public void Respawn()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        if (rb != null)
        {
            rb.position = transform.position;
            rb.rotation = transform.rotation;
            rb.isKinematic = false;
        }
        
        Physics.SyncTransforms();
        if (controller != null) 
        {
            controller.UnFreeze();
            controller.SetMovement(0, 0);
        }
    }

    public override void OnEpisodeBegin()
    {
        if (debugLogs) Debug.Log($"[RunnerAgent] Episode Begin - Resetting Reward and Respawning");
        
        SetReward(0f); // Manual reset to ensure clean state
        CaptureSpawn();
        Respawn();
        FindLocalTarget();

        if (controller != null)
        {
            controller.enabled = true;
            controller.canMove = true;
        }

        // --- RESET DATA ---
        lastDiff = Vector3.zero; 
        backwards = 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        // Log entry to track if it's actually running
        // Debug.Log("[RunnerAgent] Collecting Observations...");

        if (target == null || rb == null) 
        {
            // IF TARGET IS NULL, WE STILL MUST ADD 5 VALUES
            for (int i = 0; i < 5; i++) sensor.AddObservation(0f);
            return;
        }

        Vector3 currentDiff = (target.position - transform.position) / 20f;
        
        // OBSERVATIONS (Exactly 5 floats)
        sensor.AddObservation(currentDiff.x); // 1
        sensor.AddObservation(currentDiff.z); // 2

        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        sensor.AddObservation(localVel.x / 10f); // 3
        sensor.AddObservation(localVel.z / 10f); // 4

        bool floor = Physics.Raycast(transform.position + transform.forward * 1.5f + Vector3.up, Vector3.down, 3f);
        sensor.AddObservation(floor ? 1f : 0f); // 5
        
        // Reward Logic
        float currentDist = currentDiff.magnitude;
        float lastDist = (lastDiff != Vector3.zero) ? lastDiff.magnitude : currentDist;

        if (currentDist < lastDist) 
        {
            AddReward(0.02f); // Reward moving towards target
            backwards = Mathf.Max(0f, backwards - 0.2f); // Reduce backwards suspicion
        }
        else if (currentDist > lastDist)
        {
            AddReward(-0.02f); // Penalize moving away
            backwards += 0.2f; // Increase backwards suspicion
        }
        lastDiff = currentDiff;
        AddReward(-0.005f); // Tiny time penalty to encourage speed
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Debug.Log($"[RunnerAgent] Action Received: V={actions.ContinuousActions[0]}, H={actions.ContinuousActions[1]}");
        
        float moveV = Mathf.Clamp(actions.ContinuousActions[0], 0.0f, 1.0f);
        float moveH = Mathf.Clamp(actions.ContinuousActions[1], -1.0f, 1.0f);
        
        if (controller != null) controller.SetMovement(moveV, moveH);

        if (transform.position.y < -5f)
        {
            AddReward(-10.0f); // Massive penalty for falling
            EndEpisode();
        }

        if (backwards > 20f || GetCumulativeReward() < -100f)
        {
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // IMPORTANT: NEVER DELETE THIS METHOD OR UNITY CRASHES IA LOGIC
        var continuousActions = actionsOut.ContinuousActions;
        
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");

        // Simple Auto-pilot if no keys
        if (Mathf.Abs(v) < 0.05f && Mathf.Abs(h) < 0.05f)
        {
            if (target == null) FindLocalTarget();
            if (target != null)
            {
                Vector3 toTarget = (target.position - transform.position).normalized;
                float angle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
                v = 0.8f; 
                h = Mathf.Clamp(angle / 30f, -1f, 1f); 
            }
        }

        continuousActions[0] = v;
        continuousActions[1] = h;
        
        // Trace to console
        // Debug.Log($"[RunnerAgent] Heuristic Output: {v}, {h}");
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.CompareTag("Goal"))
        {
            AddReward(2000f);
            EndEpisode();
        }
    }
}
