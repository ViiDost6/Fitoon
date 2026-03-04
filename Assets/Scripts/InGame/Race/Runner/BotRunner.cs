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

	protected override void Awake()
	{
		base.Awake(); // Llamamos al base para asignar el Rigidbody
		
		// Inicialización forzada para Editor/Heurística
		if (Application.isEditor || RaceManager.isTraining)
		{
			BaseAwake();
			PickRandomBotCharacter();
			canMove = true;
			if (rigidBody != null) 
			{
				rigidBody.isKinematic = false;
				rigidBody.useGravity = true;
				rigidBody.interpolation = RigidbodyInterpolation.Interpolate;
				rigidBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
			}
		}
	}

	private void Start() 
	{
		// Los bots necesitan canMove activo fuera de red para la heurística
		if (Application.isEditor) canMove = true;
	}

	protected override bool HasAnimationAuthority()
	{
		return IsServerInitialized || RaceManager.isTraining;
	}

	void FixedUpdate()
    {
		// Allow movement in Editor for manual testing and heuristic debugging
		bool isEditor = Application.isEditor;
		if (!canMove || (!IsServerInitialized && !RaceManager.isTraining && !isEditor) || rigidBody == null)
		{
			return;
		}

		HandleLocomotion();
		HandleEnvironment();

		BaseFixedUpdate();
	}

    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded = false;

    private void HandleLocomotion()
    {
        // 1. Rotation (Continuous and fluid like the player)
        if (Mathf.Abs(moveH) > 0.05f) 
        {
            // Clear angular velocity to prevent physics fighting with our manual rotation
            rigidBody.angularVelocity = Vector3.zero;

            float rotStep = moveH * rotationSpeed * 140f * Time.fixedDeltaTime; 
            transform.Rotate(Vector3.up, rotStep);

            // ROTATION LOCK (Preserving slope alignment)
            var runAgent = GetComponent<RunnerAgent>();
            if (runAgent != null && runAgent.spawnPositionCaptured)
            {
                Vector3 spawnFwd = runAgent.spawnRotation * Vector3.forward;
                float angle = Vector3.SignedAngle(spawnFwd, transform.forward, Vector3.up);
                if (Mathf.Abs(angle) > 95f) 
                {
                    float clampedY = runAgent.spawnRotation.eulerAngles.y + (Mathf.Sign(angle) * 95f);
                    // Keep current X and Z (slope tilt) but lock Y
                    transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, clampedY, transform.rotation.eulerAngles.z);
                }
            }
        }

        // 2. Movement (SURFACE PROJECTED VELOCITY)
        float effectiveSpeed = baseSpeed > 0 ? baseSpeed : 10f; 
        Vector3 moveDirection = transform.forward * moveV;
        
        // If we are on a curve/slope, project our direction onto the surface plane
        if (isGrounded)
        {
            moveDirection = Vector3.ProjectOnPlane(moveDirection, groundNormal).normalized * moveV;
        }

        Vector3 targetVelocity = moveDirection * effectiveSpeed * Mathf.Max(0.05f, speedMultiplier);

        if (!rigidBody.isKinematic)
        {
            Vector3 currentVel = rigidBody.linearVelocity;
            
            if (moveV > 0.05f || Mathf.Abs(moveH) > 0.05f)
            {
                // ForceMode.VelocityChange is fast and avoids build-up of vertical errors on curves
                Vector3 velocityChange = (targetVelocity - currentVel);
                
                // On flat ground, we don't want to mess much with existing falling velocity
                if (groundNormal.y > 0.95f)
                {
                    velocityChange.y = 0; 
                }

                rigidBody.AddForce(velocityChange, ForceMode.VelocityChange);
            }
            else
            {
                // Slow down if no input
                rigidBody.linearVelocity = new Vector3(currentVel.x * 0.9f, currentVel.y, currentVel.z * 0.9f);
            }
        }
    }

    private void HandleEnvironment()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * runnerHeight;
        float adjustedRayLength = runnerHeight + 0.6f; 
        
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, adjustedRayLength, whatIsGround))
        {
            isGrounded = true;
            groundNormal = hit.normal;
            
            if (rigidBody.linearDamping != groundDrag) rigidBody.linearDamping = groundDrag;

            // ANTI-JITTER: Only on mostly flat surfaces. On cylinders, let physics handle it.
            if (groundNormal.y > 0.95f)
            {
                float targetY = hit.point.y + (runnerHeight * 0.5f) + 0.05f;
                if (Mathf.Abs(transform.position.y - targetY) > 0.005f) 
                {
                    float smoothY = Mathf.MoveTowards(transform.position.y, targetY, Time.fixedDeltaTime * 10f);
                    transform.position = new Vector3(transform.position.x, smoothY, transform.position.z);
                    Physics.SyncTransforms(); 
                }
            }
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
            if (rigidBody.linearDamping != 0) rigidBody.linearDamping = 0;
        }
    }

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
			if (!RaceManager.isTraining && !Application.isEditor)
			{
				var rAgent = GetComponent<RunnerAgent>();
				if (rAgent != null) rAgent.enabled = false;
				
				var dReq = GetComponent<DecisionRequester>();
				if (dReq != null) dReq.enabled = false;
				
				var bParam = GetComponent<BehaviorParameters>();
				if (bParam != null) bParam.enabled = false;
			}
		}
	}

	public void SetMovement(float moveV, float moveH)
	{
		if (canMove)
		{
			this.moveV = moveV;
			this.moveH = moveH;
			if (rigidBody != null && rigidBody.IsSleeping()) rigidBody.WakeUp();
		}
	}
}
