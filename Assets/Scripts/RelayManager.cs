using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using TMPro;
using System.Threading.Tasks;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    [Header("UI Panelleri")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("UI Metin Elemanları")]
    [SerializeField] private UnityEngine.UI.Button startGameButton;
    [SerializeField] private TMP_InputField codeInputField; 
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private TMP_Text playerListText;

    [HideInInspector] public string LocalProfileName;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    async void Start()
    {
        // Oyun başında panellerin doğru açılmasını sağlıyoruz
        mainMenuPanel.SetActive(true);
        lobbyPanel.SetActive(false);

        try
        {
            InitializationOptions options = new InitializationOptions();
            LocalProfileName = "Player_" + Random.Range(1000, 9999);
            options.SetProfile(LocalProfileName);

            await UnityServices.InitializeAsync(options);
            
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[{LocalProfileName}] Bulut Sistemine Giriş Yaptı.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Servis başlatma hatası: " + e.Message);
        }
    }

    // ODA KURMA (HOST)
    public async void CreateRelay()
    {
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.Shutdown();
            await Task.Delay(500); 
        }

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            Debug.Log("Oda Başarıyla Kuruldu! ODA KODUNUZ: " + joinCode);

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
                
                NetworkManager.Singleton.StartHost();

                // PANEL GEÇİŞLERİ VE KODU YAZDIRMA
                lobbyCodeText.text = "ODA KODU: " + joinCode;
                mainMenuPanel.SetActive(false);
                lobbyPanel.SetActive(true);
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Relay kurma hatası: " + e.Message);
        }
    }

    // ODAYA KATILMA (CLIENT)
    public async void JoinRelay() 
    {
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.Shutdown();
            await Task.Delay(500);
        }

        try
        {
            string joinCode = codeInputField.text; 
            Debug.Log("Odaya katılınmaya çalışılıyor, Kod: " + joinCode);
            
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
                
                NetworkManager.Singleton.StartClient();

                // PANEL GEÇİŞLERİ VE KODU YAZDIRMA
                lobbyCodeText.text = "ODA KODU: " + joinCode;
                mainMenuPanel.SetActive(false);
                lobbyPanel.SetActive(true);
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Odaya katılırken Relay hatası: " + e.Message);
        }
    }

    // Oyuncu listesini ekranda tazeleyen fonksiyon (Unity 6 optimize uyumlu)
    public void UpdatePlayerListUI()
    {
        LobbyPlayer[] players = FindObjectsByType<LobbyPlayer>(FindObjectsSortMode.None);
        bool allReady = true;
        
        playerListText.text = "ODADAKİ OYUNCULAR:\n";
        foreach (LobbyPlayer player in players)
        {
            string readyStatus = player.isReady.Value ? "<color=green>[HAZIR]</color>" : "<color=red>[BEKLİYOR]</color>";
            playerListText.text += $"- {player.playerName.Value} {readyStatus}\n";
            
            if (!player.isReady.Value) allReady = false;
        }

        // Sadece Host "Başlat" butonunu yönetebilir
        if (NetworkManager.Singleton.IsHost)
        {
            // Herkes hazırsa ve en az 2 kişi varsa (test için 1 yapabilirsin) başlat butonu aktif olur
            startGameButton.interactable = allReady && players.Length >= 1; 
        }
    }
    public void OnReadyClicked()
    {
        NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<LobbyPlayer>().ToggleReadyServerRpc();
    }

    public void OnStartGameClicked()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            // Ağ üzerinden sahne geçişi (Tüm oyuncuları aynı anda oyun sahnesine taşır)
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}