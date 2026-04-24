using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Managing.Statistic;
using FishNet.Component.Transforming;
using FishNet.Object.Synchronizing;
using TMPro;

/// <summary>
/// This class is used to control the player character. It handles movement and rotation based on face tracking data. It's also used for setting the player's position and medal gain.
/// </summary>
public class PlayerController : BaseRunner
{
	[SerializeField] new Camera camera;
    FaceTrackingToMovement faceTracking;

	public override void OnStartClient()
    {
		Debug.Log("Player Starting");
		BaseAwake();
		faceTracking = GetComponent<FaceTrackingToMovement>();

		if (Owner.IsLocalClient)
		{
			Camera.main.GetComponent<CameraFollowPlayer>().target = transform;
			SaveData.ReadFromJson();
					Debug.Log("[CHARLOAD] Client" + SaveData.player.playerCharacterData.hairColor + " " + SaveData.player.playerCharacterData.skinColor + " " + SaveData.player.playerCharacterData.topColor + " " + SaveData.player.playerCharacterData.bottomColor);

			SetCharacter(SaveData.player.playerCharacterData, SaveData.player.username);
		}
		else
		{
			faceTracking.enabled = false;
			GetComponent<PlayerController>().enabled = false;
		}
	}

	private void FixedUpdate()
	{
#if !UNITY_EDITOR
		if (faceTracking == null || !faceTracking.detectado || !canMove || !IsOwner)
		{
			return;
		}
		Debug.Log("Player Moving");
		rigidBody.linearVelocity = new Vector3(0, rigidBody.linearVelocity.y, 0);
		// Un tercio más lento = 2/3 de la velocidad original
		float playerSpeed = baseSpeed * faceTracking.speed * Mathf.Max(0.1f, speedMultiplier) * (2f / 3f);
		// Asegurar mínimo de 1 m/s
		playerSpeed = Mathf.Max(1f, playerSpeed);
		rigidBody.linearVelocity += playerSpeed * transform.forward;
		rigidBody.rotation = Quaternion.Slerp(rigidBody.rotation, faceTracking.faceRotation, rotationSpeed);
#else
		if (!canMove || !IsOwner)
		{
			return;
		}
		rigidBody.linearVelocity = new Vector3(0, rigidBody.linearVelocity.y, 0);
		// Un tercio más lento = 2/3 de la velocidad original
		float playerSpeed = baseSpeed * Mathf.Max(0.1f, speedMultiplier) * (2f / 3f);
		// Asegurar mínimo de 1 m/s
		playerSpeed = Mathf.Max(1f, playerSpeed);
		rigidBody.linearVelocity += playerSpeed * transform.forward + 0.01f * Vector3.right;
#endif
		if (Physics.Raycast(transform.position, Vector3.down, out _, runnerHeight * 0.5f + 1f, whatIsGround))
		{
			rigidBody.linearDamping = groundDrag;
		}
		else
		{
			rigidBody.linearDamping = 0;
		}
		BaseFixedUpdate();
	}

	void Update()
	{
		BaseUpdate();
	}

	public void SetPosition(int pos, int runnerAmount)
	{
		SetPositionServerRpc(pos, runnerAmount);
	}

	[ServerRpc (RequireOwnership = false)]
	void SetPositionServerRpc(int pos, int runnerAmount)
	{
		SetPositionObserversRpc(pos, runnerAmount);
	}

	[ObserversRpc]
	void SetPositionObserversRpc(int pos, int runnerAmount)
	{
		if (!IsOwner)
		{
			return;
		}
		if (runnerAmount >= 3)
		{
			int medals = Mathf.RoundToInt((runnerAmount - pos + 1 - runnerAmount / 2) * Mathf.Lerp(15, 5, runnerAmount / 32f));
			GameObject.Find("PositionText").GetComponent<TextMeshProUGUI>().text = pos + "/" + runnerAmount;
			SaveData.player.medals += medals;
			SaveData.player.normalCoins += 5;
			if (pos == 1)
			{
				SaveData.player.wins++;
			}
		}
		SaveData.player.runnedDistance += (int)faceTracking.GetTotalDistance();
		SaveData.SaveToJson();
		//Debug.Log("Total medals: " + SaveData.player.medals + "\nTotal distance: " + SaveData.player.runnedDistance + "\nMedals: "  + medals + "\nDistance: " + (int)faceTracking.GetTotalDistance());
	}
}