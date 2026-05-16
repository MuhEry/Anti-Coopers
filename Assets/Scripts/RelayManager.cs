using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using TMPro;
using System.Threading.Tasks; // Task geciktirmeleri için şart

public class RelayManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField codeInputField; 

    async void Start()
    {
        try
        {
            // 1. BİZİM ÇÖZÜM: Aynı bilgisayarda çakışmayı önlemek için benzersiz profil üretiyoruz
            InitializationOptions options = new InitializationOptions();
            string uniqueProfileName = "Player_" + Random.Range(1000, 9999);
            options.SetProfile(uniqueProfileName);

            await UnityServices.InitializeAsync(options);
            
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[{uniqueProfileName}] Bulut Sistemine Giriş Yaptı. ID: " + AuthenticationService.Instance.PlayerId);
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
        // CLAUDE'UN ÖNERİSİ: Eğer eski bir otomatik bağlantı varsa durdur ve 500ms bekle
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Eski Host bağlantısı temizleniyor...");
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
        // CLAUDE'UN ÖNERİSİ: Eğer otomatik başlayan client varsa durdur ve 500ms bekle
        if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Eski otomatik Client bağlantısı temizleniyor...");
            NetworkManager.Singleton.Shutdown();
            await Task.Delay(500); // Ağın tamamen kapanması için gereken altın süre
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
                Debug.Log("Tebrikler! Odaya Başarıyla Katılabilindi.");
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Odaya katılırken Relay hatası: " + e.Message);
        }
    }
}