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

    [HideInInspector] public string LocalProfileName;
    private Dictionary<ulong, (string name, Color32 color)> savedPlayerData = new Dictionary<ulong, (string, Color32)>();
    
    private Dictionary<ulong, int> clientSlots = new Dictionary<ulong, int>();
    private bool isGameInProgress = false; 

    private List<string> gamePlaylist = new List<string>();
    private bool showingScoreboard = false;
    private int currentMapIndex = 0;
    private Dictionary<ulong, int> playerScores = new Dictionary<ulong, int>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    async void Start()
    {
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        hostMapSelectionPanel.SetActive(false);
        if (startGameButton != null) startGameButton.gameObject.SetActive(false); 
        if (errorText != null) errorText.gameObject.SetActive(false);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            
            // YENİ: Başarılı bağlantı olayını dinliyoruz
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }

        try
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
        catch (System.Exception e) { Debug.LogError("Servis hatası: " + e.Message); }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (isGameInProgress)
        {
            response.Approved = false;
            response.Reason = "Oyun zaten başladı!";
            return;
        }

        if (clientSlots.Count >= 8)
        {
            response.Approved = false;
            response.Reason = "Oda şu an tam kapasite (8/8) dolu!";
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
    }

    // --- YENİ: BAĞLANTI ONAYLANDIĞINDA PANEL GEÇİŞİ YAP ---
    private void OnClientConnected(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Sadece katılan istemciler (Client) için paneli açıyoruz
            // Host zaten CreateRelay fonksiyonunda paneli kendi açıyor
            if (!NetworkManager.Singleton.IsHost)
            {
                HideError(); // "Bağlanılıyor..." yazısını temizle
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
        if (NetworkManager.Singleton.IsServer && clientSlots.ContainsKey(clientId))
        {
            clientSlots.Remove(clientId);
        }

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            string reason = NetworkManager.Singleton.DisconnectReason;
            if (string.IsNullOrEmpty(reason))
            {
                reason = "Odaya bağlanılamadı. Oda kapanmış veya kod geçersiz.";
            }
            ShowError(reason);
            LeaveLobby();
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
            errorText.text = $"</color> {message}";
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
            NetworkManager.Singleton.Shutdown();
            while (NetworkManager.Singleton.IsListening)
            {
                await Task.Delay(50); 
            }
            await Task.Delay(200);
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
                transport.SetRelayServerData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );
                
                isGameInProgress = false; 
                clientSlots.Clear(); 
                
                NetworkManager.Singleton.StartHost();
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;

                lobbyCodeText.text = "ODA KODU: " + joinCode;
                mainMenuPanel.SetActive(false);
                lobbyPanel.SetActive(true);
                hostMapSelectionPanel.SetActive(true);
                
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
            string joinCode = codeInputField.text; 
            
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                ShowError("Geçersiz kod");
                return;
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            if (transport != null)
            {
                transport.SetRelayServerData(
                    joinAllocation.RelayServer.IpV4,
                    (ushort)joinAllocation.RelayServer.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData
                );
                
                // DÜZELTİLDİ: UI panellerini buradan kaldırdık. Sadece istek atıp bekliyoruz.
                ShowError("Odaya bağlanılıyor, lütfen bekleyin...");
                NetworkManager.Singleton.StartClient();
                lobbyCodeText.text = "ODA KODU: " + joinCode;
            }
        }
        catch (System.Exception) 
        { 
            ShowError("Geçersiz kod"); 
        }
    }

    public void UpdatePlayerListUI()
    {
        if (playerListText == null || !lobbyPanel.activeInHierarchy) return;

        LobbyPlayer[] players = FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None);
        bool allReady = true;
        playerListText.text = "ODADAKİ OYUNCULAR:\n";
        savedPlayerData.Clear();

        foreach (LobbyPlayer player in players)
        {
            string readyStatus = player.isReady.Value ? "<color=green>[HAZIR]</color>" : "<color=red>[BEKLİYOR]</color>";
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
        playlistText.text = "OYNATMA LİSTESİ:\n";
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
            isGameInProgress = true; 
            showingScoreboard = false; 
            currentMapIndex = 0;
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
                Debug.Log("TÜM PLAYLIST BİTTİ!");
                LeaveLobby(); 
            }
        }
    }

    private void OnSceneLoadCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName.StartsWith("MiniGame_") || sceneName == "GameScene")
        {
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
                    controller.networkPlayerName.Value = data.name;
                    controller.networkPlayerColor.Value = data.color; 
                }
            }
        }
    }

    public void LeaveLobby()
    {
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        isGameInProgress = false;
        gamePlaylist.Clear();
        playerScores.Clear(); 
        clientSlots.Clear(); 
        lobbyPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}