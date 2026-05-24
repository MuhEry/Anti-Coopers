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
            // Sunucu, ağa bağlanan tüm istemcilere skor tablosunu hazırlayıp RPC ile yollar
            BuildAndSendScoreboardClientRpc();
            StartCoroutine(WaitAndLoadNextGame());
        }
    }

    [ClientRpc]
    private void BuildAndSendScoreboardClientRpc()
    {
        if (scoreboardText == null) return;

        scoreboardText.text = "=== MAÇ SONU SKORLARI ===\n\n";

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (RelayManager.Instance != null)
            {
                int currentScore = RelayManager.Instance.GetPlayerScore(client.ClientId);
                
                // Oyuncunun lobi verilerinden gerçek Nickname ve Renk bilgisini alıyoruz
                RelayManager.Instance.GetMySavedData(client.ClientId, out string playerNick, out Color32 pColor);
                
                // Rengi Hex koduna çeviriyoruz (Örn: #FF0000)
                string hexColor = ColorUtility.ToHtmlStringRGB(pColor);

                // YENİ: İsmi kendi renginde, skoru düz yazacak şekilde Rich Text hazırlıyoruz
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