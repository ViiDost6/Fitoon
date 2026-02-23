// LobbyManager.cs
using FishNet;
using FishNet.Connection;
using FishNet.Discovery;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Transporting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : NetworkBehaviour
{
	public class PlayerCard
	{
		public string name;
		public string title;
		public bool ready;
		public int score;
		public int pfp;
		public int banner;
	}

	public static List<Runner> runnerData = new List<Runner>();

	// Modificado a int para poder meter bots sin un NetworkConnection real
	readonly SyncDictionary<int, PlayerCard> playerEntries = new SyncDictionary<int, PlayerCard>();

	[SerializeField] GameObject cardPrefab;
	[SerializeField] ScrollRect scrollRect;
	[SerializeField] public GameObject content;
	[SerializeField] TextMeshProUGUI countDownText;
	[SerializeField] TextMeshProUGUI playerCountText;
	[SerializeField] GameObject lobbyPlayerPrefab;

	List<LeaderboardField> cardList = new List<LeaderboardField>();

	bool starting = false;
	private int botIdCounter = 0;

	private void Awake()
	{
		runnerData.Clear();
	}

	private void OnApplicationQuit()
	{
		InstanceFinder.NetworkManager.ClientManager.StopConnection();
		InstanceFinder.NetworkManager.ServerManager.StopConnection(true);
		Destroy(InstanceFinder.NetworkManager.gameObject);
	}

	private void Update()
	{
		List<PlayerCard> playerCards = playerEntries.Values.ToList();
		playerCards = playerCards.OrderByDescending(p => p.score).ToList();
		int i;
		for(i = 0; i  < playerCards.Count; i++)
		{
			if(i >= cardList.Count)
			{
				LeaderboardField card = Instantiate(cardPrefab, content.transform).GetComponent<LeaderboardField>();
				cardList.Add(card);
			}

			cardList[i].SetBanner(playerCards[i].banner);
			cardList[i].SetPlayerName(playerCards[i].name);
			cardList[i].SetReady(playerCards[i].ready);
			cardList[i].SetProfilePicture(playerCards[i].pfp);
			cardList[i].SetMedals(playerCards[i].score);
			cardList[i].SetPlayerTitle(playerCards[i].title);
		}
		for(int j = cardList.Count - 1; j >= i; j--)
		{
			Destroy(cardList[j].gameObject);
			cardList.RemoveAt(j);
		}
	}

	public override void OnStartServer()
	{
		InstanceFinder.NetworkManager.ClientManager.StartConnection();
		InstanceFinder.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
		playerEntries.OnChange += OnChangePlayerEntries;
	}

	public override void OnStartClient()
	{
		if (!SessionDataHolder.lookForLobby)
		{
			SpawnPlayer(InstanceFinder.ClientManager.Connection);
		}
		SessionDataHolder.lookForLobby = false;
	}

	[ServerRpc(RequireOwnership = false)]
	void SpawnPlayer(NetworkConnection connection)
	{
		GameObject player = Instantiate(lobbyPlayerPrefab);
		Spawn(player, connection);
	}

	private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
	{
		if(args.ConnectionState == RemoteConnectionState.Stopped)
		{
			playerEntries.Remove(connection.ClientId);
			runnerData.Remove(runnerData.Find((r) => r.connection == connection));
		}
	}

	private void OnChangePlayerEntries(SyncDictionaryOperation op, int key, PlayerCard value, bool asServer)
	{
		if(!IsServerInitialized)
		{
			return;
		}
		SetPlayerNumber(playerEntries.Count);
		CheckReady();
	}

	public void ReadyButton()
	{
		SetReady(InstanceFinder.ClientManager.Connection);
	}

	public void ExitButton()
	{
		if (InstanceFinder.NetworkManager != null)
		{
			InstanceFinder.NetworkManager.ClientManager.StopConnection();
			InstanceFinder.NetworkManager.ServerManager.StopConnection(true);
		}
		Destroy(InstanceFinder.NetworkManager.gameObject);
		UnityEngine.SceneManagement.SceneManager.LoadScene(1);
	}

	[ServerRpc(RequireOwnership = false)]
	public void AddPlayer(NetworkConnection key, string name, string title, int pfp, int banner, int score, CharacterData characterData)
	{
		PlayerCard card = new PlayerCard()
		{
			name = name,
			ready = false,
			pfp = pfp,
			score = score,
			banner = banner,
			title = title
		};
		playerEntries.Add(key.ClientId, card);
		playerEntries.Dirty(key.ClientId);

		runnerData.Add(new Runner()
		{
			id = key.ClientId,
			name = name,
			connection = key,
			characterData = characterData
		});
	}

	[Server]
	public void AddBot()
	{
		botIdCounter++;
		string botName = "Bot_" + botIdCounter;

		// Todo a 0 por defecto para que no revienten los arrays de AssetLoader
		PlayerCard botCard = new PlayerCard()
		{
			name = botName,
			ready = true,
			pfp = 0,
			score = 0,
			banner = 0,
			title = "AI Challenger"
		};

		// Usamos un ID negativo para que nunca coincida con el ClientId de un jugador real
		int botId = -botIdCounter;
		playerEntries.Add(botId, botCard);

		// Copiamos el personaje del jugador anfitrión (el primero en runnerData)
		CharacterData botChar = null;
		if (runnerData.Count > 0)
		{
			botChar = runnerData[0].characterData;
		}

		runnerData.Add(new Runner()
		{
			id = botId,
			name = botName,
			connection = null,
			characterData = botChar
		});

		SetPlayerNumber(playerEntries.Count);
		CheckReady();
	}

	[ServerRpc(RequireOwnership = false)]
	void SetReady(NetworkConnection connection)
	{
		PlayerCard card;
		if (playerEntries.TryGetValue(connection.ClientId, out card))
		{
			card.ready = !card.ready;
			playerEntries.Dirty(connection.ClientId);
		}
	}

	[ServerRpc(RequireOwnership = false)]
	public void CheckReady()
	{
		if (!IsServerInitialized)
			return;
		int ready = 0;
		foreach (PlayerCard card in playerEntries.Values)
		{
			if (card.ready)
			{
				ready++;
			}
		}
		if (ready / (float)playerEntries.Count >= 0.6f)
		{
			if (starting)
				return;

			InstanceFinder.NetworkManager.GetComponent<NetworkDiscovery>().StopSearchingOrAdvertising();
			starting = true;
			StartCoroutine(StartGameCountdown());
		}
		else
		{
			ChangeCountdownText("WAITING FOR PLAYERS");
			InstanceFinder.NetworkManager.GetComponent<NetworkDiscovery>().AdvertiseServer();
			starting = false;
		}
	}

	[ObserversRpc]
	public void ChangeCountdownText(string s)
	{
		countDownText.text = s;
	}

	[ObserversRpc]
	public void SetPlayerNumber(int i)
	{
		playerCountText.text = i.ToString() + "/32";
	}

	IEnumerator StartGameCountdown()
	{
		if (!starting)
			yield break;
		for (int i = 5; i >= 0; i--)
		{
			ChangeCountdownText("STARTING IN " + i.ToString());
			yield return new WaitForSeconds(1);
			if (!starting)
				yield break;
		}

		if (IsServerInitialized)
		{
			int botsNeeded = 32 - playerEntries.Count;
			for (int b = 0; b < botsNeeded; b++)
			{
				AddBot();
			}
		}

		SceneLoadData sld = new SceneLoadData("FindingScenario");
		SceneManager.LoadGlobalScenes(sld);

		SceneUnloadData sud = new SceneUnloadData("LobbyScene");
		SceneManager.UnloadGlobalScenes(sud);
	}
}