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
	TextMeshPro nameTag;
	Coroutine animatorCoroutine;
    protected Collider runnerCollider;

	public readonly SyncVar<int> id = new SyncVar<int>(-1);

	protected Rigidbody rigidBody;
	protected Animator animator;

	protected float speedMultiplier = 1;
	public bool canMove = true;

	[Header("Bot Character Models")]
	[SerializeField] protected List<GameObject> botPrefabList;

	public readonly SyncVar<int> syncedBotPrefabIndex = new SyncVar<int>(-1);

	protected virtual void Awake()
	{
		rigidBody = GetComponent<Rigidbody>();
        runnerCollider = GetComponent<Collider>();
		if (rigidBody != null) rigidBody.detectCollisions = true;

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
		if (botPrefabList == null || botPrefabList.Count == 0) return;

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
		Character character = CharacterLoader.GetCharacter(characterData);

		if (character.prefab == null)
		{
			return;
		}

		if (characterObject != null)
			Destroy(characterObject);

		characterObject = Instantiate(character.prefab, transform.position - Vector3.up, Quaternion.identity, transform);

		characterObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

		animator = characterObject.GetComponentInChildren<Animator>();
		if (animator != null) animator.applyRootMotion = false;

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
			
			yield return new WaitForSeconds(0.1f);
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

	private void Start()
	{
		if ((RaceManager.isTraining || Application.isEditor) && !IsServerInitialized)
		{
			BaseAwake();
			PickRandomBotCharacter();
			canMove = true;
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

		if (!rigidBody.isKinematic) rigidBody.linearVelocity = Vector3.zero;
		rigidBody.isKinematic = true;
		rigidBody.detectCollisions = false;
		GetComponent<Collider>().enabled = false;

		var tm = FindFirstObjectByType<GameManager>();
		if (tm != null && !RaceManager.isTraining) tm.GoalReached(id.Value);
	}

	[ServerRpc]
	void FreezeServerRpc()
	{
		ObserversFreeze();
	}

	[ObserversRpc]
	private void ObserversFreeze()
	{
		LocalFreeze();
	}

	public void Freeze()
	{
		if (RaceManager.isTraining || Application.isEditor)
		{
			LocalFreeze();
		}
		else if (IsServerInitialized)
		{
			ObserversFreeze();
		}
		else
		{
			FreezeServerRpc();
		}
	}

	public void LocalFreeze()
	{
		if (!IsOwner && !RaceManager.isTraining && !IsServerInitialized && !Application.isEditor)
			return;
		canMove = false;
		if (rigidBody != null && !rigidBody.isKinematic) rigidBody.linearVelocity = Vector3.zero;
	}

	[ObserversRpc(ExcludeServer = false, ExcludeOwner = false)]
	private void ObserversUnFreeze()
	{
		LocalUnFreeze();
	}

	public void UnFreeze()
	{
		if (RaceManager.isTraining || Application.isEditor)
		{
			LocalUnFreeze();
		}
		else if (IsServerInitialized)
		{
			ObserversUnFreeze();
		}
	}

	public void LocalUnFreeze()
	{
		if (!IsOwner && !RaceManager.isTraining && !IsServerInitialized && !Application.isEditor)
			return;
			
		// Debug.Log("Local Unfreezing: " + gameObject.name);
		canMove = true;
		
		if (rigidBody != null)
		{
			// Restore physics and collisions
			rigidBody.detectCollisions = true;
			rigidBody.isKinematic = false;
		}
		
		if (runnerCollider != null) runnerCollider.enabled = true;
	}

	public void SetId(int i)
	{
		id.Value = i;
		if (IsServerInitialized) id.DirtyAll();
	}

	public int GetId()
	{
		return id.Value;
	}
}