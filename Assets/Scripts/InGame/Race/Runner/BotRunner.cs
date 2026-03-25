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

    void FixedUpdate()
    {
        if (!canMove) return;

        // 1. ROTACIÓN LIMITADA (Igual que el jugador humano)
        // Calculamos el giro incremental basado en moveH
        float turnAmount = moveH * rotationSpeed * 120f * Time.fixedDeltaTime;
        Quaternion nextRotation = rigidBody.rotation * Quaternion.Euler(0, turnAmount, 0);

        // Limitamos la rotación para que no puedan girar más de 90 grados a cada lado (N/E/O)
        Vector3 limitedEuler = RotationLimited(nextRotation.eulerAngles);
        rigidBody.MoveRotation(Quaternion.Euler(limitedEuler));

        // 2. MOVIMIENTO FÍSICO RESPONSIVO
        if (moveV > 0)
        {
            // Usamos VelocityChange para una respuesta instantánea y fluida
            Vector3 targetVelocity = transform.forward * moveV * baseSpeed * Mathf.Max(0.1f, speedMultiplier);
            Vector3 velocityChange = targetVelocity - new Vector3(rigidBody.linearVelocity.x, 0, rigidBody.linearVelocity.z);
            rigidBody.AddForce(velocityChange, ForceMode.VelocityChange);
        }
        else
        {
            // Si no hay input vertical, frenamos el movimiento lateral/forward gradualmente para evitar jitter
            Vector3 currentVel = new Vector3(rigidBody.linearVelocity.x, 0, rigidBody.linearVelocity.z);
            rigidBody.AddForce(-currentVel * 0.1f, ForceMode.VelocityChange);
        }

        BaseFixedUpdate();

        // 3. ANIMACIONES
        if (animator != null)
        {
            float horizontalSpeed = new Vector3(rigidBody.linearVelocity.x, 0, rigidBody.linearVelocity.z).magnitude;
            animator.SetBool("isRunning", moveV > 0.1f);
            animator.SetFloat("playerSpeed", horizontalSpeed / 10f);

            bool grounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 1.5f, whatIsGround);
            animator.SetBool("isFalling", !grounded);
        }

        // 4. LÓGICA DE VELOCIDAD RELATIVA AL JUGADOR (Solo fuera de entrenamiento)
        if (!RaceManager.isTraining)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                Rigidbody playerRb = player.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    float playerSpeed = playerRb.linearVelocity.magnitude;
                    float targetSpeed = playerSpeed + UnityEngine.Random.Range(-0.3f, 0.3f);
                    
                    if (rigidBody.linearVelocity.magnitude > 0.1f)
                    {
                        rigidBody.linearVelocity = rigidBody.linearVelocity.normalized * targetSpeed;
                    }
                }
            }
        }
    }

	private void Update()
	{
		BaseUpdate();

		RaycastHit hit;
		bool grounded = Physics.Raycast(transform.position, Vector3.down, out hit, 2 * 0.5f + 3f, whatIsGround);

		// Limit velocity to avoid speed hacks/bugs
		Vector3 flatVel = new Vector3(rigidBody.linearVelocity.x, 0f, rigidBody.linearVelocity.z);
		if (flatVel.magnitude > baseSpeed * 2f)
		{
			Vector3 limitedVel = flatVel.normalized * baseSpeed * 2f;
			rigidBody.linearVelocity = new Vector3(limitedVel.x, rigidBody.linearVelocity.y, limitedVel.z);
		}

		// Drag Control
		if (grounded)
		{
			rigidBody.linearDamping = groundDrag;
		}
		else
		{
			rigidBody.linearDamping = 0;
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

	/// <summary>
	/// Set the movement of the bot. Receives values from ML-Agents or Human Input.
	/// </summary>
	public void SetMovement(float moveV, float moveH)
	{
		if (canMove)
		{
			this.moveV = moveV;
			this.moveH = moveH;
		}
	}

    /// <summary>
    /// Clamps the Y rotation to ensure bots cannot look backwards (90 degrees left/right limit).
    /// </summary>
    private Vector3 RotationLimited(Vector3 rotation)
    {
        if (rotation.y > 90 && rotation.y < 270)
        {
            if (rotation.y < 180)
                rotation.y = 90;
            else
                rotation.y = 270;
        }
        return rotation;
    }
}