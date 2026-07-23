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
            int[] oldScores = new int[connectedIds.Length];
            int[] newScores = new int[connectedIds.Length];
            for (int i = 0; i < connectedIds.Length; i++)
            {
                int currentTotal = RelayManager.Instance.GetPlayerScore(connectedIds[i]);
                int gained = RelayManager.Instance.GetLastMatchPoints(connectedIds[i]);
                newScores[i] = currentTotal;
                oldScores[i] = currentTotal - gained;
            }

            // Playlist durumunu hesapla
            int currentMatch = RelayManager.Instance.GetCurrentMapIndex() + 1;
            int totalMatches = RelayManager.Instance.GetPlaylistCount();

            // 3. SİHİRLİ DOKUNUŞ: Tüm verilere misafirlere fırlatıyoruz!
            SyncScoreboardClientRpc(connectedIds, oldScores, newScores, currentMatch, totalMatches);
        }
        else
        {
            // Misafir oyuncu ayarları
            if (hostNextButton != null) hostNextButton.SetActive(false);
            if (statusText != null) statusText.text = "Waiting for the host to start the next game...";
        }
    }

    [ClientRpc]
    private void SyncScoreboardClientRpc(ulong[] playerIds, int[] oldScores, int[] newScores, int currentMatch, int totalMatches)
    {
        StartCoroutine(AnimateScoreboard(playerIds, oldScores, newScores, currentMatch, totalMatches));
    }

    private System.Collections.IEnumerator AnimateScoreboard(ulong[] playerIds, int[] oldScores, int[] newScores, int currentMatch, int totalMatches)
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
                matchInfoText.text = $"Current Match: {currentMatch} / {totalMatches}\n<color=green>Remaining Matches: {remainingGames}</color>";
            }
        }

        // B. Skor Tablosunu Animasyonlu Oluşturma
        float duration = 2.0f; // Animasyon 2 saniye sürecek
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            List<(ulong id, int score)> currentData = new List<(ulong, int)>();
            for (int i = 0; i < playerIds.Length; i++)
            {
                int val = Mathf.RoundToInt(Mathf.Lerp(oldScores[i], newScores[i], t));
                currentData.Add((playerIds[i], val));
            }

            BuildScoreboardText(currentData, currentMatch, totalMatches);
            yield return null; // Bir sonraki frame'e geç
        }

        // Animasyon bittiğinde kesin final skorlarını yazdır
        List<(ulong id, int score)> finalData = new List<(ulong, int)>();
        for (int i = 0; i < playerIds.Length; i++)
        {
            finalData.Add((playerIds[i], newScores[i]));
        }
        BuildScoreboardText(finalData, currentMatch, totalMatches);
    }

    private void BuildScoreboardText(List<(ulong id, int score)> playerDataList, int currentMatch, int totalMatches)
    {
        if (scoreboardText == null || RelayManager.Instance == null) return;

        bool isFinalScoreboard = currentMatch >= totalMatches;

        // Başlık Yazısı: Finalse Şampiyon, değilse Normal Durum
        if (isFinalScoreboard)
        {
            scoreboardText.text = "<size=130%><color=yellow>THE CHAMPION</color>\n\n";
        }
        else
        {
            scoreboardText.text = "<color=yellow>CURRENT SCORE STATUS</color>\n\n";
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

            // 🚀 ŞAMPİYON VURGUSU
            if (rank == 1 && isFinalScoreboard)
            {
                // <size=x%> etiketi sadece bu satırın fontunu diğerlerine göre devasa yapar
                scoreboardText.text += $"<size=150%><color=#{colorHex}>{pName}</color>  {player.score} Pts</size>\n\n";
            }
            else
            {
                scoreboardText.text += $"{rank}. <color=#{colorHex}>{pName}</color>  {player.score} Points\n";
            }
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