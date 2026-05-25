using Unity.Netcode;
using UnityEngine;

public class RaceFinishLine : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Sadece sunucu tetiklemeleri kontrol etmeli ve çarpan obje oyuncu olmalı
        if (!NetworkManager.Singleton.IsServer) return;

        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                // Yarış yöneticisine hangi ClientID'nin bitirdiğini haber ver
                MinigameRaceManager.Instance.PlayerFinished(netObj.OwnerClientId);
                
                // Oyuncunun bitiş alanında kalıp sürekli puan üretmesini önlemek için objeyi deaktif edebiliriz
                // veya oyuncu scriptine "bitti" bayrağı ekleyebiliriz. Şimdilik engellemek için:
                other.gameObject.SetActive(false); 
            }
        }
    }
}