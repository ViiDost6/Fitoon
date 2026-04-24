using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InitialUIManager : UIManager
{
    [Header("Progress Data")]
    [SerializeField] private ProgressData progressData;

    [Header("Private Match Passcode")]
    [SerializeField] private TMP_InputField passcodeField;

    [Header("Player Resources And Stats")]
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI medalText;
    [SerializeField] private Slider medalSlider;
    [SerializeField] private TextMeshProUGUI leagueText;
    [SerializeField] private Image leagueImage;
    [SerializeField] private TextMeshProUGUI streakText;

    [Header("Player Data")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TMP_InputField playerNameField;
    [SerializeField] private Banner banner;
    [SerializeField] private Image profileIconImage;

    [Header("Leaderboard Data")]
    [SerializeField] private GameObject leaderboardScroll;
    [SerializeField] private GameObject leaderboardTabs;
    [SerializeField] private GameObject selectedContentTab;
    [SerializeField] private GameObject Global;
    [SerializeField] private GameObject AllGyms;
    [SerializeField] private GameObject Gym;

    [SerializeField] private Sprite selectedTabSprite;
    [SerializeField] private Sprite deselectedTabSprite;
    

    [SerializeField] private GameObject leaderboardFieldPrefab;
    [SerializeField] private GameObject leaderboardPlayerPreview;

    [Header("Pay Screen")]
    [SerializeField] private GameObject UnlinkedGymScreen;


    private void Start()
    {
        SaveData.ReadFromJson();
        UpdateAllUI();
        if(SaveData.player.gymKey != -1) CheckGymStatus();
    }

    private void Update()
    {
        // if (Input.GetKeyDown(KeyCode.Delete)) {
        //     Debug.Log("Erased");
        //     SaveData.SaveToJson();
        //     UpdateAllUI();
        // }
        // if (Input.GetKeyDown(KeyCode.Keypad2)) {
        //     SaveData.player.normalCoins += 20;
        //     SaveData.SaveToJson();
        //     UpdateCoins();
        // }
        // if (Input.GetKeyDown(KeyCode.Keypad1)) {

        //     SaveData.player.expPoints += 50;
        //     SaveData.SaveToJson();
        //     UpdateExp();
        // }
        // if (Input.GetKeyDown(KeyCode.UpArrow)) {

        //     SaveData.player.medals += 50;
        //     SaveData.SaveToJson();
        //     UpdateMedals();
        // }
        // if (Input.GetKeyDown(KeyCode.DownArrow)) {

        //     SaveData.player.medals -= 50;
        //     SaveData.SaveToJson();
        //     UpdateMedals();
        // }
    }

    // --------------------------------------------------------------------------------------------------------------------------------------------------
    // Data display
    // --------------------------------------------------------------------------------------------------------------------------------------------------

    public void SaveUsername(string value)
    {
        if(value == "" || value == SaveData.player.username) return;
        SaveData.ChangeUsername(value, (result) =>
        {
            MainThreadDispatcher.instance.Enqueue(() =>
            {
                UpdateProfile(result);
            });
        });
    }

    public void CheckGymStatus()
    {
        DatabaseManager.instance.CheckGymStatus(SaveData.player.gymKey, (status) =>
        {
            if(!status)
            {
                MainThreadDispatcher.instance.Enqueue(() =>
                {
                    Debug.Log("Gym status false, opening pay screen");
                    OpenMenu(UnlinkedGymScreen);
                    SaveData.player.gymKey = -1;
                    SaveData.SaveToJson();
                });
            }
        });
    }

    public void SetLeaderboardTab(GameObject selectedTab)
    {
        foreach (Transform tab in leaderboardScroll.transform) {
            tab.gameObject.SetActive(tab.gameObject == selectedTab);
            selectedContentTab = selectedTab.GetComponent<ScrollRect>().content.gameObject;
        }
    }

    public void SetTabButtonSelected(GameObject selectedButton)
    {
        foreach (Transform button in leaderboardTabs.transform) {
            if (button.gameObject == selectedButton) {
                button.GetComponent<Image>().sprite = selectedTabSprite;
            } else {
                button.GetComponent<Image>().sprite = deselectedTabSprite;
            }
        }
    }

    public void UpdateAllUI()
    {
        UpdateExp();
        UpdateCoins();
        UpdateMedals();
        UpdateProfile(true);
        UpdateStreak();
    }
    public void UpdateCoins()
    {
        if (coinsText != null) coinsText.text = SaveData.player.normalCoins.ToString();
    }

    public void UpdateExp()
    {
        int currentLevel = CalculateLevelFromXP(SaveData.player.expPoints);
        if (levelText != null)
        {
            levelText.text = currentLevel.ToString();
        }
        if (expText != null) {
            int totalXPNeededForNextLevel = CalculateTotalXPNeededForNextLevel(currentLevel);
            int pointsNeededForNextLevel = totalXPNeededForNextLevel - SaveData.player.expPoints;

            expText.text = $"{SaveData.player.expPoints}/{totalXPNeededForNextLevel}";
        }
    }

    public void UpdateMedals()
    {
        int rank = SaveData.player.medals / progressData.rankMedalInterval;
        if (rank >= progressData.rankTitles.Count) {
            rank = progressData.rankTitles.Count - 1;
        }
        if (medalText != null) {
            if (rank == progressData.rankTitles.Count - 1) {
                medalText.text = SaveData.player.medals.ToString();
            }
            else {
                medalText.text = SaveData.player.medals.ToString() + "/" + ((rank + 1) * progressData.rankMedalInterval).ToString();
            }
        }
        if (medalSlider != null) {
            if (rank == progressData.rankTitles.Count - 1) {
                medalSlider.minValue = rank * progressData.rankMedalInterval - 1;
                medalSlider.maxValue = rank * progressData.rankMedalInterval;
            }
            else {
                medalSlider.minValue = rank * progressData.rankMedalInterval;
                medalSlider.maxValue = (rank + 1) * progressData.rankMedalInterval;
            }
            medalSlider.value = SaveData.player.medals;
        }
        if (leagueImage != null) {
            leagueImage.sprite = progressData.rankSprites[rank];
        }
        if (leagueText != null) {
            leagueText.text = (progressData.rankTitles[rank] + " league").ToUpper();
        }
    }

    public void UpdateStreak()
    {
        if (streakText != null) streakText.text = SaveData.player.streak.ToString();
    }

    public void UpdateProfile(bool usernameNotTaken = false)
    {
        if (playerNameText != null) playerNameText.text = SaveData.player.username;
        if (playerNameField != null)
        {
            if (!usernameNotTaken) StartCoroutine(ShowError());
            else playerNameField.text = SaveData.player.username;
        }
        if (banner != null) {
            banner.SetBanner(SaveData.player.bannerID);
            banner.SetProfilePicture(SaveData.player.pfp);
        }

    }
    public void DisplayLeaderboardData()
    {
        if(leaderboardPlayerPreview != null) leaderboardPlayerPreview.transform.parent.gameObject.SetActive(true);
        if (selectedContentTab == Global)
        {
            DatabaseManager.instance.GetLeaderboard((leaderboard) => {
                MainThreadDispatcher.instance.Enqueue(() => {
                    DisplayLeaderBoardData(leaderboard);
                });
            });
        }
        else if (selectedContentTab == Gym && SaveData.player.gymKey != -1) {
            DatabaseManager.instance.GetLeaderboard((leaderboard) => {
                MainThreadDispatcher.instance.Enqueue(() => {
                    DisplayGymLeaderBoardData(leaderboard);
                });
            }, SaveData.player.gymKey);
        }
        else if (selectedContentTab == AllGyms) {
            DatabaseManager.instance.GetGymsLeaderboardWithNames((leaderboard) => {
                MainThreadDispatcher.instance.Enqueue(() => {
                    foreach (var item in leaderboard) Debug.Log(item.Item1);
                    DisplayAllGymsLeaderBoardData(leaderboard);
                });
            });
        }
    }

    public void DisplayLeaderBoardData(List<Tuple<string, UserData>> leaderboard)
    {
        foreach(Transform child in selectedContentTab.transform) {
            Destroy(child.gameObject);
        }
        for (int i = 0; i < leaderboard.Count; i++) {
            GameObject field = Instantiate(leaderboardFieldPrefab, selectedContentTab.transform);
            LeaderboardField leaderboardField = field.GetComponent<LeaderboardField>();
            leaderboardField.SetBanner(leaderboard[i].Item2.bannerID);
            leaderboardField.SetProfilePicture(leaderboard[i].Item2.profileID);
            leaderboardField.SetPlayerName(leaderboard[i].Item1);
            leaderboardField.SetPlayerTitle(leaderboard[i].Item2.title);
            leaderboardField.SetMedals(leaderboard[i].Item2.medals);
            leaderboardField.SetPosition(i);

            if (leaderboard[i].Item1 == SaveData.player.username)
            {
                LeaderboardField playerField = leaderboardPlayerPreview.GetComponent<LeaderboardField>();
                playerField.SetBanner(leaderboard[i].Item2.bannerID);
                playerField.SetProfilePicture(leaderboard[i].Item2.profileID);
                playerField.SetPlayerName(leaderboard[i].Item1);
                playerField.SetPlayerTitle(leaderboard[i].Item2.title);
                playerField.SetMedals(leaderboard[i].Item2.medals);
                playerField.SetPosition(i);
            }
        }
    }

    public void DisplayGymLeaderBoardData(List<Tuple<string, UserData>> leaderboard)
    {
        foreach (Transform child in selectedContentTab.transform) {
            Destroy(child.gameObject);
        }
        for (int i = 0; i < leaderboard.Count; i++) {
            GameObject field = Instantiate(leaderboardFieldPrefab, selectedContentTab.transform);
            LeaderboardField leaderboardField = field.GetComponent<LeaderboardField>();
            leaderboardField.SetBanner(leaderboard[i].Item2.bannerID);
            leaderboardField.SetProfilePicture(leaderboard[i].Item2.profileID);
            leaderboardField.SetPlayerName(leaderboard[i].Item1);
            leaderboardField.SetPlayerTitle(leaderboard[i].Item2.title);
            leaderboardField.SetMedals(leaderboard[i].Item2.medals);
            leaderboardField.SetPosition(i);

            if (leaderboard[i].Item1 == SaveData.player.username) {
                LeaderboardField playerField = leaderboardPlayerPreview.GetComponent<LeaderboardField>();
                playerField.SetBanner(leaderboard[i].Item2.bannerID);
                playerField.SetProfilePicture(leaderboard[i].Item2.profileID);
                playerField.SetPlayerName(leaderboard[i].Item1);
                playerField.SetPlayerTitle(leaderboard[i].Item2.title);
                playerField.SetMedals(leaderboard[i].Item2.medals);
                playerField.SetPosition(i);
            }
        }
    }

    public void DisplayAllGymsLeaderBoardData(List<Tuple<string, int>> leaderboard)
    {
        leaderboardPlayerPreview.transform.parent.gameObject.SetActive(false);
        foreach (Transform child in selectedContentTab.transform) {
            Destroy(child.gameObject);
        }
        for (int i = 0; i < leaderboard.Count; i++) {
            GameObject field = Instantiate(leaderboardFieldPrefab, selectedContentTab.transform);
            LeaderboardField leaderboardField = field.GetComponent<LeaderboardField>();
            leaderboardField.SetPlayerName(leaderboard[i].Item1);
            leaderboardField.SetMedals(leaderboard[i].Item2);
            leaderboardField.SetPosition(i);
        }
    }

    // --------------------------------------------------------------------------------------------------------------------------------------------------
    // Progresion de puntos
    // --------------------------------------------------------------------------------------------------------------------------------------------------
    //Se utiliza una progresion geométrica de manera que sea un aumento exponencial de dificultad. De esta manera ganar nivel al principio es sencillo para motivar a jugar, pero
    //subir de nivel en un momento más avanzado es más difícil. Implica jugar muchas partidas o pagar para conseguir monedas.
    //Formula: PuntosNecesarios(nivel)=base*factor elevado a (nivel−1)


    //Ejemplo con base 100 y factor 1.5: Para llegar al nivel 1 hacen falta 0+100 puntos. Nivel 2: 100+150 puntos. Nivel 3: 100+150+225 puntos...
    private int CalculateXPForNextLevel(int level)
    {
        return Mathf.CeilToInt(progressData.baseXP * Mathf.Pow(1.5f, level - 1));
    }

    public int CalculateLevelFromXP(int exp)
    {
        int level = 1;
        int xpForNextLevel = Mathf.CeilToInt(progressData.baseXP);

        while (exp >= xpForNextLevel) {
            exp -= xpForNextLevel;
            level++;
            xpForNextLevel = CalculateXPForNextLevel(level);
        }

        return level;
    }

    private int CalculateTotalXPNeededForNextLevel(int currentLevel)
    {
        int totalXP = 0;

        for (int level = 1; level <= currentLevel; level++) {
            totalXP += CalculateXPForNextLevel(level);
        }

        return totalXP;
    }

    IEnumerator ShowError()
    {
        playerNameField.textComponent.color = Color.red;
        playerNameField.text = "Already taken!";
        yield return new WaitForSeconds(1f);
        playerNameField.textComponent.color = Color.white;
        playerNameField.text = SaveData.player.username;
    }

    // --------------------------------------------------------------------------------------------------------------------------------------------------
    // Lobby
    // --------------------------------------------------------------------------------------------------------------------------------------------------
    public void ExitApp()
    {
        Application.Quit();
    }

    public void SetPasscode(string value)
    {
        DiscoveryHandler.Passcode = value;
    }

    public void JoinLobby(bool privateLobby)
    {
        if (!privateLobby)
        {
            DiscoveryHandler.Passcode = null;
            DiscoveryHandler.isBotsEnabled = "BotsEnabled";
        }
        DiscoveryHandler.Passcode = null;
        Debug.Log("Passcode: " + DiscoveryHandler.Passcode);
        SessionDataHolder.Reset();
        SceneManager.LoadScene("LobbyScene");
    }
}
