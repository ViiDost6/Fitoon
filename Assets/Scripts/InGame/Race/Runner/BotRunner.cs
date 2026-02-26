// BotRunner.cs
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

/// <summary>
/// This class is used to control the bots. It handles movement and rotation based on ML-Agents training.
/// </summary>
public class BotRunner : BaseRunner
{
    float moveV;
    float moveH;

	private void Start()
	{
		if (RaceManager.isTraining && !IsServerInitialized)
		{
			BaseAwake();
			PickRandomBotCharacter();
			canMove = true;
		}
	}

	protected override bool HasAnimationAuthority()
	{
		return IsServerInitialized || RaceManager.isTraining;
	}

	void FixedUpdate()
    {
		if (!canMove || (!IsServerInitialized && !RaceManager.isTraining) || rigidBody == null)
		{
			return;
		}

		HandleLocomotion();
		HandleEnvironment();

		BaseFixedUpdate();
	}

    private void HandleLocomotion()
    {
        // 1. Rotation (Lowered to 120 deg/sec with tiny deadzone to prevent zig-zag)
        if (Mathf.Abs(moveH) > 0.05f) 
        {
            float step = moveH * 120f * Time.fixedDeltaTime; 
            transform.Rotate(Vector3.up, step);

            // ROTATION LOCK: Constraint to 90 degrees left/right from spawn
            var agent = GetComponent<GeneralistAgent>();
            if (agent != null && agent.spawnPositionCaptured)
            {
                float angle = Vector3.SignedAngle(agent.spawnRotation * Vector3.forward, transform.forward, Vector3.up);
                if (Mathf.Abs(angle) > 90f)
                {
                    float clampedAngle = Mathf.Sign(angle) * 90f;
                    transform.rotation = Quaternion.Euler(0, agent.spawnRotation.eulerAngles.y + clampedAngle, 0);
                    rigidBody.angularVelocity = Vector3.zero;
                }
            }
        }

        // 2. Direct velocity control (Matching PlayerController style)
        Vector3 moveInput = transform.forward * baseSpeed * Mathf.Max(0.05f, speedMultiplier) * moveV;

        if (!rigidBody.isKinematic)
        {
            Vector3 vel = rigidBody.linearVelocity;
            rigidBody.linearVelocity = new Vector3(moveInput.x, vel.y, moveInput.z);
        }
    }

    private void HandleEnvironment()
    {
        // Ground checking and Y-snapping (Copied precisely from PlayerController)
        float rayLength = runnerHeight * 0.5f + 0.4f; 
        
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, rayLength, whatIsGround))
        {
            if (rigidBody.linearDamping != groundDrag) rigidBody.linearDamping = groundDrag;

            // CORRECCIÓN ANTI-JITTER:
            float targetY = hit.point.y + (runnerHeight * 0.5f) + 0.01f;
            if (Mathf.Abs(transform.position.y - targetY) > 0.005f) 
            {
                float smoothY = Mathf.MoveTowards(transform.position.y, targetY, Time.fixedDeltaTime * 5f);
                transform.position = new Vector3(transform.position.x, smoothY, transform.position.z);
            }
        }
        else
        {
            if (rigidBody.linearDamping != 0) rigidBody.linearDamping = 0;
        }
    }

    // Update is only for visuals, we can keep it for server/non-training but skip logic
    private void Update()
    {
        BaseUpdate();
    }

	public override void OnStartNetwork()
	{
		BaseAwake();
		if (IsServerInitialized)
		{
			PickRandomBotCharacter();
		}
		else
		{
			if (!RaceManager.isTraining)
			{
				GetComponent<RunnerAgent>().enabled = false;
				GetComponent<DecisionRequester>().enabled = false;
				GetComponent<BehaviorParameters>().enabled = false;
			}
		}
	}

	public void SetMovement(float moveV, float moveH)
	{
		if (canMove)
		{
			this.moveV = moveV;
			this.moveH = moveH;
		}
	}

}