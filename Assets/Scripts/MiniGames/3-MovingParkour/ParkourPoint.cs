using Unity.Netcode;
using UnityEngine;

// NetworkBehaviour yerine MonoBehaviour yaptık!
public class ParkourPoint : MonoBehaviour 
{
    [Header("Nokta Ayarları")]
    [Tooltip("Bu noktanın benzersiz kimliği (1. Adacık için 1, 2. için 2 yapın)")]
    public int pointId = 1;

    private void OnTriggerEnter(Collider other)
    {
        // IsServer yerine NetworkManager üzerinden kontrol ediyoruz
        if (!NetworkManager.Singleton.IsServer) return;

        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && MinigameParkourManager.Instance != null)
            {
                // Sunucuya "Bu oyuncu bu ID'li noktaya bastı" diyoruz
                MinigameParkourManager.Instance.PlayerCollectedPoint(netObj.OwnerClientId, pointId);
            }
        }
    }
}