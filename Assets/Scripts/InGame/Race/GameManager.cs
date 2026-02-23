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

    public static bool addBots = false;
    
    List<BaseRunner> runners = new List<BaseRunner>();
    int positionIndex = 0;
    bool raceFinished = false;

    // Track coroutines to stop them safely
    private Coroutine _waitForPlayersRoutine;
    private Coroutine _finishRaceRoutine;
    private Coroutine _finishCountdownRoutine;

    public override void OnStartServer()
    {
        SceneManager.OnLoadEnd += OnSceneLoaded;
        _waitForPlayersRoutine = StartCoroutine(WaitForPlayers());
    }

    private void OnSceneLoaded(SceneLoadEndEventArgs args)
    {
        readyPlayers++;
    }

    public override void OnStartClient()
    {
        SceneManager.OnLoadEnd += OnSceneLoaded;
        if (countdown != null) countdown.StartCountdown();
    }

    private void OnDestroy()
    {
        // Safely stop tracked coroutines instead of using StopAllCoroutines()
        if (_waitForPlayersRoutine != null) StopCoroutine(_waitForPlayersRoutine);
        if (_finishRaceRoutine != null) StopCoroutine(_finishRaceRoutine);
        if (_finishCountdownRoutine != null) StopCoroutine(_finishCountdownRoutine);
        
        if (SceneManager != null)
        {
            SceneManager.OnLoadEnd -= OnSceneLoaded;
        }
    }

    void FreezeAllRunners()
    {
        for (int i = 0; i < runners.Count; i++)
        {
            if (runners[i] != null)
            {
                runners[i].Freeze();
                runners[i].canMove = false;
            }
        }
    }

    void UnfreezeAllRunners()
    {
        for(int i = 0; i < runners.Count; i++)
        {
            if (runners[i] != null)
            {
                runners[i].UnFreeze();
                runners[i].canMove = true;
            }
        }
    }

    void SpawnRunners()
    {
        for (int i = 0; i < runnerData.Count; i++)
        {
            runnerData[i].goalReached = false;
            NetworkObject runnerObject = Instantiate(runnerData[i].connection != null ? playerPrefab : botPrefab, spawnPoints[i].position, spawnPoints[i].rotation, transform);
            BaseRunner runner = runnerObject.GetComponent<BaseRunner>();
            runners.Add(runner);
            runner.SetId(runnerData[i].id);
            Spawn(runnerObject, runnerData[i].connection);
        }
    }

    void InitializeBots()
    {
        while (runnerData.Count < 32)
        {
            runnerData.Add(new Runner {
                id = runnerData.Count,
                connection = null,
                characterData = CharacterLoader.CreateRandomCharacterData(),
                name = "Runner #" + runnerData.Count.ToString().PadLeft(2, '0')
            });
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void GoalReached(int id)
    {
        if (raceFinished) return;

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

            if (runnerObject is PlayerController player)
            {
                player.SetPosition(positionIndex, runnerData.Count);
            }
        }

        if (positionIndex >= runnerData.Count)
        {
            raceFinished = true;
            if (_finishRaceRoutine == null) 
                _finishRaceRoutine = StartCoroutine(FinishRace());
        }
    }

    IEnumerator FinishRace()
    {
        raceFinished = true; 
        yield return new WaitForSeconds(0.1f); 

        if (IsServerInitialized)
        {
            // Despawn network objects safely
            foreach (var runner in runners)
            {
                if (runner != null && runner.NetworkObject != null && runner.NetworkObject.IsSpawned)
                {
                    runner.NetworkObject.Despawn();
                }
            }

            LobbyManager.runnerData = new List<Runner>();

            SceneLoadData sld = new SceneLoadData("LobbyScene");
            SceneManager.LoadGlobalScenes(sld);

            if (scene != null)
            {
                SceneUnloadData sud = new SceneUnloadData(scene.nombreEscenario);
                SceneManager.UnloadGlobalScenes(sud);
            }
        }
    }

    [ObserversRpc(ExcludeServer = false)]
    void StartFinishCountdownRpc()
    {
        if (_finishCountdownRoutine == null)
            _finishCountdownRoutine = StartCoroutine(FinishCountdown());
    }

    IEnumerator FinishCountdown()
    {
        int countdownVal = 10;
        
        while (countdownVal > 0 && !raceFinished && this != null)
        {
            if (countdownText != null) countdownText.text = countdownVal.ToString();
            yield return new WaitForSeconds(1);
            countdownVal--;
        }

        if (this != null && IsServerInitialized && !raceFinished)
        {
            raceFinished = true; 
            SortRunners(); 
            
            if (_finishRaceRoutine == null)
                _finishRaceRoutine = StartCoroutine(FinishRace()); 
        }
    }

    void SortRunners()
    {
        var remaining = runnerData.Where(r => !r.goalReached).ToList();
        List<Runner> sortedRunners = remaining.OrderBy((runner) => {
            BaseRunner runnerObject = runners.Find((r) => r.GetId() == runner.id);
            return runnerObject != null ? Vector3.Distance(runnerObject.transform.position, goal.position) : float.MaxValue;
        }).ToList();

        foreach (var runner in sortedRunners)
        {
            positionIndex++;
            BaseRunner runnerObject = runners.Find((r) => r.GetId() == runner.id);
            if (runnerObject is PlayerController player)
            {
                player.SetPosition(positionIndex, runnerData.Count);
            }
        }
    }

    IEnumerator WaitForPlayers()
    {
        yield return new WaitUntil(() => readyPlayers >= 2 || (readyPlayers >= 1 && addBots));
        yield return new WaitForSeconds(1);

        runnerData = LobbyManager.runnerData;
        LobbyManager.runnerData = null;

        if (addBots) InitializeBots();
        SpawnRunners();

        yield return StartCoroutine(WaitForCountdown());
        UnfreezeAllRunners();
    }

    public IEnumerator WaitForCountdown()
    {
        if (countdown == null) yield break;
        yield return new WaitUntil(() => countdown.HasFinished());
    }
}

[Serializable]
public class Runner
{
    public int id;
    public string name;
    public NetworkConnection connection;
    public CharacterData characterData;
    public bool goalReached = false;
}