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
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private UnityEngine.UI.Button startGameButton;
    [SerializeField] private TMP_Dropdown mapDropdown; 
    [SerializeField] private TMP_Text playlistText; // YENİ: Seçilen haritaların sırasını gösterecek metin

    [Header("Oyun İçi Doğum Ayarları")]
    [SerializeField] private GameObject gamePlayerPrefab; 

    [HideInInspector] public string LocalProfileName;
    private Dictionary<ulong, (string name, Color color)> savedPlayerData = new Dictionary<ulong, (string, Color)>();
    
    // YENİ: Seçilen mini oyunların isim sıralamasını tutan liste (Playlist)
    private List<string> gamePlaylist = new List<string>();
    private int currentMapIndex = 0;

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

    public async void CreateRelay()
    {
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        { NetworkManager.Singleton.Shutdown(); await Task.Delay(500); }

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            if (transport != null)
            {
                transport.SetRelayServerData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData);
                NetworkManager.Singleton.StartHost();
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;

                lobbyCodeText.text = "ODA KODU: " + joinCode;
                mainMenuPanel.SetActive(false);
                lobbyPanel.SetActive(true);
                hostMapSelectionPanel.SetActive(true);
                UpdatePlaylistUI();
            }
        }
        catch (RelayServiceException e) { Debug.LogError("Relay hatası: " + e.Message); }
    }

    public async void JoinRelay() 
    {
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        { NetworkManager.Singleton.Shutdown(); await Task.Delay(500); }

        try
        {
            string joinCode = codeInputField.text; 
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            
            if (transport != null)
            {
                transport.SetRelayServerData(joinAllocation.RelayServer.IpV4, (ushort)joinAllocation.RelayServer.Port, joinAllocation.AllocationIdBytes, joinAllocation.Key, joinAllocation.ConnectionData, joinAllocation.HostConnectionData);
                NetworkManager.Singleton.StartClient();

                lobbyCodeText.text = "ODA KODU: " + joinCode;
                mainMenuPanel.SetActive(false);
                lobbyPanel.SetActive(true);
                hostMapSelectionPanel.SetActive(false);
            }
        }
        catch (RelayServiceException e) { Debug.LogError("Relay hatası: " + e.Message); }
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

        if (NetworkManager.Singleton.IsHost)
        {
            // OYUNU BAŞLATMA ŞARTI: Herkes hazır olmalı VE admin en az 1 harita seçmiş olmalı
            startGameButton.interactable = allReady && players.Length >= 1 && gamePlaylist.Count > 0; 
        }
    }

    // YENİ: PlayerController'ın lobi rengini çekebilmesi için yardımcı fonksiyon
    public bool GetMySavedColor(ulong clientId, out Color color)
    {
        if (savedPlayerData.TryGetValue(clientId, out var data))
        {
            color = data.color;
            return true;
        }
        color = Color.white;
        return false;
    }

    public void ChangeMyColor(string colorHex)
    {
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            if (ColorUtility.TryParseHtmlString(colorHex, out Color chosenColor))
            {
                NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<LobbyPlayer>().SelectColorServerRpc(chosenColor);
            }
        }
    }

    // YENİ: Admin "Harita Ekle" butonuna basınca çalışacak fonksiyon
    public void AddMapToPlaylist()
    {
        string selectedMap = mapDropdown.options[mapDropdown.value].text;
        gamePlaylist.Add(selectedMap);
        UpdatePlaylistUI();
        UpdatePlayerListUI(); // Oyun başlat butonunu tetiklemek için
    }

    // YENİ: Playlist listesini ekrana yazdıran fonksiyon
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
            currentMapIndex = 0;
            NetworkManager.Singleton.SceneManager.LoadScene(gamePlaylist[currentMapIndex], UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    // Sıradaki mini oyuna geçişi tetikleyecek fonksiyon (Mini oyun bitince çağrılacak)
    public void LoadNextMinigame()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        currentMapIndex++;
        if (currentMapIndex < gamePlaylist.Count)
        {
            // Sıradaki haritayı yükle
            NetworkManager.Singleton.SceneManager.LoadScene(gamePlaylist[currentMapIndex], UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            // Tüm oyunlar bitti! Kazananlar ekranına ışınla
            Debug.Log("Tüm mini oyunlar tamamlandı! Skor ekranına gidiliyor...");
            // NetworkManager.Singleton.SceneManager.LoadScene("EndGameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void OnSceneLoadCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName.StartsWith("MiniGame_") || sceneName == "GameScene")
        {
            int spawnIndex = 0;
            foreach (ulong clientId in clientsCompleted)
            {
                Vector3 spawnPosition = new Vector3(spawnIndex * 2.5f, 1f, 0f);
                GameObject newPlayer = Instantiate(gamePlayerPrefab, spawnPosition, Quaternion.identity);
                newPlayer.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);

                if (savedPlayerData.TryGetValue(clientId, out var data))
                {
                    PlayerController controller = newPlayer.GetComponent<PlayerController>();
                    controller.networkPlayerName.Value = data.name;
                    controller.networkPlayerColor.Value = data.color; // Renk hatası buradan çözüldü!
                }
                spawnIndex++;
            }
        }
    }

    public void LeaveLobby()
    {
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        gamePlaylist.Clear();
        lobbyPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}