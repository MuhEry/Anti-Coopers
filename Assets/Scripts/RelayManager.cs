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
    private bool isIntentionallyLeaving = false; 

    private List<string> gamePlaylist = new List<string>();
    private bool showingScoreboard = false;
    private int currentMapIndex = 0;
    private Dictionary<ulong, int> playerScores = new Dictionary<ulong, int>();
    private bool isReturningToLobby = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 🚀 BÜYÜK DÜZELTME 1: UI Çalınma Koruması ApprovalCheck
            // Eğer yanlışlıkla MiniGame sahnesine bir RelayManager koyduysan, ana menüyü bozmasını engeller!
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

                if (Instance.isReturningToLobby)
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
                // Olası Event sızıntılarını (Leak) önlemek için önce çıkarıp sonra ekliyoruz
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

        UpdatePlaylistUI();
        UpdatePlayerListUI();
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (isGameInProgress)
        {
            response.Approved = false;
            response.Reason = "Game has already started!";
            return;
        }

        if (clientSlots.Count >= 8)
        {
            response.Approved = false;
            response.Reason = "Room is currently at full capacity (8/8)!";
            return;
        }

        response.Approved = true;
        
        // 🚀 BÜYÜK DÜZELTME 2: Çifte Doğurma Çöküşünü Engelle!
        // Karakteri manuel (OnSceneLoadCompleted içinde) doğurduğumuz için Netcode'un otomatik doğurmasını KAPATIYORUZ.
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
                if (string.IsNullOrEmpty(reason)) reason = "Failed to connect to the room. The room may be closed or the code is invalid.";
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
        SetNicknameBeforeConnect();
        await SafeShutdownNetwork(); 

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(7); 
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            if (transport != null)
            {
                string connectionType = "dtls";
#if UNITY_WEBGL
                transport.UseWebSockets = true;
                connectionType = "wss";
#endif
                RelayServerData relayServerData = new RelayServerData(allocation, connectionType);
                transport.SetRelayServerData(relayServerData);
                
                isGameInProgress = false; 
                clientSlots.Clear(); 
                
                NetworkManager.Singleton.StartHost();
                
                // 🚀 BÜYÜK DÜZELTME 3: Sızıntı engelleme
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;

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
            }
        }
        catch (RelayServiceException e) { Debug.LogError("Relay hatası: " + e.Message); }
    }

    public async void JoinRelay() 
    {
        SetNicknameBeforeConnect();
        await SafeShutdownNetwork(); 

        try
        {
            if (codeInputField == null) return;
            string joinCode = codeInputField.text; 
            
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                ShowError("Invalid code!");
                return;
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            if (transport != null)
            {
                string connectionType = "dtls";
#if UNITY_WEBGL
                transport.UseWebSockets = true;
                connectionType = "wss";
#endif
                RelayServerData relayServerData = new RelayServerData(joinAllocation, connectionType);
                transport.SetRelayServerData(relayServerData);
                
                ShowError("Connecting to the room, please wait...");
                NetworkManager.Singleton.StartClient();
                if (lobbyCodeText != null) lobbyCodeText.text = "ROOM CODE: " + joinCode;
            }
        }
        catch (System.Exception) 
        { 
            ShowError("Invalid code! Room not found or your internet connection is down."); 
        }
    }

    public void UpdatePlayerListUI()
    {
        if (playerListText == null || lobbyPanel == null || !lobbyPanel.activeInHierarchy) return;

        LobbyPlayer[] players = FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None);
        bool allReady = true;
        playerListText.text = "PLAYERS IN THE ROOM:\n";
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
            // Çift tıklanıp iki kez sahne yüklenmesini engeller
            if (isGameInProgress) return; 

            isGameInProgress = true; 
            showingScoreboard = false; 
            currentMapIndex = 0;

            // 🚀 BÜYÜK DÜZELTME 4: Lobi UI Gizlemesi! (Yazıların üst üste binmesini çözer)
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
    }

    public int GetPlayerScore(ulong clientId)
    {
        if (playerScores.TryGetValue(clientId, out int score)) return score;
        return 0;
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

            if (currentMapIndex < gamePlaylist.Count)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(gamePlaylist[currentMapIndex], UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else
            {
                isReturningToLobby = true;
                NetworkManager.Singleton.SceneManager.LoadScene("MainMenu", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }
    }

    private void OnSceneLoadCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        // 🚀 DÜZELTME: Artık sahne isimlerine takılmıyoruz. 
        // Eğer sahne "MainMenu" veya "ScoreboardScene" DEĞİLSE, kesin oyun haritasındayızdır, karakteri doğur!
        if (sceneName != "MainMenu" && sceneName != "ScoreboardScene")
        {
            // Güvenlik Duvarı: Prefab yoksa çökmesin
            if (gamePlayerPrefab == null) 
            { 
                Debug.LogError("[RelayManager] ERROR: gamePlayerPrefab atanmamış! Karakterler doğamaz."); 
                return; 
            }

            foreach (ulong clientId in clientsCompleted)
            {
                int slot = GetPlayerSlot(clientId);

                // 🎯 SPAWN POINT MANAGER DEVREDE:
                // Sahnede SpawnPointManager varsa oradaki noktaları alır, yoksa varsayılan yan yana dizer.
                Vector3 spawnPosition = SpawnPointManager.Instance != null
                ? SpawnPointManager.Instance.GetSpawnPosition(slot) 
                : new Vector3(slot * 2.5f, 1f, 0f); 

                GameObject newPlayer = Instantiate(gamePlayerPrefab, spawnPosition, Quaternion.identity);
                
                // Karakteri ağda fiziksel "PlayerObject" olarak yetkilendiriyoruz
                newPlayer.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);

                // Lobi oyuncusundaki renk ve isimleri asıl fiziksel karaktere kopyalıyoruz
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