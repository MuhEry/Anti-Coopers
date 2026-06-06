using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class RaceFinishLine : MonoBehaviour
{
    // Her oyuncu için ayrı ayrı takip — tek triggered tüm oyuncuları bloklıyordu
    private HashSet<ulong> finishedClients = new HashSet<ulong>();

    private void OnTriggerEnter(Collider other)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (!other.CompareTag("Player")) return;

        NetworkObject netObj = other.GetComponent<NetworkObject>();
        if (netObj == null) return;

        ulong clientId = netObj.OwnerClientId;

        // Bu oyuncu daha önce geçmediyse işle
        if (finishedClients.Contains(clientId)) return;

        finishedClients.Add(clientId);
        MinigameRaceManager.Instance.PlayerFinished(clientId);
    }
}