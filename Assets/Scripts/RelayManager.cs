using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Networking.Transport.Relay;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    [Header("UI Panelleri")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject hostMapSelectionPanel; 

    [Header("UI Metin Elemanları")]
    [SerializeField] private TMP_InputField codeInputField; 
    [SerializeField] private TMP_InputField nicknameInputField; 
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private UnityEngine.UI.Button startGameButton;
    [SerializeField] private TMP_Dropdown mapDropdown; 
    [SerializeField] private TMP_Text playlistText; 
    [SerializeField] private TMP_Text errorText; 

    [Header("Oyun İçi Doğum Ayarları")]
    [SerializeField] private GameObject gamePlayerPrefab; 

    public int GetCurrentMapIndex() => currentMapIndex;
    public int GetPlaylistCount() => gamePlaylist.Count;

    [HideInInspector] public string LocalProfileName;
    private Dictionary<ulong, (string name, Color32 color)> savedPlayerData = new Dictionary<ulong, (string, Color32)>();
    
    private Dictionary<ulong, int> clientSlots = new Dictionary<ulong, int>();
    private bool isGameInProgress = false; 
    private bool isConnecting = false;
    private bool isIntentionallyLeaving = false; 
    private string currentJoinCode = "";
    private List<string> gamePlaylist = new List<string>();
    private bool showingScoreboard = false;
    private int currentMapIndex = 0;
    private Dictionary<ulong, int> playerScores = new Dictionary<ulong, int>();
    private Dictionary<ulong, int> lastMatchPoints = new Dictionary<ulong, int>();
    private bool isReturningToLobby = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu")
            {
                Instance.mainMenuPanel = this.mainMenuPanel;
                Instance.lobbyPanel = this.lobbyPanel;
                Instance.hostMapSelectionPanel = this.hostMapSelectionPanel;
                Instance.codeInputField = this.codeInputField;
                Instance.nicknameInputField = this.nicknameInputField;
                Instance.lobbyCodeText = this.lobbyCodeText;
                Instance.playerListText = this.playerListText;
                Instance.startGameButton = this.startGameButton;
                Instance.mapDropdown = this.mapDropdown;
                Instance.playlistText = this.playlistText;
                Instance.errorText = this.errorText;

                bool isConnected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;

                if (Instance.isReturningToLobby || isConnected)
                {
                    Instance.ShowLobbyAfterReturn();
                    Instance.isReturningToLobby = false;
                }
                else
                {
                    Instance.ResetUIToMainMenu();
                }
            }
            
            Destroy(gameObject); 
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    async void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.runInBackground = true;
        
        if (Instance == this)
        {
            ResetUIToMainMenu();

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
                
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }

            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    InitializationOptions options = new InitializationOptions();
                    LocalProfileName = "Player_" + Random.Range(1000, 9999);
                    options.SetProfile(LocalProfileName);

                    await UnityServices.InitializeAsync(options);
                    if (!AuthenticationService.Instance.IsSignedIn)
                    {
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    }
                }
            }
            catch (System.Exception e) { Debug.LogError("Servis hatası: " + e.Message); }
        }
    }

    public void ResetUIToMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (hostMapSelectionPanel != null) hostMapSelectionPanel.SetActive(false);
        if (startGameButton != null) startGameButton.gameObject.SetActive(false); 
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    private void ShowLobbyAfterReturn()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(true);

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        if (hostMapSelectionPanel != null) hostMapSelectionPanel.SetActive(isHost);

        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            startGameButton.interactable = false;
        }

        currentMapIndex = 0;
        showingScoreboard = false;
        isGameInProgress = false;
        gamePlaylist.Clear();
        playerScores.Clear();
        lastMatchPoints.Clear();
        UpdatePlaylistUI();
        UpdatePlayerListUI();
        if (lobbyCodeText != null) 
        {
            lobbyCodeText.text = "ROOM CODE: " + currentJoinCode;
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (isGameInProgress)
        {
            response.Approved = false;
            response.Reason = "Game in progress!";
            return;
        }

        if (clientSlots.Count >= 8)
        {
            response.Approved = false;
            response.Reason = "Room is full!";
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (!NetworkManager.Singleton.IsHost)
            {
                HideError();
                mainMenuPanel.SetActive(false);
                lobbyPanel.SetActive(true);
                hostMapSelectionPanel.SetActive(false);
            }
        }
    }

    public int AssignSlot(ulong clientId)
    {
        if (clientSlots.ContainsKey(clientId)) return clientSlots[clientId];
        
        for (int i = 0; i < 8; i++) 
        {
            if (!clientSlots.ContainsValue(i))
            {
                clientSlots.Add(clientId, i);
                return i;
            }
        }
        return 0; 
    }

    public int GetPlayerSlot(ulong clientId)
    {
        if (clientSlots.TryGetValue(clientId, out int slot)) return slot;
        return 0; 
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && clientSlots.ContainsKey(clientId))
        {
            clientSlots.Remove(clientId);
        }

        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (!isIntentionallyLeaving)
            {
                string reason = NetworkManager.Singleton.DisconnectReason;
                if (string.IsNullOrEmpty(reason)) reason = "Could not connect to room. Invalid code.";
                ShowError(reason);
                LeaveLobby();
            }
        }
        else
        {
            UpdatePlayerListUI(); 
        }
    }

    public void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.gameObject.SetActive(true);
            errorText.text = $"<color=red>INFO:</color> {message}";
            CancelInvoke(nameof(HideError));
            Invoke(nameof(HideError), 4f); 
        }
    }

    private void HideError()
    {
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    private void SetNicknameBeforeConnect()
    {
        if (nicknameInputField != null && !string.IsNullOrEmpty(nicknameInputField.text))
        {
            LocalProfileName = nicknameInputField.text;
        }
    }

    private async Task SafeShutdownNetwork()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            isIntentionallyLeaving = true; 
            NetworkManager.Singleton.Shutdown();
            while (NetworkManager.Singleton.IsListening)
            {
                await Task.Delay(50); 
            }
            await Task.Delay(200);
            isIntentionallyLeaving = false;
        }
    }

    public async void CreateRelay()
    {
        if (isConnecting) return;
        isConnecting = true;
        ShowError("Creating Room...");

        SetNicknameBeforeConnect();
        await SafeShutdownNetwork(); 

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(7); 
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            if (transport != null)
            {
                string connectionType = transport.UseWebSockets ? "wss" : "dtls";
                RelayServerData relayServerData = new RelayServerData(allocation, connectionType);
                transport.SetRelayServerData(relayServerData);
                
                isGameInProgress = false; 
                clientSlots.Clear(); 
                currentJoinCode = joinCode;

                if (lobbyCodeText != null) lobbyCodeText.text = "ROOM CODE: " + joinCode;
                if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
                if (lobbyPanel != null) lobbyPanel.SetActive(true);
                if (hostMapSelectionPanel != null) hostMapSelectionPanel.SetActive(true);

                if (startGameButton != null)
                {
                    startGameButton.gameObject.SetActive(true);
                    startGameButton.interactable = false;
                }
                UpdatePlaylistUI();

                NetworkManager.Singleton.StartHost();
                // Ekstra güvenlik: Spawn senkron olmasa bile listeyi tazele
                UpdatePlayerListUI();
                
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
            }
            isConnecting = false;
        }
        catch (RelayServiceException e) 
        { 
            Debug.LogError("Relay hatası: " + e.Message); 
            ShowError("Failed to create room!");
            isConnecting = false;
        }
    }

    public async void JoinRelay() 
    {
        if (isConnecting) return;
        isConnecting = true;

        SetNicknameBeforeConnect();
        await SafeShutdownNetwork(); 

        try
        {
            if (codeInputField == null) { isConnecting = false; return; }
            string joinCode = codeInputField.text; 
            
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                ShowError("Invalid code!");
                isConnecting = false;
                return;
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            if (transport != null)
            {
                string connectionType = transport.UseWebSockets ? "wss" : "dtls";
                RelayServerData relayServerData = new RelayServerData(joinAllocation, connectionType);
                transport.SetRelayServerData(relayServerData);
                currentJoinCode = joinCode;
                ShowError("Connecting to room...");
                NetworkManager.Singleton.StartClient();
                if (lobbyCodeText != null) lobbyCodeText.text = "ROOM CODE: " + joinCode;
            }
            isConnecting = false;
        }
        catch (System.Exception) 
        { 
            ShowError("Invalid code! Room not found or connection failed."); 
            isConnecting = false;
        }
    }

    public void UpdatePlayerListUI()
    {
        if (playerListText == null || lobbyPanel == null || !lobbyPanel.activeInHierarchy) return;

        LobbyPlayer[] players = FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None);
        bool allReady = true;
        playerListText.text = "ROOM PLAYERS:\n";
        savedPlayerData.Clear();

        foreach (LobbyPlayer player in players)
        {
            string readyStatus = player.isReady.Value ? "<color=green>[READY]</color>" : "<color=red>[WAITING]</color>";
            playerListText.text += $"- {player.playerName.Value} {readyStatus}\n";
            if (!player.isReady.Value) allReady = false;

            if (!savedPlayerData.ContainsKey(player.OwnerClientId))
            {
                savedPlayerData.Add(player.OwnerClientId, (player.playerName.Value.ToString(), player.playerColor.Value));
            }
        }

        if (NetworkManager.Singleton.IsHost && startGameButton != null && startGameButton.gameObject.activeSelf)
        {
            startGameButton.interactable = allReady && players.Length >= 1 && gamePlaylist.Count > 0; 
        }
    }

    public bool GetMySavedData(ulong clientId, out string name, out Color32 color)
    {
        if (savedPlayerData.TryGetValue(clientId, out var data))
        {
            name = data.name;
            color = data.color;
            return true;
        }
        name = "Player_" + clientId;
        color = Color.white;
        return false;
    }

    public void ChangeMyColor(string colorHex)
    {
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            if (ColorUtility.TryParseHtmlString(colorHex, out Color chosenColor))
            {
                Color32 strictColor = chosenColor; 
                NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<LobbyPlayer>().SelectColorServerRpc(strictColor);
            }
        }
    }

    public void AddMapToPlaylist()
    {
        if (mapDropdown == null || mapDropdown.options.Count == 0) return;
        string selectedMap = mapDropdown.options[mapDropdown.value].text;
        gamePlaylist.Add(selectedMap);
        UpdatePlaylistUI();
        UpdatePlayerListUI(); 
    }

    public void ClearPlaylist()
    {
        gamePlaylist.Clear();
        UpdatePlaylistUI();
        UpdatePlayerListUI();
    }

    private void UpdatePlaylistUI()
    {
        if (playlistText == null) return;
        playlistText.text = "PLAYLIST:\n";
        for (int i = 0; i < gamePlaylist.Count; i++)
        {
            playlistText.text += $"{i + 1}. {gamePlaylist[i]}\n";
        }
    }

    public void OnReadyClicked()
    {
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        { NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<LobbyPlayer>().ToggleReadyServerRpc(); }
    }

    public void OnStartGameClicked()
    {
        if (NetworkManager.Singleton.IsHost && gamePlaylist.Count > 0)
        {
            if (isGameInProgress) return; 

            isGameInProgress = true; 
            showingScoreboard = false; 
            currentMapIndex = 0;

            if (lobbyPanel != null) lobbyPanel.SetActive(false);
            if (hostMapSelectionPanel != null) hostMapSelectionPanel.SetActive(false);

            NetworkManager.Singleton.SceneManager.LoadScene(gamePlaylist[currentMapIndex], UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    public void AddScore(ulong clientId, int points)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (playerScores.ContainsKey(clientId)) { playerScores[clientId] += points; }
        else { playerScores.Add(clientId, points); }

        if (lastMatchPoints.ContainsKey(clientId)) { lastMatchPoints[clientId] += points; }
        else { lastMatchPoints.Add(clientId, points); }
    }

    public int GetPlayerScore(ulong clientId)
    {
        if (playerScores.TryGetValue(clientId, out int score)) return score;
        return 0;
    }

    public int GetLastMatchPoints(ulong clientId)
    {
        if (lastMatchPoints.TryGetValue(clientId, out int score)) return score;
        return 0;
    }

    [ClientRpc]
    private void PrepareReturnToLobbyClientRpc()
    {
        isReturningToLobby = true;
    }

    public void LoadNextMinigame()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        if (!showingScoreboard)
        {
            showingScoreboard = true;
            NetworkManager.Singleton.SceneManager.LoadScene("ScoreboardScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            showingScoreboard = false;
            currentMapIndex++;
            lastMatchPoints.Clear();

            if (currentMapIndex < gamePlaylist.Count)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(gamePlaylist[currentMapIndex], UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else
            {
                isReturningToLobby = true; 
                PrepareReturnToLobbyClientRpc(); 
                NetworkManager.Singleton.SceneManager.LoadScene("MainMenu", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }
    }

    private void OnSceneLoadCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName != "MainMenu" && sceneName != "ScoreboardScene")
        {
            if (gamePlayerPrefab == null) 
            { 
                Debug.LogError("[RelayManager] HATA: gamePlayerPrefab atanmamış! Karakterler doğamaz."); 
                return; 
            }
            LobbyPlayer[] lobbyPlayers = FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None);
        
            foreach (LobbyPlayer lobbyPlayer in lobbyPlayers)
            {
                // 1. Koordinatını yerin altına (-100) çekiyoruz
                lobbyPlayer.transform.position = new Vector3(0f, -100f, 0f);
            }

            foreach (ulong clientId in clientsCompleted)
            {
                int slot = GetPlayerSlot(clientId);

                Vector3 spawnPosition = SpawnPointManager.Instance != null
                ? SpawnPointManager.Instance.GetSpawnPosition(slot) 
                : new Vector3(slot * 2.5f, 1f, 0f); 

                GameObject newPlayer = Instantiate(gamePlayerPrefab, spawnPosition, Quaternion.identity);
                newPlayer.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);

                if (savedPlayerData.TryGetValue(clientId, out var data))
                {
                    PlayerController controller = newPlayer.GetComponent<PlayerController>();
                    if (controller != null)
                    {
                        controller.networkPlayerName.Value = data.name;
                        controller.networkPlayerColor.Value = data.color; 
                    }
                }
            }
        }
    }

    public void LeaveLobby()
    {
        isIntentionallyLeaving = true; 

        if (NetworkManager.Singleton != null) 
        {
            NetworkManager.Singleton.Shutdown();
        }

        isGameInProgress = false;
        gamePlaylist.Clear();
        playerScores.Clear(); 
        lastMatchPoints.Clear();
        clientSlots.Clear(); 
        
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu")
        {
            ResetUIToMainMenu();
            isIntentionallyLeaving = false; 
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            isIntentionallyLeaving = false;
        }
    }
}