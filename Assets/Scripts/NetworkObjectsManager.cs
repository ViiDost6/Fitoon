using UnityEngine;
using FishNet.Managing; // Espacio de nombres principal de FishNet
using FishNet.Transporting;

public class NetworkObjectsManager : MonoBehaviour
{
    public bool isTrainingScene;

    void Start()
    {
        if (isTrainingScene)
        {
            StartNetwork();
        }
    }

    private void StartNetwork()
    {
        // En FishNet, el InstanceFinder es la forma más rápida de acceder al NetworkManager
        if (FishNet.InstanceFinder.NetworkManager != null)
        {
            // Iniciamos el Servidor y el Cliente simultáneamente (Host)
            FishNet.InstanceFinder.ServerManager.StartConnection();
            FishNet.InstanceFinder.ClientManager.StartConnection();
            
            Debug.Log("Simulación de red FishNet iniciada: Servidor + Cliente.");
        }
        else
        {
            Debug.LogError("No se encontró el NetworkManager de FishNet en la escena.");
        }
    }
}