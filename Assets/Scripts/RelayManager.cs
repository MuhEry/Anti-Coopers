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
    private bool showingScoreboard = false;
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
    public bool GetMySavedColor(ulong clientId, out Color32 color)
    {
        if (savedPlayerData.TryGetValue(clientId, out var data))
        {
            color = data.color; // Otomatik implicit cast olur
            return true;
        }
        color = new Color32(255, 255, 255, 255);
        return false;
    }

    public void ChangeMyColor(string colorHex)
    {
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            if (ColorUtility.TryParseHtmlString(colorHex, out Color chosenColor))
            {
                // Color'ı doğrudan Color32'ye çevirerek tam sayı doğruluğu sağlıyoruz
                Color32 strictColor = chosenColor; 
                NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<LobbyPlayer>().SelectColorServerRpc(strictColor);
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
    // Her oyuncunun ulong türündeki ID'sine karşılık gelen toplam skor tablosu
    private Dictionary<ulong, int> playerScores = new Dictionary<ulong, int>();

    // Oyun içi yöneticilerin oyunculara puan ekleyebilmesi için fonksiyon
    public void AddScore(ulong clientId, int points)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (playerScores.ContainsKey(clientId))
        {
            playerScores[clientId] += points;
        }
        else
        {
            playerScores.Add(clientId, points);
        }
        Debug.Log($"Oyuncu {clientId} için puan güncellendi. Yeni Skor: {playerScores[clientId]}");
    }

    // Skorları çekmek için yardımcı fonksiyon
    public int GetPlayerScore(ulong clientId)
    {
        if (playerScores.TryGetValue(clientId, out int score)) return score;
        return 0;
    }
    // Sıradaki mini oyuna geçişi tetikleyecek fonksiyon (Mini oyun bitince çağrılacak)
    public void LoadNextMinigame()
{
    if (!NetworkManager.Singleton.IsHost) return;

    // Eğer şu an mini oyundan çıktıysak, önce skor ekranına uğra
    if (!showingScoreboard)
    {
        showingScoreboard = true;
        NetworkManager.Singleton.SceneManager.LoadScene("ScoreboardScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
    else
    {
        // Skor ekranından geliyorsak index'i arttır ve sıradaki asıl haritayı yükle
        showingScoreboard = false;
        currentMapIndex++;

        if (currentMapIndex < gamePlaylist.Count)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gamePlaylist[currentMapIndex], UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            Debug.Log("TÜM PLAYLIST BİTTİ! Büyük şampiyon ilan ediliyor...");
            // Buraya oyun tamamen bittiğinde açılacak şampiyonluk sahnesini koyabiliriz
        }
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