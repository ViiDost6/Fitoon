using FishNet.Discovery;
using FishNet.Managing;
using FishNet.Object;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

/// <summary>
/// This class is used to discover servers on the network. It uses the NetworkDiscovery component to search for servers and connect to them.
/// </summary>
public class DiscoveryHandler : MonoBehaviour
{
	public static string Passcode;
	public static string isBotsEnabled = "BotsEnabled";
	NetworkDiscovery netDiscovery;
	[SerializeField] NetworkManager networkManager;
	[SerializeField] ushort port = 7077;    //This is the port where the game is hosted.

	void Start()
	{
		if (!SessionDataHolder.lookForLobby)
		{
			return;
		}

		netDiscovery = networkManager.GetComponent<NetworkDiscovery>();
		if (Passcode != null)
		{
			Debug.LogWarning("Passcode: '" + Passcode + "'");
			Debug.LogWarning("DiscoveryHandler.isBotsEnabled: '" + DiscoveryHandler.isBotsEnabled + "'");
			
			string combined = Passcode + isBotsEnabled;
			Debug.LogWarning("Combined: '" + combined + "'");
			
			netDiscovery.ChangeSecret(combined);
		}
		Debug.LogWarning("Secret final en Discovery: " + netDiscovery.GetSecret());
		netDiscovery.ServerFoundCallback += ConnectToServer;
		BeginSearch();
	}

	void OnDestroy()
	{
		netDiscovery.ServerFoundCallback -= ConnectToServer;
	}

	/// <summary>
	/// This method is used to start the server discovery process. It will search for servers on the network and connect to them if found. If none are found, it will start a server on the local machine.
	/// </summary>
	public void BeginSearch()
	{
		Debug.Log("Searching. . . ");
		netDiscovery.SearchForServers();
		StartCoroutine(SearchTimeOut());
	}

	private void ConnectToServer(IPEndPoint point)
	{
		StopAllCoroutines();
		netDiscovery.StopSearchingOrAdvertising();
		networkManager.ClientManager.StartConnection(point.Address.ToString(), port);
	}

	IEnumerator SearchTimeOut()
	{
		yield return new WaitForSeconds(Random.Range(2, 3f));
		if (netDiscovery.IsSearching)
		{
			netDiscovery.ServerFoundCallback -= ConnectToServer;
			netDiscovery.StopSearchingOrAdvertising();
			networkManager.ServerManager.StartConnection(port);
			netDiscovery.AdvertiseServer();
		}
	}
}
