using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class MoveToTargetPlay : Agent
{
    [SerializeField] private float movementSpeed = 7f;
    [SerializeField] private float rotationSpeed = 1f;
    public Transform target;
    public Vector3 spawnpoint;
    [SerializeField] private List<GameObject> checkpointsPassedList;

    private BotRunner controller;
    private Vector3 diff;

    public override void Initialize()
    {
        controller = GetComponent<BotRunner>();
    }

    public override void OnEpisodeBegin()
    {
        checkpointsPassedList.Clear();

        transform.localRotation = Quaternion.identity;
        if (spawnpoint != null) transform.localPosition = new Vector3(spawnpoint.x, spawnpoint.y + 1, spawnpoint.z);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        diff = (target.transform.localPosition - transform.localPosition)/20;
        sensor.AddObservation(diff);

        AddReward(-0.001f); //Encourage the player to move faster
    }

    public override void OnActionReceived(ActionBuffers actions)
    {

        //float moveV = Mathf.Abs(actions.ContinuousActions[0]);
        float moveV = ScaleAction(actions.ContinuousActions[0], 0.0f, 1.0f);
        float moveH = ScaleAction(actions.ContinuousActions[1], -1.0f, 1.0f);

        controller.SetMovement(moveV, moveH);

        //Penalize for staying still
        if (moveV <= 0)
        {
            AddReward(-10f);
        }
    }

    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;

        continuousActions[0] = Input.GetAxisRaw("Vertical");
        continuousActions[1] = Input.GetAxisRaw("Horizontal");
    }
    

    private void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.name == target.name)
        {
            AddReward(1000f);
            Destroy(gameObject);
        }
        if (collider.gameObject.CompareTag("Checkpoint"))
        {
            int counter = 0;

            foreach (GameObject go in checkpointsPassedList)
            {
                if (go == collider.gameObject)
                {
                    AddReward(-100f); //Penalize for reaching a repeated CheckPoint
                    //Debug.Log("REPEATED CP");
                }
                else
                {
                    counter++;
                }
            }

            if(counter == checkpointsPassedList.Count)
            {
                checkpointsPassedList.Add(collider.gameObject);
                AddReward(100f); //Reward for reaching a CheckPoint
                //Debug.Log("NEW CP");
            }
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-10f); //Penalization for colliding with a wall
        }
    }

}