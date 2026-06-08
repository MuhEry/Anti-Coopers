using Unity.Netcode;
using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Tetiklenmeyi sadece Server hesaplar, hile olmaz
        if (!NetworkManager.Singleton.IsServer) return;

        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                if (MinigameKnockoutManager.Instance != null)
                {
                    // Yöneticimize bu oyuncunun elendiğini haber ver
                    MinigameKnockoutManager.Instance.PlayerEliminated(netObj.OwnerClientId);
                }
            }
        }
    }
}