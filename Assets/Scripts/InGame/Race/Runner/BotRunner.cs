using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

public class BotRunner : BaseRunner
{
    float moveV;
    float moveH;

    void FixedUpdate()
    {
        // Only server simulates bot physics to avoid desynchronization across clients
        // Bots are server-authoritative, so clients receive updates via NetworkTransform
        if (!IsServerInitialized || !canMove || rigidBody == null) return;

        float turnAmount = moveH * rotationSpeed * 140f * Time.fixedDeltaTime;
        Quaternion nextRotation = rigidBody.rotation * Quaternion.Euler(0, turnAmount, 0);
        Vector3 limitedEuler = RotationLimited(nextRotation.eulerAngles);
        rigidBody.MoveRotation(Quaternion.Euler(limitedEuler));

        float currentMaxSpeed = baseSpeed * speedMultiplier;

        if (moveV > 0.1f)
        {
            Vector3 moveInput = transform.forward * moveV * currentMaxSpeed;
            rigidBody.linearVelocity = new Vector3(moveInput.x, rigidBody.linearVelocity.y, moveInput.z);
        }
        else
        {
            rigidBody.linearVelocity = new Vector3(0, rigidBody.linearVelocity.y, 0);
        }

        BaseFixedUpdate();

        if (animator != null)
        {
            float horizontalSpeed = new Vector3(rigidBody.linearVelocity.x, 0, rigidBody.linearVelocity.z).magnitude;
            animator.SetBool("isRunning", moveV > 0.1f);
            animator.SetFloat("playerSpeed", horizontalSpeed / 10f);
        }
    }

    private void Update()
    {
        BaseUpdate();

        // Only server updates physics properties to avoid desynchronization
        if (!IsServerInitialized) return;

        RaycastHit hit;
        bool grounded = Physics.Raycast(transform.position, Vector3.down, out hit, 2.5f, whatIsGround);
        if (!rigidBody.isKinematic)
        {
            rigidBody.linearDamping = grounded ? groundDrag : 0.05f;
        }

        float maxAllowed = baseSpeed * speedMultiplier;
        Vector3 flatVel = new Vector3(rigidBody.linearVelocity.x, 0f, rigidBody.linearVelocity.z);
        
        if (flatVel.magnitude > maxAllowed)
        {
            Vector3 limitedVel = flatVel.normalized * maxAllowed;
            rigidBody.linearVelocity = new Vector3(limitedVel.x, rigidBody.linearVelocity.y, limitedVel.z);
        }
    }

    public override void OnStartNetwork()
    {
        BaseAwake();
        if (IsServerInitialized)
        {
            SetCharacter(CharacterLoader.CreateRandomCharacterData(), "");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Bots are server-authoritative: clients receive updates via NetworkTransform only
        // Set rigidbody to kinematic to prevent double simulation
        if (rigidBody != null && !IsServerInitialized)
        {
            rigidBody.isKinematic = true;
            rigidBody.useGravity = false;
            rigidBody.interpolation = RigidbodyInterpolation.None;
        }
    }

    /// <summary>
    /// Override animation authority: bots are controlled by the server, not the owner
    /// </summary>
    protected override bool HasAnimationAuthority()
    {
        return IsServerInitialized;
    }

    public void SetMovement(float moveV, float moveH)
    {
        if (canMove)
        {
            this.moveV = moveV;
            this.moveH = moveH;
        }
    }

    private Vector3 RotationLimited(Vector3 rotation)
    {
        if (rotation.y > 90 && rotation.y < 270)
        {
            if (rotation.y < 180) rotation.y = 90;
            else rotation.y = 270;
        }
        return rotation;
    }
}