using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections.Generic;

public class ScoreboardManager : NetworkBehaviour
{
    [Header("UI Elemanları")]
    [SerializeField] private TMP_Text scoreboardText;   // Sıralamanın yazacağı metin
    [SerializeField] private TMP_Text statusText;       // "Host bekleniyor..." yazısı
    [SerializeField] private TMP_Text matchInfoText;    // YENİ: "Maç: 2 / 5" veya "TÜM OYUNLAR BİTTİ!" yazısı
    [SerializeField] private GameObject hostNextButton; // Sadece hostta açılacak buton

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 1. Host paneli ayarları
            if (hostNextButton != null) hostNextButton.SetActive(true);
            if (statusText != null) statusText.text = "You can start the next game when you're ready.";

            // 2. Ağ verilerini topla (Sadece sunucu görebilir)
            var connectedIds = NetworkManager.Singleton.ConnectedClientsIds.ToArray();
            int[] scores = new int[connectedIds.Length];
            for (int i = 0; i < connectedIds.Length; i++)
            {
                scores[i] = RelayManager.Instance.GetPlayerScore(connectedIds[i]);
            }

            // Playlist durumunu hesapla
            int currentMatch = RelayManager.Instance.GetCurrentMapIndex() + 1;
            int totalMatches = RelayManager.Instance.GetPlaylistCount();

            // 3. SİHİRLİ DOKUNUŞ: Tüm verilere misafirlere fırlatıyoruz!
            SyncScoreboardClientRpc(connectedIds, scores, currentMatch, totalMatches);
        }
        else
        {
            // Misafir oyuncu ayarları
            if (hostNextButton != null) hostNextButton.SetActive(false);
            if (statusText != null) statusText.text = "Waiting for the host to start the next game...";
        }
    }

    [ClientRpc]
    private void SyncScoreboardClientRpc(ulong[] playerIds, int[] scores, int currentMatch, int totalMatches)
    {
        // A. Kaç Oyun Kaldı Bilgisini Güncelleme
        if (matchInfoText != null)
        {
            if (currentMatch >= totalMatches)
            {
                matchInfoText.text = "<color=red><b>ALL GAMES ARE DONE!</b></color>";
                
                // Durum yazılarını oyunun bittiğine göre revize et
                if (statusText != null)
                {
                    statusText.text = IsHost 
                        ? "You can end your game by pressing the button." 
                        : "Waiting for the host to end the game...";
                }
            }
            else
            {
                int remainingGames = totalMatches - currentMatch;
                matchInfoText.text = $"Current Match: {currentMatch} / {totalMatches}  |  <color=green>Remaining Matches: {remainingGames}</color>";
            }
        }

        // B. Skor Tablosunu Oluşturma (Gelen verileri büyükten küçüğe sırala)
        if (scoreboardText == null || RelayManager.Instance == null) return;

        scoreboardText.text = "<color=yellow>CURRENT SCORE STATUS</color>\n\n";

        List<(ulong id, int score)> playerDataList = new List<(ulong, int)>();
        for (int i = 0; i < playerIds.Length; i++)
        {
            playerDataList.Add((playerIds[i], scores[i]));
        }
        
        var sortedPlayers = playerDataList.OrderByDescending(p => p.score).ToList();

        int rank = 1;
        foreach (var player in sortedPlayers)
        {
            string pName = "Player " + player.id;
            string colorHex = "#FFFFFF";

            if (RelayManager.Instance.GetMySavedData(player.id, out string savedName, out Color32 color))
            {
                pName = savedName;
                colorHex = ColorUtility.ToHtmlStringRGB(color);
            }

            scoreboardText.text += $"{rank}. <color=#{colorHex}>{pName}</color>  {player.score} Points\n";
            rank++;
        }
    }

    // Host butona bastığında tetiklenir
    public void OnHostNextButtonClicked()
    {
        if (IsHost)
        {
            if (hostNextButton != null) 
            {
                var btn = hostNextButton.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.interactable = false; // Çift tıklamayı önler
            }
            
            RelayManager.Instance.LoadNextMinigame();
        }
    }
}