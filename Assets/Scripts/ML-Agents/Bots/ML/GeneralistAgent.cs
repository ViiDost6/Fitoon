using UnityEngine;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class GeneralistAgent : Agent
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 15f; // Increased from 10 to 15 for faster racing
    [SerializeField] private float turnSpeed = 120f; 

    [Header("Curriculum Settings")]
    [Range(0, 1)]
    [SerializeField] private float difficultyLevel = 0f; // 0.0=Easy, 0.5=Medium, 1.0=Hard

    private Rigidbody rb;
    private BotRunner controller;
    private Transform nextTarget;
    public Vector3 spawnPosition { get; private set; }
    public Quaternion spawnRotation { get; private set; }
    private bool hasSetSpawn = false;
    public bool spawnPositionCaptured { get; private set; } = false;
    private float previousDistance = Mathf.Infinity;
    private float lastTurnAction = 0f;
    private List<Transform> localGoalsCache = null;
    private Vector3 lastFramePosition;

    public float Difficulty { get => difficultyLevel; set => difficultyLevel = Mathf.Clamp01(value); }
    
    [Header("Evolution Brain")]
    public BotBrain brain;
    public bool useEvolutionBrain = false;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        controller = GetComponent<BotRunner>();
    }

    public override void OnEpisodeBegin()
    {
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (!spawnPositionCaptured)
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            spawnPositionCaptured = true;
            hasSetSpawn = true;
        }

        previousDistance = Mathf.Infinity;
        hasFallen = false;
        lastFramePosition = transform.position;
        
        Respawn();
        FindLocalTarget();
    }

    public void Respawn()
    {
        // Force reset the Rigidbody state to stop all momentum
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true; 
        }

        // Force teleport both Transform and Rigidbody
        transform.position = spawnPosition + Vector3.up * 0.1f;
        transform.rotation = spawnRotation;
        
        if (rb != null)
        {
            rb.position = transform.position;
            rb.rotation = transform.rotation;
            rb.isKinematic = false;
        }

        // Force Unity to acknowledge the new position immediately
        Physics.SyncTransforms();

        if (controller != null)
        {
            controller.UnFreeze();
            controller.SetMovement(0, 0);
        }

        hasFallen = false;
        previousDistance = Mathf.Infinity;
    }

    private void FindLocalTarget()
    {
        // Optimization: Search only within our local training area (parent)
        // rather than the entire scene with FindGameObjectsWithTag.
        Transform searchRoot = transform.parent != null ? transform.parent : transform;
        
        float closestDist = Mathf.Infinity;
        Transform closestGoal = null;

        if (localGoalsCache == null || localGoalsCache.Count == 0)
        {
            localGoalsCache = new List<Transform>();
            foreach (var col in searchRoot.GetComponentsInChildren<Collider>(true))
            {
                if (col.CompareTag("Goal"))
                {
                    localGoalsCache.Add(col.transform);
                }
            }
        }

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

        if (closestGoal != null)
        {
            nextTarget = closestGoal;
        }
    }

    public void SetTarget(Transform target)
    {
        nextTarget = target;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Difficulty Context (Scalar)
        sensor.AddObservation(difficultyLevel);

        if (nextTarget == null)
        {
            sensor.AddObservation(new float[4]); // Direction and Dot/Angle placeholders
            return;
        }

        // 2. Relative Heading (Map-Agnostic)
        Vector3 toTarget = (nextTarget.position - transform.position).normalized;
        float dotProduct = Vector3.Dot(transform.forward, toTarget);
        float angleToTarget = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up) / 180f;

        sensor.AddObservation(dotProduct);
        sensor.AddObservation(angleToTarget);
        
        // Velocity context
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        sensor.AddObservation(localVelocity.x / moveSpeed);
        sensor.AddObservation(localVelocity.z / moveSpeed);

        // 3. Ground Presence Sensing (Anti-Fall "Eyes")
        // Check 1.5m and 3m ahead for floor
        bool floorAheadShort = Physics.Raycast(transform.position + transform.forward * 1.5f + Vector3.up, Vector3.down, 3f);
        bool floorAheadLong = Physics.Raycast(transform.position + transform.forward * 3.0f + Vector3.up, Vector3.down, 3f);
        sensor.AddObservation(floorAheadShort ? 1f : 0f);
        sensor.AddObservation(floorAheadLong ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveAction = actions.ContinuousActions[0]; // Forward
        float turnAction = actions.ContinuousActions[1]; // Rotation

        // --- STEERING STABILIZATION (Smoothens pathing) ---
        // Penalize the "jitter" caused by alternating left-right too fast
        float turnChange = Mathf.Abs(turnAction - lastTurnAction);
        if (turnChange > 0.05f) 
        {
            // Much stricter penalty for jitter
            AddReward(-0.005f * turnChange);
        }
        
        // Extra penalty for any turning if we are already mostly facing the goal
        if (nextTarget != null)
        {
            float alignment = Vector3.Dot(transform.forward, (nextTarget.position - transform.position).normalized);
            if (alignment > 0.95f && Mathf.Abs(turnAction) > 0.1f)
            {
                AddReward(-0.001f);
            }
        }

        lastTurnAction = turnAction;

        ApplyMovement(moveAction, turnAction);
    }

    private void ApplyMovement(float moveAction, float turnAction)
    {
        if (controller != null)
        {
            controller.SetMovement(moveAction, turnAction);
        }
        else
        {
            // Fallback if controller is missing
            rb.MovePosition(transform.position + transform.forward * moveAction * moveSpeed * Time.fixedDeltaTime);
            transform.Rotate(Vector3.up, turnAction * turnSpeed * Time.fixedDeltaTime);
        }

        // Anti-Spin and motion logic continues here...

        // --- SHORTEST PATH HEURISTIC ---
        // 1. Time Penalty: Every second costs points. Finishing fast is a requirement.
        AddReward(-0.005f); // Increased from 0.002 to make speed more critical

        // 2. Distance Penalty: Every meter traveled costs points.
        // This forces the bot to find the most direct route (Shortest Path).
        float distThisFrame = Vector3.Distance(transform.position, lastFramePosition);
        if (distThisFrame > 0.001f)
        {
            AddReward(-distThisFrame * 0.1f); // 10m = -1.0 point. 
        }
        lastFramePosition = transform.position;

        // --- ANTI-SPINNING & MOTION LOGIC ---
        float absTurn = Mathf.Abs(turnAction);
        
        // 1. Encourage Forward > Turning (Ratio-based)
        if (moveAction > 0.5f) // Higher threshold for "aggressive" movement
        {
            // Reward for maintaining high speed (Aggression Bonus)
            AddReward(moveAction * 0.005f);
        }
        else if (moveAction < 0.2f)
        {
            // Slowness penalty: We don't want bots that "creep" slowly
            AddReward(-0.005f);
        }
        else
        {
            // 2. Penalty for spinning in place or being static
            // High turn with low move is a heavy penalty
            if (absTurn > 0.5f) 
            {
                AddReward(-0.01f * absTurn); 
            }
            else 
            {
                AddReward(-0.002f); // Generic static penalty
            }
        }

        // 3. Physical Rotation Penalty (Detects actual spinning)
        if (rb != null && Mathf.Abs(rb.angularVelocity.y) > 2.0f)
        {
            AddReward(-0.005f);
        }

        // --- PROGRESS & ALIGNMENT REWARDS ---
        if (nextTarget != null)
        {
            float currentDistance = Vector3.Distance(transform.position, nextTarget.position);
            Vector3 directionToGoal = (nextTarget.position - transform.position).normalized;
            
            // 1. VELOCITY-BASED PROGRESS (More robust than distance)
            // Reward the bot for its SPEED directed towards the target
            float speedTowardGoal = Vector3.Dot(rb.linearVelocity, directionToGoal);
            
            // --- STRAIGHT LINE EXCELLENCE ---
            // Calculate how much of our speed is "wasted" laterally
            Vector3 lateralVelocity = rb.linearVelocity - (directionToGoal * speedTowardGoal);
            float lateralSpeed = lateralVelocity.magnitude;
            
            if (speedTowardGoal > 0.1f)
            {
                // AGGRESSIVE VELOCITY REWARD
                // We reward the SQUARE of the speed to favor high-intensity sprinting
                float normalizedSpeed = speedTowardGoal / moveSpeed;
                AddReward(Mathf.Pow(normalizedSpeed, 2) * 0.02f); 
            }
            else
            {
                // Stiffer penalty for backtracking or being stalled
                AddReward(-0.01f); 
                if (speedTowardGoal < 0) AddReward(speedTowardGoal * 0.1f);
            }

            // 2. Alignment Reward: Focus on the heading
            Vector3 moveVec = (transform.forward * moveAction) + (transform.right * turnAction);
            float alignment = Vector3.Dot(moveVec.normalized, directionToGoal);
            
            if (alignment > 0.5f) 
            {
                // Removed intermediate alignment reward
            }
            else if (alignment < -0.1f)
            {
                // STRONGLY PENALIZE MOVING AWAY FROM GOAL
                AddReward(alignment * 0.02f); 
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // 1. If we are testing the old JSON genetic algorithm logic manually
        if (useEvolutionBrain && brain != null && brain.outputSize >= 2)
        {
            float[] obs = GetObservationsArray();
            float[] outputs = brain.Predict(obs);
            
            if (outputs != null && outputs.Length >= 2)
            {
                var actions = actionsOut.ContinuousActions;
                actions[0] = outputs[0];
                actions[1] = outputs[1];
                return;
            }
        }
        
        // 2. Standard Keyboard Control (WASD / Arrows)
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");

        // --- SMART ASSIST (The "Upgraded Heuristic") ---
        
        // A. Prevent Falling Off: If we are heading for a cliff, override forward input
        bool floorAhead = Physics.Raycast(transform.position + transform.forward * 1.5f + Vector3.up, Vector3.down, 3f);
        if (!floorAhead && v > 0) 
        {
            v = -0.5f; // Auto-brake/Back up
            Debug.DrawRay(transform.position + transform.forward * 1.5f, Vector3.down * 3, Color.red);
        }

        // B. Anti-Spin Assist: If we are already spinning too fast, dampen horizontal input
        if (Mathf.Abs(rb.angularVelocity.y) > 5f)
        {
            h = -Mathf.Sign(rb.angularVelocity.y); // Counter-steer
        }

        // C. Goal Locking: If user isn't pressing keys, nudge bot to face the goal
        if (Mathf.Abs(v) < 0.1f && Mathf.Abs(h) < 0.1f && nextTarget != null)
        {
            float angle = Vector3.SignedAngle(transform.forward, (nextTarget.position - transform.position).normalized, Vector3.up);
            if (Mathf.Abs(angle) > 10f) h = Mathf.Sign(angle) * 0.5f;
        }

        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = v;
        continuousActions[1] = h;

        // 3. Debug Tools & Shortcuts
        
        // Manual Reset: Hit 'R' to instantly restart the episode during manual testing
        if (Input.GetKeyDown(KeyCode.R)) 
        {
            EndEpisode();
            return;
        }

        // Curriculum Slider Testing
        if (Input.GetKeyDown(KeyCode.Alpha1)) difficultyLevel = 0.0f; // Easy
        if (Input.GetKeyDown(KeyCode.Alpha2)) difficultyLevel = 0.5f; // Medium
        if (Input.GetKeyDown(KeyCode.Alpha3)) difficultyLevel = 1.0f; // Hard

        // Target Visualizer: Draws a line in the Editor Scene view to see where the bot is aiming
        if (nextTarget != null) 
        {
            Debug.DrawLine(transform.position + Vector3.up, nextTarget.position, Color.yellow);
        }
    }

    public float[] GetObservationsArray()
    {
        float[] obs = new float[5];
        obs[0] = difficultyLevel;

        if (nextTarget == null)
        {
            obs[1] = 0f;
            obs[2] = 0f;
        }
        else
        {
            Vector3 toTarget = (nextTarget.position - transform.position).normalized;
            obs[1] = Vector3.Dot(transform.forward, toTarget);
            obs[2] = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up) / 180f;
        }

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        obs[3] = localVelocity.x / moveSpeed;
        obs[4] = localVelocity.z / moveSpeed;

        return obs;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Checkpoint"))
        {
            // Removed intermediate checkpoint rewards
            // Logic to update nextTarget would be here
        }
        else if (other.CompareTag("Goal"))
        {
            // CRITICAL REWARD: Reaching the goal is the only reason to exist.
            // Increased significantly to outweigh the accumulated time penalties.
            AddReward(2000.0f); 
            
            // Trigger genetic evolution if enabled
            if (useEvolutionBrain && EvolutionManager.Instance != null)
            {
                EvolutionManager.Instance.NextGeneration();
            }
            else
            {
                // Falling back to standard ML-Agents reset
                ResetAllAgents();
            }
        }
    }

    private void ResetAllAgents()
    {
        // Find all agents in the scene and end their episodes to force a full reset
        GeneralistAgent[] allAgents = Object.FindObjectsByType<GeneralistAgent>(FindObjectsSortMode.None);
        foreach (var agent in allAgents)
        {
            agent.EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Removed penalties for hitting walls/obstacles as requested.
        // The bot will learn to avoid them because they stop progress (and thus stop rewards).
    }

    private bool hasFallen = false;

    private void FixedUpdate()
    {
        // Massive penalty for falling, then respawn
        if (transform.position.y < -5f)
        {
            AddReward(-1.0f); // Penalty for falling
            Respawn();
        }
    }
}
