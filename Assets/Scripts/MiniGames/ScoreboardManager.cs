using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;

public class ScoreboardManager : NetworkBehaviour
{
    [SerializeField] private TMP_Text scoreboardText;
    [SerializeField] private float waitTimeBeforeNextGame = 5f; 

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Bağlı oyuncu sayısına göre dizilerimizi hazırlıyoruz
            int clientCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            ulong[] clientIds = new ulong[clientCount];
            int[] scores = new int[clientCount];

            int index = 0;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                clientIds[index] = client.ClientId;
                // Skorları doğrudan sunucudaki RelayManager hafızasından çekiyoruz
                scores[index] = RelayManager.Instance != null ? RelayManager.Instance.GetPlayerScore(client.ClientId) : 0;
                index++;
            }

            // Hazırlanan net veriyi ağdaki tüm istemcilere parametre olarak gönderiyoruz
            BuildAndSendScoreboardClientRpc(clientIds, scores);
            StartCoroutine(WaitAndLoadNextGame());
        }
    }

    [ClientRpc]
    private void BuildAndSendScoreboardClientRpc(ulong[] clientIds, int[] scores)
    {
        if (scoreboardText == null) return;

        scoreboardText.text = "=== MAÇ SONU SKORLARI ===\n\n";

        // Sunucunun gönderdiği senkronize diziler üzerinden döngü kuruyoruz
        for (int i = 0; i < clientIds.Length; i++)
        {
            ulong clientId = clientIds[i];
            int currentScore = scores[i];

            if (RelayManager.Instance != null)
            {
                // Oyuncunun lobi verilerinden Nickname ve Renk bilgisini alıyoruz
                RelayManager.Instance.GetMySavedData(clientId, out string playerNick, out Color32 pColor);
                
                // Rengi Hex koduna çeviriyoruz
                string hexColor = ColorUtility.ToHtmlStringRGB(pColor);

                // İsmi kendi renginde, skoru düz yazacak şekilde metne ekliyoruz
                scoreboardText.text += $"<color=#{hexColor}>{playerNick}</color> -> Toplam Skor: {currentScore} Puan\n";
            }
        }
    }

    private IEnumerator WaitAndLoadNextGame()
    {
        yield return new WaitForSeconds(waitTimeBeforeNextGame);
        
        if (RelayManager.Instance != null)
        {
            RelayManager.Instance.LoadNextMinigame();
        }
    }
}