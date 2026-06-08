using Unity.Netcode;
using UnityEngine;

public class CrownTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null && MinigameCrownManager.Instance != null)
            {
                MinigameCrownManager.Instance.InitialPickup(netObj.OwnerClientId);
                
                // DOĞRU YÖNTEM: Coini kapatmak yerine sadece Collider'ını kapatıyoruz
                if (TryGetComponent<Collider>(out var col))
                {
                    col.enabled = false;
                }
            }
        }
    }
}