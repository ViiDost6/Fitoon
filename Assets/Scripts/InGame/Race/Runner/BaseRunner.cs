// BaseRunner.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using TMPro;
using FishNet.Object.Synchronizing;
using System;
using System.Linq;

/// <summary>
/// Base class for bots and players. It handles setting the appearance of the character and its animation.
/// </summary>
public class BaseRunner : NetworkBehaviour
{
	protected GameObject characterObject;
	[SerializeField] protected float baseSpeed;
	[SerializeField] protected float rotationSpeed = 1;
	[SerializeField] protected float groundDrag = 5f;
	[SerializeField] protected GameObject trailBoost;
	[SerializeField] protected LayerMask whatIsGround;
	[SerializeField] protected float runnerHeight = 2;
	[SerializeField] TextMeshPro nameTag;
	Coroutine animatorCoroutine;

	public readonly SyncVar<int> id = new SyncVar<int>(-1);

	protected Rigidbody rigidBody;
	protected Animator animator;

	protected float speedMultiplier = 1;
	public bool canMove = true;

	[Header("Bot Character Models")]
	[SerializeField] protected List<GameObject> botPrefabList;

	public readonly SyncVar<int> syncedBotPrefabIndex = new SyncVar<int>(-1);

	private void Awake()
	{
		rigidBody = GetComponent<Rigidbody>();
		rigidBody.detectCollisions = true;

		syncedBotPrefabIndex.OnChange += OnBotPrefabChanged;
	}

	protected void BaseAwake()
	{
		animatorCoroutine = StartCoroutine(UpdateAnimatorAndBoostTrail());
	}

	private void OnDestroy()
	{
		if (animatorCoroutine != null)
		{
			StopCoroutine(animatorCoroutine);
		}
		
		syncedBotPrefabIndex.OnChange -= OnBotPrefabChanged;
	}

	protected void PickRandomBotCharacter()
	{
		if (!IsServerInitialized && !RaceManager.isTraining) return;

		if (botPrefabList == null || botPrefabList.Count == 0)
		{
			Debug.LogError("Bot Prefab List is empty! Please assign prefabs in the Inspector.");
			return;
		}

		int newIndex = UnityEngine.Random.Range(0, botPrefabList.Count);
		if (RaceManager.isTraining && !IsServerInitialized)
		{
			LocalOnBotPrefabChanged(newIndex);
		}
		else
		{
			syncedBotPrefabIndex.Value = newIndex;
		}
	}

	void LocalOnBotPrefabChanged(int newIndex)
	{
		if (newIndex < 0 || botPrefabList == null || newIndex >= botPrefabList.Count) return;

		if (characterObject != null) Destroy(characterObject);

		GameObject selectedPrefab = botPrefabList[newIndex];
		characterObject = Instantiate(selectedPrefab, transform.position - Vector3.up, Quaternion.identity, transform);
		characterObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

		animator = characterObject.GetComponentInChildren<Animator>();
		
		LocalSetNameTag("Bot"); 
	}

	void OnBotPrefabChanged(int oldIndex, int newIndex, bool asServer)
	{
		LocalOnBotPrefabChanged(newIndex);
	}

	[ServerRpc]
	protected void SetCharacter(CharacterData characterData, string playerName)
	{
		Debug.Log("[CHARLOAD] Server" + characterData.hairColor + " " + characterData.skinColor + " " + characterData.topColor + " " + characterData.bottomColor);

		LoadCharacter(characterData);
		SetNameTag(playerName);
	}

	void LocalSetNameTag(string name)
	{
		if (nameTag != null) nameTag.text = name;
	}

	[ObserversRpc]
	void SetNameTag(string name)
	{
		LocalSetNameTag(name);
	}

	[ObserversRpc]
	void LoadCharacter(CharacterData characterData)
	{
		Debug.Log("Loading: " + characterData.characterName);
		Debug.Log("[CHARLOAD] Observers" + characterData.hairColor + " " + characterData.skinColor + " " + characterData.topColor + " " + characterData.bottomColor);
		Character character = CharacterLoader.GetCharacter(characterData);

		if (character.prefab == null)
		{
			Debug.LogError("Character Data is Null");
			return;
		}

		if (characterObject != null)
			Destroy(characterObject);

		characterObject = Instantiate(character.prefab, transform.position - Vector3.up, Quaternion.identity, transform);

		characterObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

		characterObject.GetComponent<CharacterPrefabColorChanger>().ChangeColors(character.hairColor, character.skinColor, character.topColor, character.bottomColor);
		characterObject.GetComponent<CharacterPrefabColorChanger>().ChangeShoe(ShoeLoader.GetMesh(character.shoes.itemID), ShoeLoader.getMaterials(character.shoes.materials));
	}

	List<GameObject> GetAllChildrenRecursive(GameObject gameObject)
	{
		List<GameObject> children = new List<GameObject>();
		for(int i = 0; i < gameObject.transform.childCount; i++)
		{
			children.Add(gameObject.transform.GetChild(i).gameObject);
			children.Concat(GetAllChildrenRecursive(gameObject.transform.GetChild(i).gameObject));
		}
		Debug.Log(children);
		return children;
	}

	protected void BaseFixedUpdate()
	{
		if (speedMultiplier > 1f)
		{
			speedMultiplier -= 0.01f;
			speedMultiplier = Mathf.Clamp(speedMultiplier, 1f, 10f);
		}
	}

	[ServerRpc]
	void TrailBoostServerRpc(bool on)
	{
		TrailBoostRpc(on);
	}

	void LocalTrailBoost(bool on)
	{
		if (trailBoost == null) return;
		trailBoost.GetComponent<TrailRenderer>().emitting = on;
		if (on)
		{
			trailBoost.GetComponentInChildren<ParticleSystem>().Play();
		}
		else
		{
			trailBoost.GetComponentInChildren<ParticleSystem>().Stop();
		}
	}

	[ObserversRpc]
	void TrailBoostRpc(bool on)
	{
		LocalTrailBoost(on);
	}

	protected void BaseUpdate()
	{
		if (animator == null)
		{
			animator = GetComponentInChildren<Animator>();
		}
	}

	protected virtual bool HasAnimationAuthority()
	{
		return IsOwner; 
	}

	IEnumerator UpdateAnimatorAndBoostTrail()
	{
		while (true)
		{
			if (HasAnimationAuthority())
			{
				bool isRunning = rigidBody.linearVelocity.magnitude > 0.3f;
				bool isFalling = !Physics.Raycast(transform.position, Vector3.down, out _, runnerHeight * 0.5f + 1f, whatIsGround);
				float speed = new Vector3(rigidBody.linearVelocity.x, 0, rigidBody.linearVelocity.z).magnitude / 10;
				bool isBoosting = speedMultiplier > 1f;

				if (IsServerInitialized)
				{
					SetAnimatorParametersObserversRpc(isRunning, isFalling, speed);
					TrailBoostRpc(isBoosting);
				}
				else if (RaceManager.isTraining)
				{
					LocalSetAnimatorParameters(isRunning, isFalling, speed);
					LocalTrailBoost(isBoosting);
				}
				else
				{
					SetAnimatorParametersServerRpc(isRunning, isFalling, speed);
					TrailBoostServerRpc(isBoosting);
				}
			}
			
			yield return new WaitForSeconds(0.5f);
		}
	}

	[ServerRpc]
	void SetAnimatorParametersServerRpc(bool running, bool falling, float speed)
	{
		SetAnimatorParametersObserversRpc(running, falling, speed);
	}

	void LocalSetAnimatorParameters(bool running, bool falling, float speed)
	{
		if (animator == null)
		{
			animator = GetComponentInChildren<Animator>();
		}
		if (animator == null)
		{
			return;
		}
		animator.SetBool("isRunning", running);
		animator.SetBool("isFalling", falling);
		animator.SetFloat("playerSpeed", speed);
	}

	[ObserversRpc]
	void SetAnimatorParametersObserversRpc(bool running, bool falling, float speed)
	{
		LocalSetAnimatorParameters(running, falling, speed);
	}

	private void OnTriggerEnter(Collider other)
	{
		if(other.tag is "Goal")
		{
			GoalReached();
		}
		if(other.GetComponent<SpeedBoost>() != null)
		{
			speedMultiplier = other.GetComponent<SpeedBoost>().speedBoost;
			other.GetComponent<SpeedBoost>().FadeAndRespawn();
		}
	}

	void GoalReached()
	{
		if(!IsOwner && !RaceManager.isTraining)
			return;

		if (RaceManager.isTraining && !IsServerInitialized)
		{
			LocalFreeze();
		}
		else
		{
			FreezeServerRpc();
		}

		rigidBody.detectCollisions = false;
		rigidBody.isKinematic = true;
		rigidBody.linearVelocity = Vector3.zero;
		GetComponent<Collider>().enabled = false;

		var tm = FindFirstObjectByType<GameManager>();
		if (tm != null) tm.GoalReached(id.Value);
	}

	[ServerRpc]
	void FreezeServerRpc()
	{
		Freeze();
	}

	[ObserversRpc]
	public void Freeze()
	{
		LocalFreeze();
	}

	public void LocalFreeze()
	{
		if (!IsOwner && !RaceManager.isTraining)
			return;
		canMove = false;
		rigidBody.linearVelocity = Vector3.zero;
	}

	[ObserversRpc(ExcludeServer = false, ExcludeOwner = false)]
	public void UnFreeze()
	{
		if (!IsOwner)
			return;
		Debug.Log("I'm Unfreezing");
		canMove = true;
	}

	public void SetId(int i)
	{
		id.Value = i;
		id.DirtyAll();
	}

	public int GetId()
	{
		return id.Value;
	}
}