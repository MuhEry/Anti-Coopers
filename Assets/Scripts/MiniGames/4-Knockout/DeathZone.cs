using Unity.Netcode;
using UnityEngine;

public class DeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            
            // MUAZZAM MİMARİ: Hangi haritada olursak olalım aktif menajere haber uçar!
            if (netObj != null && BaseMinigameManager.ActiveMinigame != null)
            {
                BaseMinigameManager.ActiveMinigame.PlayerEliminated(netObj.OwnerClientId);
            }
        }
    }
}