using FishNet;
using FishNet.Component.Spawning;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// This class handles all data and events related to the race.
/// </summary>
public class GameManager : NetworkBehaviour
{
	int readyPlayers;
	public List<Runner> runnerData;

	[SerializeField] public TextMeshProUGUI cadenceText;
	[SerializeField] public TextMeshProUGUI velocityText;
	[SerializeField] public TextMeshProUGUI positionText;
	[SerializeField] public TextMeshProUGUI countdownText;
	[SerializeField] Transform goal;
	[SerializeField] Transform[] spawnPoints;
    [SerializeField] NetworkObject playerPrefab;
    [SerializeField] NetworkObject botPrefab;
    [SerializeField] public Countdown countdown;
    [SerializeField] EscenarioItem scene;

    public static bool addBots = false;	//Set to true to add bots to the race
    
    List<BaseRunner> runners = new List<BaseRunner>();

    int positionIndex = 0;
    bool raceFinished = false;

	public override void OnStartServer()
    {
		SceneManager.OnLoadEnd += OnSceneLoaded;
		StartCoroutine(WaitForPlayers());
	}

	private void OnSceneLoaded(SceneLoadEndEventArgs args)
	{
		readyPlayers++;
	}

	public override void OnStartClient()
	{
		SceneManager.OnLoadEnd += OnSceneLoaded;
		countdown.StartCountdown();
	}

	/// <summary>
	/// Freezes all player movement.
	/// </summary>
	void FreezeAllRunners()
	{
		for (int i = 0; i < runners.Count; i++)
		{
			runners[i].Freeze();
			runners[i].canMove = false;
		}
	}

	/// <summary>
	/// Unfreezes all player movement.
	/// </summary>
	void UnfreezeAllRunners()
    {
        for(int i = 0; i < runners.Count; i++)
        {
			Debug.Log("Unfreezing runner");
			runners[i].UnFreeze();
			runners[i].canMove = true;
		}
    }

	/// <summary>
	/// This method instantiates the runners and spawns them over the network.
	/// </summary>
	void SpawnRunners()
    {
		for (int i = 0; i < runnerData.Count; i++)
		{
			runnerData[i].goalReached = false;
			NetworkObject runnerObject = Instantiate(runnerData[i].connection != null ? playerPrefab : botPrefab, spawnPoints[i].position, spawnPoints[i].rotation, transform);
            BaseRunner runner = runnerObject.GetComponent<BaseRunner>();
            Debug.Log("Adding runner: " + runnerData[i].characterData.characterName);
			runners.Add(runner);
			runner.SetId(runnerData[i].id);
            Debug.Log("RunnerConnection: " + runnerData[i].connection);
			Spawn(runnerObject, runnerData[i].connection);
		}
	}

	/// <summary>
	/// This method initializes the bots with random data.
	/// </summary>
	void InitializeBots()
    {
        while (runnerData.Count < 32)
        {
            runnerData.Add
            (
                new Runner
                {
                    id = runnerData.Count,
                    connection = null,
                    characterData = CharacterLoader.CreateRandomCharacterData(),
					name = "Runner #" + runnerData.Count.ToString().PadLeft(2, '0')
				}
            );
        }
    }

	/// <summary>
	/// This method is called when a player reaches the goal. It updates the player's data and checks if all players have finished. If it's the first player to finish, it starts the countdown.
	/// </summary>
	/// <param name="id"></param>
	[ServerRpc(RequireOwnership = false)]
    public void GoalReached(int id)
	{
		if (raceFinished) return;

		Debug.Log("Goal reached by " + id);
		Runner runner = runnerData.Find((r) => r.id == id);
		BaseRunner runnerObject = runners.Find((r) => r.GetId() == id);

		if(positionIndex == 0)
		{
			StartFinishCountdownRpc();
		}

		if (runner != null && !runner.goalReached)
		{
			runner.goalReached = true;
			positionIndex++;

			try
			{
				if (runnerObject is PlayerController player)
				{
					player.SetPosition(positionIndex, runnerData.Count);
				}
			}
			catch (InvalidCastException)
			{
				Debug.Log("Runner is not a player");
			}
		}

		if (positionIndex >= runnerData.Count)
		{
			raceFinished = true;
			StartCoroutine(FinishRace());
		}
	}

	/// <summary>
	/// This coroutine waits for a few seconds and then loads the lobby scene.
	/// </summary>
	IEnumerator FinishRace()
	{
		yield return new WaitForSeconds(1);

		LobbyManager.runnerData = new List<Runner>();

		SceneLoadData sld = new SceneLoadData("LobbyScene");
		SceneManager.LoadGlobalScenes(sld);

		SceneUnloadData sud = new SceneUnloadData(scene.nombreEscenario);
		SceneManager.UnloadGlobalScenes(sud);
	}

	[ObserversRpc(ExcludeServer = false)]
	void StartFinishCountdownRpc()
	{
		StartCoroutine(FinishCountdown());
	}

	/// <summary>
	/// This coroutine handles the countdown for the finish line. It updates the countdown text every second and then freezes all runners and sorts them based on their distance to the goal.
	/// </summary>
	IEnumerator FinishCountdown()
	{
		int countdownVal = 10;
		while (countdownVal > 0 && !raceFinished)
		{
			countdownText.text = countdownVal.ToString();
			yield return new WaitForSeconds(1);
			countdownVal--;
		}

		if (IsServerInitialized && !raceFinished)
		{
			raceFinished = true;
			FreezeAllRunners();
			SortRunners();
			yield return new WaitForSeconds(1);
			StartCoroutine(FinishRace());
		}
	}

	/// <summary>
	/// This method sorts the runners based on their distance to the goal. It updates the position of each runner and sets their position text.
	/// </summary>
	void SortRunners()
	{
		List<Runner> sortedRunners = runnerData
			.Where(r => !r.goalReached)
			.OrderBy(runner => 
			{
				BaseRunner runnerObj = runners.Find(r => r.GetId() == runner.id);
				return Vector3.Distance(runnerObj.transform.position, goal.position);
			}).ToList();

		for (int i = 0; i < sortedRunners.Count; i++)
		{
			positionIndex++;
			BaseRunner runnerObject = runners.Find((r) => r.GetId() == sortedRunners[i].id);
			try
			{
				if (runnerObject is PlayerController player)
				{
					player.SetPosition(positionIndex, runnerData.Count);
				}
			}
			catch (InvalidCastException)
			{
				Debug.Log("Runner is not a player");
			}
		}
	}

	/// <summary>
	/// This coroutine waits a little bit to give time to all players to change scene. Then it initializes the bots if needed. Finally, it spawns the runners and starts the countdown.
	/// </summary>
	IEnumerator WaitForPlayers()
	{
		yield return new WaitUntil(() => readyPlayers == 2);
		yield return new WaitForSeconds(1);

		Debug.Log("All players ready");

		runnerData = LobbyManager.runnerData;
		LobbyManager.runnerData = null;

		if (addBots)
			InitializeBots();
		Debug.Log("Initialized bots");
		SpawnRunners();

		yield return StartCoroutine(WaitForCountdown());
		Debug.Log("Countdown finished");
		UnfreezeAllRunners();
	}

	public IEnumerator WaitForCountdown()
    {
        yield return new WaitUntil(() => countdown.HasFinished());
	}
}

/// <summary>
/// This class is used to store the data of each runner. A runner can be a player or a bot.
/// </summary>
[Serializable]
public class Runner
{
	public int id;
    public string name;
	public NetworkConnection connection; // if null, it's a bot
	public CharacterData characterData;
	public bool goalReached = false;
}