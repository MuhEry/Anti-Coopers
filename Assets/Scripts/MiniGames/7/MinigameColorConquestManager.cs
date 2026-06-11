using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MinigameColorConquestManager : BaseMinigameManager
{
    [Header("UI Elemanları")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text statusText;       // YENİ: Süre bitince kazanılan puanı yazacağımız büyük yazı

    [Header("Ayarlar")]
    [SerializeField] private float gameDuration = 45f;

    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        45f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override bool IsGameStarted => gameStarted.Value;

    private PaintTile[] allPaintTiles;
    private bool gameEnded = false;

    private void Start()
    {
        allPaintTiles = FindObjectsByType<PaintTile>(FindObjectsSortMode.None);
        
        if (IsServer)
        {
            timeRemaining.Value = gameDuration;
            StartCoroutine(GameRoutine());
        }

        timeRemaining.OnValueChanged += (oldV, newV) => {
            if (timerText != null) timerText.text = $"SÜRE: {Mathf.CeilToInt(newV)}s";
        };

        if (statusText != null) statusText.gameObject.SetActive(false);
    }

    private IEnumerator GameRoutine()
    {
        gameStarted.Value = false;
        for (int i = 3; i > 0; i--)
        {
            UpdateCountdownClientRpc(i.ToString());
            yield return new WaitForSeconds(1f);
        }
        UpdateCountdownClientRpc("BOYA!");
        yield return new WaitForSeconds(0.8f);
        UpdateCountdownClientRpc("");

        gameStarted.Value = true;

        while (timeRemaining.Value > 0)
        {
            yield return new WaitForSeconds(1f);
            timeRemaining.Value = Mathf.Max(0, timeRemaining.Value - 1f);
        }

        EndGame();
    }

    private void EndGame()
    {
        if (!IsServer || gameEnded) return;
        gameEnded = true;
        gameStarted.Value = false;

        // 1. ADIM: Aktif bağlı oyuncu sayısını alıyoruz
        int playerCount = NetworkManager.Singleton.ConnectedClients.Count;
        if (playerCount == 0) playerCount = 1; // Sıfıra bölünme hatasını (Zero Division) önleme barajı

        // 2. ADIM: Matematiksel modeli kuruyoruz
        // Toplam 400 karo üzerinden kişi başına düşen adil karo miktarını hesapla
        float fairShareOfTiles = 400f / playerCount; 

        Dictionary<ulong, int> playerTileCounts = new Dictionary<ulong, int>();

        foreach (var clientId in NetworkManager.Singleton.ConnectedClients.Keys)
        {
            playerTileCounts[clientId] = 0;
        }

        // Tüm karoları tara
        foreach (var tile in allPaintTiles)
        {
            ulong ownerId = tile.paintedByPlayerId.Value;
            if (ownerId != 9999 && playerTileCounts.ContainsKey(ownerId))
            {
                playerTileCounts[ownerId]++;
            }
        }

        // 3. ADIM: Orantılı Göreli Puan Dağıtımı
        foreach (var kvp in playerTileCounts)
        {
            int rawTiles = kvp.Value;
            
            // SİHİRLİ FORMÜL: (Boyanan Karo / Adil Pay) * 50
            float calculatedScore = (rawTiles / fairShareOfTiles) * 50f;

            // Puanı en yakın tam sayıya (int) yuvarla
            int finalCalculatedScore = Mathf.RoundToInt(calculatedScore);

            // Maksimum veya minimum puan sınırlandırması koymak istersen burayı esnetebilirsin
            finalCalculatedScore = Mathf.Max(0, finalCalculatedScore); // Puan eksiye düşmesin

            // Veritabanına/Skor tablosuna nihai int puanı ekle
            RelayManager.Instance.AddScore(kvp.Key, finalCalculatedScore);

            // Her oyuncunun kendi ekranında görmesi için ClientRpc fırlat
            ShowMyEndScoreClientRpc(kvp.Key, rawTiles, finalCalculatedScore);
        }

        UpdateCountdownClientRpc("SÜRE BİTTİ!");
        StartCoroutine(DelayedEnd());
    }

    [ClientRpc]
    private void UpdateCountdownClientRpc(string text)
    {
        if (countdownText != null) countdownText.text = text;
    }

    // YENİ: Sadece ilgili oyuncunun ekranına özel puan kartı basan fonksiyon
    [ClientRpc]
    private void ShowMyEndScoreClientRpc(ulong targetClientId, int tileCount, int earnedScore)
    {
        // Bu kod ağdaki herkesin bilgisayarında çalışır ama içerideki IF sayesinde 
        // sadece hedef oyuncunun ekranında UI'ı tetikler! (Kişiselleştirilmiş UI)
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                
                // Oyuncuya kaç karo boyadığını ve matematiksel olarak aldığı nihai int puanı gösteriyoruz
                statusText.text = $"<color=yellow>{tileCount} KARO BOYADIN!</color>\n<size=50><color=green>+{earnedScore} Puan</color></size>";
            }
        }
    }

    private IEnumerator DelayedEnd()
    {
        yield return new WaitForSeconds(3f);
        RelayManager.Instance.LoadNextMinigame();
    }
}