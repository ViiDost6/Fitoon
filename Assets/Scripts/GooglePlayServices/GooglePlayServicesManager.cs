#if UNITY_ANDROID
using System;
using UnityEngine;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
using UnityEngine.SceneManagement;
using Firebase.Extensions;

public class GooglePlayServicesManager : MonoBehaviour
{
    public static GooglePlayServicesManager instance;
    private enum DataOperation { Save, Load }
    private DataOperation currentOperation;
    private ISavedGameMetadata currentSavedGame = null;
    private string savedGameFilename = "PlayerData";

    /// <summary>
    /// Singleton pattern
    /// </summary>
    private void Awake()
    {
        if (GooglePlayServicesManager.instance == null)
        {
            GooglePlayServicesManager.instance = this;
            SignIn();
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    /// <summary>
    /// Manually signs in the player, usually not needed
    /// </summary>
    public void SignIn()
    {
        PlayGamesPlatform.Activate();
        PlayGamesPlatform.Instance.Authenticate(OnSignInResult);
    }

    private void OnSignInResult(SignInStatus signInStatus)
    {
        if (signInStatus == SignInStatus.Success)
        {
            Debug.Log("Sign in success");
            SaveData.ReadFromJson();
        }
        else Debug.Log("Sign in failed");
    }

    public void SaveGame(string savedData)
    {
        if (currentSavedGame == null || !currentSavedGame.IsOpen)
        {
            currentOperation = DataOperation.Save;
            OpenSavedGame();
        }
        var update = new SavedGameMetadataUpdate.Builder()
                .WithUpdatedDescription("Saved at " + DateTime.Now.ToString())
                .WithUpdatedPlayedTime(currentSavedGame.TotalTimePlayed.Add(TimeSpan.FromHours(1)))
                .Build();

        PlayGamesPlatform.Instance.SavedGame.CommitUpdate(
                currentSavedGame,
                update,
                System.Text.ASCIIEncoding.Default.GetBytes(savedData),
                (status, updated) => { return; });
        Debug.Log("[SAVE] Datos guardados en la nube");
    }

    public void LoadGame()
    {
        if (currentSavedGame == null || !currentSavedGame.IsOpen)
        {
            currentOperation = DataOperation.Load;
            OpenSavedGame();
        }
        else
        {
            ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;
            savedGameClient.ReadBinaryData(currentSavedGame, OnSavedGameDataRead);
        }
    }

    private void OnSavedGameDataRead(SavedGameRequestStatus status, byte[] data)
    {
        if (status == SavedGameRequestStatus.Success && data != null && data.Length != 0)
        {
            string savedData = System.Text.ASCIIEncoding.Default.GetString(data);
            SaveData.ReceiveData(savedData);
            SceneManager.LoadScene("Inicial");
        }
        else
        {
            Debug.Log("Error reading saved game data: " + status);
            SaveData.ReceiveData(null);
            SceneManager.LoadScene("Inicial");
        }
    }

    /// <summary>
    /// Opens the saved game file in the cloud (if it doesn't exist, it creates it)
    /// </summary>
    private void OpenSavedGame()
    {
        ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;
        savedGameClient.OpenWithAutomaticConflictResolution(savedGameFilename, DataSource.ReadCacheOrNetwork, ConflictResolutionStrategy.UseLongestPlaytime, OnSavedGameOpened);
    }

    private void OnSavedGameOpened(SavedGameRequestStatus status, ISavedGameMetadata game)
    {
        if (status == SavedGameRequestStatus.Success)
        {
            currentSavedGame = game;
            ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;
            if (currentOperation == DataOperation.Load) savedGameClient.ReadBinaryData(currentSavedGame, OnSavedGameDataRead);
        }
        else Debug.Log("Error opening saved game: " + status);
    }

    public string GetPlayerUsername()
    {
        return PlayGamesPlatform.Instance.GetUserDisplayName();
    }
}
#endif