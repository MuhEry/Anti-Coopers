using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;

public class ScoreboardManager : NetworkBehaviour
{
    [SerializeField] private TMP_Text scoreboardText;
    [SerializeField] private float waitTimeBeforeNextGame = 4f; // Skor ekranında kaç saniye beklenecek?

    private void Start()
    {
        if (scoreboardText == null) return;

        scoreboardText.text = "=== MAÇ SONU SKORLARI ===\n\n";

        // Ağdaki tüm bağlı oyuncuları dönüp RelayManager'daki skorlarını ekrana yazıyoruz
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (RelayManager.Instance != null)
            {
                int currentScore = RelayManager.Instance.GetPlayerScore(client.ClientId);
                
                // Eğer oyuncunun ismini de çekmek istersek RelayManager hafızasından eşleştirebiliriz
                scoreboardText.text += $"Oyuncu (ID: {client.ClientId}) -> Toplam Skor: {currentScore} Puan\n";
            }
        }

        // Sadece Host olan taraf zamanlayıcıyı başlatsın ve sıradaki oyuna geçirsin
        if (IsServer)
        {
            StartCoroutine(WaitAndLoadNextGame());
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