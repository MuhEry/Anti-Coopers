using Unity.Netcode;
using UnityEngine;

public class ParkourPoint : NetworkBehaviour
{
    [Header("Nokta Ayarları")]
    [Tooltip("Bu noktanın benzersiz kimliği (1. Adacık için 1, 2. için 2 yapın)")]
    public int pointId = 1;

    /*[Tooltip("Puan alındığında çıkacak görsel efekt (Opsiyonel)")]
    [SerializeField] private GameObject collectEffectPrefab;*/

    private void OnTriggerEnter(Collider other)
    {
        // Temas işlemlerini sadece ve sadece Sunucu (Server) hesaplar, hile yapılamaz
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && MinigameParkourManager.Instance != null)
            {
                // Sunucuya "Bu oyuncu bu ID'li noktaya bastı" diyoruz
                bool isCollected = MinigameParkourManager.Instance.PlayerCollectedPoint(netObj.OwnerClientId, pointId);

                // Eğer oyuncu bu puanı İLK DEFA aldıysa (isCollected true dönerse) herkese efekt göster
                /*if (isCollected)
                {
                    ShowCollectEffectClientRpc(transform.position);
                }*/
            }
        }
    }

    /*[ClientRpc]
    private void ShowCollectEffectClientRpc(Vector3 position)
    {
        // Eğer bir toplama efekti atadıysan burada çalışır (Low-poly yıldız patlaması vb.)
        if (collectEffectPrefab != null)
        {
            Instantiate(collectEffectPrefab, position, Quaternion.identity);
        }
        
        // İstersen burada ufak bir "Ting!" ses efekti de çaldırabilirsin
    }*/
}