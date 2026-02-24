using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class GeneralistAgent : Agent
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 180f;

    [Header("Curriculum Settings")]
    [Range(0, 1)]
    [SerializeField] private float difficultyLevel = 0f; // 0.0=Easy, 0.5=Medium, 1.0=Hard

    private Rigidbody rb;
    private Transform nextTarget;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    public float Difficulty { get => difficultyLevel; set => difficultyLevel = Mathf.Clamp01(value); }

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        spawnPosition = transform.localPosition;
        spawnRotation = transform.localRotation;
    }

    public override void OnEpisodeBegin()
    {
        transform.localPosition = spawnPosition;
        transform.localRotation = spawnRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        // Reset rewards for the new episode
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
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveAction = actions.ContinuousActions[0]; // Forward
        float turnAction = actions.ContinuousActions[1]; // Rotation

        // Apply Movement
        rb.MovePosition(transform.position + transform.forward * moveAction * moveSpeed * Time.fixedDeltaTime);
        transform.Rotate(Vector3.up, turnAction * turnSpeed * Time.fixedDeltaTime);

        // Progress-based reward system
        if (nextTarget != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, nextTarget.position);
            // Small incremental reward for moving closer (Reward Shaping)
            AddReward(0.001f * (1f - (distanceToTarget / 50f))); 
        }

        // Time penalty to encourage efficiency
        AddReward(-0.0005f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Vertical");
        continuousActions[1] = Input.GetAxis("Horizontal");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Checkpoint"))
        {
            AddReward(0.5f);
            // Logic to update nextTarget would be here
        }
        else if (other.CompareTag("Goal"))
        {
            AddReward(2.0f);
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-0.1f);
            // Optional: EndEpisode() on hard collision
        }
    }
}
