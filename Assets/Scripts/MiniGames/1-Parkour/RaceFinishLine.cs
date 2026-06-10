using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class RaceFinishLine : MonoBehaviour
{
    private HashSet<ulong> finishedClients = new HashSet<ulong>();

    private void OnTriggerEnter(Collider other)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (!other.CompareTag("Player")) return;

        NetworkObject netObj = other.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            ulong clientId = netObj.OwnerClientId;

            // Bu oyuncu daha önce geçmediyse işle
            if (finishedClients.Contains(clientId)) return;

            finishedClients.Add(clientId);

            // MUAZZAM MİMARİ: Hangi haritada olursak olalım aktif menajere haber uçar!
            if (BaseMinigameManager.ActiveMinigame != null)
            {
                BaseMinigameManager.ActiveMinigame.PlayerFinished(clientId);
            }
        }
    }
}