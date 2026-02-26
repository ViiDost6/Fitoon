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

    private BotRunner controller;
    private Vector3 diff;
    private Vector3 lastDiff;
    private float backwards = 0f;

    public override void Initialize()
    {
        controller = GetComponent<BotRunner>();
        
        GameObject goalObject = GameObject.FindWithTag("Goal");
        if (goalObject != null)
        {
            target = goalObject.transform;
        }
        else
        {
            Debug.LogWarning("RunnerAgent: No object with tag 'Goal' found in the scene.");
        }
    }

    public override void OnEpisodeBegin()
    {
        if (GetComponent<GeneralistAgent>() != null) return;

        controller.enabled = true;
        if (target != null)
        {
            target.GetComponent<Collider>().enabled = true;
            lastDiff = (target.transform.localPosition - transform.localPosition) / 20;
        }
        backwards = 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        diff = (target.transform.localPosition - transform.localPosition) / 20;
        sensor.AddObservation(diff);

        if (diff.magnitude < lastDiff.magnitude) AddReward(0.5f);
        if (diff.magnitude > lastDiff.magnitude)
        {
            AddReward(-0.5f);
            backwards += 0.1f;
        }
        lastDiff = diff;

        //AddReward(-0.001f); //Encourage the player to move faster
    }

    public override void OnActionReceived(ActionBuffers actions)
    {

        float moveV = ScaleAction(actions.ContinuousActions[0], 0.0f, 1.0f);
        float moveH = ScaleAction(actions.ContinuousActions[1], -1.0f, 1.0f);
        controller.SetMovement(moveV, moveH);

        if (moveV == 0 && moveH == 0)
        {
            AddReward(-0.1f); // Penalize for staying still
        }

        //if (GetCumulativeReward() < -1000 || backwards > 20f)
        //{
        //    EndEpisode();
        //}
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;

        continuousActions[0] = Input.GetAxisRaw("Vertical");
        continuousActions[1] = Input.GetAxisRaw("Horizontal");
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (GetComponent<GeneralistAgent>() != null) return;

        if (collider.gameObject.CompareTag("Goal"))
        {
            AddReward(500f);
            EndEpisode();
        }
        if (collider.gameObject.CompareTag("POI"))
        {
            AddReward(20f);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (training)
        {
            if (collision.gameObject.CompareTag("Wall"))
            {
                AddReward(-0.1f); // Apply a small penalty continuously
            }
            if (collision.gameObject.CompareTag("Obstacle"))
            {
                AddReward(-0.05f); // Apply a small penalty continuously
            }
            if (collision.gameObject.CompareTag("Player"))
            {
                AddReward(-0.001f); // Apply a small penalty continuously
            }
        }
    }
}
