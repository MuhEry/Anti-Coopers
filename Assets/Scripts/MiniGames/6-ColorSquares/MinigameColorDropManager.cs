using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MinigameColorDropManager : BaseMinigameManager
{
    public static MinigameColorDropManager Instance { get; private set; }

    [Header("UI Elemanları")]
    [SerializeField] private TMP_Text timerText;        // Üstteki genel süre veya Tur bilgisi (ROUND)
    [SerializeField] private TMP_Text countdownText;    // Ortadaki büyük renk ve geri sayım yazısı
    [SerializeField] private TMP_Text statusText;       // ELENDİN / KAZANDIN yazıları için
    [SerializeField] private GameObject spectatorCamera; // Ölenlerin izleyici kamerası

    [Header("Oyun Ayarları")]
    [SerializeField] private float roundDuration = 4f;  // Oyuncuların rengi bulması için kaç saniyesi var?
    [SerializeField] private int maxRounds = 5;         // Toplam kaç tur sürecek?

    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override bool IsGameStarted => gameStarted.Value;

    // Ekrana basılacak ana renk metinlerini doğrudan İNGİLİZCE yaptık!
    private List<string> colors = new List<string> 
    { 
        "RED", "BLUE", "GREEN", "YELLOW", "ORANGE", "PURPLE", "PINK", "BLACK", "WHITE" 
    };

    // Yazının bürünebileceği rastgele renk havuzu
    private Color32[] textColors = 
    { 
        Color.red, 
        Color.blue, 
        Color.green, 
        Color.yellow, 
        new Color32(255, 128, 0, 255),  // Turuncu
        new Color32(127, 0, 255, 255),  // Mor
        new Color32(255, 105, 180, 255),// Pembe
        Color.black, 
        Color.white 
    };
    
    private ColorTile[] allTiles;
    private int currentRound = 1;
    private bool gameEnded = false;
    private HashSet<ulong> alivePlayers = new HashSet<ulong>();

    protected override void Awake()
    {
        base.Awake();
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        allTiles = FindObjectsByType<ColorTile>(FindObjectsSortMode.None);

        if (IsServer)
        {
            foreach (var clientId in NetworkManager.Singleton.ConnectedClients.Keys)
            {
                alivePlayers.Add(clientId);
            }
            StartCoroutine(PlayGameRoutine());
        }

        if (statusText != null) statusText.gameObject.SetActive(false);
        if (spectatorCamera != null) spectatorCamera.SetActive(false);
    }

    // Ana Oyun Döngüsü (Sadece Sunucuda Çalışır)
    private IEnumerator PlayGameRoutine()
    {
        gameStarted.Value = false;
        
        // İlk başlangıç geri sayımı (5 saniye)
        for (int i = 5; i > 0; i--)
        {
            UpdateCountdownUIClientRpc(i.ToString(), Color.white);
            yield return new WaitForSeconds(1f);
        }

        gameStarted.Value = true;

        // Turlar Başlıyor
        while (currentRound <= maxRounds && alivePlayers.Count > 1 && !gameEnded)
        {
            // "TUR" yerine evrensel "ROUND" kelimesini kullanıyoruz
            UpdateTimerUIClientRpc($"ROUND: {currentRound} / {maxRounds}");

            int targetColorIndex = Random.Range(0, colors.Count);
            string targetColorName = colors[targetColorIndex]; // Örn: "RED"

            int randomVisualColorIndex = Random.Range(0, textColors.Length);
            Color32 targetVisualColor = textColors[randomVisualColorIndex];

            float timeCount = roundDuration;
            while (timeCount > 0)
            {
                UpdateCountdownUIClientRpc($"{targetColorName}\n{timeCount:F1}s", targetVisualColor);
                yield return new WaitForSeconds(0.1f);
                timeCount -= 0.1f;
            }

            // "SÜRE BİTTİ!" yerine "TIME'S UP!" yaptık
            UpdateCountdownUIClientRpc("TIME'S UP!", Color.white);

            // colors listesini doğrudan İngilizce yaptığımız için ConvertToEnglish fonksiyonuna gerek kalmadı!
            TriggerTilesClientRpc(targetColorName);

            // Oyuncuların lavın içine düşmesi için beklenen süre
            yield return new WaitForSeconds(2f);

            // ADALETLİ PUAN SİSTEMİ
            if (IsServer)
            {
                foreach (ulong clientId in alivePlayers)
                {
                    RelayManager.Instance.AddScore(clientId, 10);
                }
            }

            ResetTilesClientRpc();
            currentRound++;
            
            roundDuration = Mathf.Max(1.5f, roundDuration * 0.85f); 
            yield return new WaitForSeconds(1.5f);
        }

        EndGame();
    }

    [ClientRpc]
    private void TriggerTilesClientRpc(string correctColor)
    {
        // Karo renk kontrolünü kolaylaştırmak için sistemdeki isimlendirmeyi küçük/büyük harfe duyarlı yapabiliriz
        foreach (var tile in allTiles)
        {
            // Senin tile sisteminde renk isimleri "Red", "Blue" şeklindeyse sorun olmasın diye System.StringComparison kullandım
            if (!tile.tileColorName.Equals(correctColor, System.StringComparison.OrdinalIgnoreCase))
            {
                tile.DropTile();
            }
        }
    }

    [ClientRpc]
    private void ResetTilesClientRpc()
    {
        foreach (var tile in allTiles) tile.ResetTile();
    }

    [ClientRpc]
    private void UpdateCountdownUIClientRpc(string text, Color32 textColor)
    {
        if (countdownText == null) return;
        countdownText.text = text;
        countdownText.color = textColor;
    }

    [ClientRpc]
    private void UpdateTimerUIClientRpc(string text)
    {
        if (timerText != null) timerText.text = text;
    }

    public override void PlayerEliminated(ulong clientId)
    {
        if (!IsServer || gameEnded) return;
        if (!alivePlayers.Contains(clientId)) return;

        alivePlayers.Remove(clientId);

        int totalScoreEarnedSoFar = (currentRound - 1) * 10;

        EliminatePlayerClientRpc(clientId, totalScoreEarnedSoFar); 

        if (alivePlayers.Count <= 1) EndGame();
    }

    [ClientRpc]
    private void EliminatePlayerClientRpc(ulong eliminatedId, int score)
    {
        if (NetworkManager.Singleton.LocalClientId == eliminatedId)
        {
            var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObj != null)
            {
                var pc = playerObj.GetComponent<PlayerController>();
                if (pc != null) pc.enabled = false;
            }

            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = $"<color=red>ELIMINATED!</color>\n<size=40>+{score} POINTS</size>";
            }

            var vCams = FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None);
            foreach (var vCam in vCams) vCam.gameObject.SetActive(false);
            if (Camera.main != null) Camera.main.gameObject.SetActive(false);

            if (Instance.spectatorCamera != null)
            {
                Instance.spectatorCamera.SetActive(true);
                var specCamComp = Instance.spectatorCamera.GetComponent<Camera>();
                if (specCamComp != null) specCamComp.enabled = true;
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(AudioManager.Instance.deathSound);
        }
    }

    private void EndGame()
    {
        if (!IsServer || gameEnded) return;
        gameEnded = true;

        if (alivePlayers.Count == 1)
        {
            ulong winnerId = 0;
            foreach (var id in alivePlayers) winnerId = id;
            RelayManager.Instance.AddScore(winnerId, 30); 
            ShowWinnerUIClientRpc(winnerId);
        }

        StartCoroutine(DelayedEnd());
    }

    [ClientRpc]
    private void ShowWinnerUIClientRpc(ulong winnerId)
    {
        if (NetworkManager.Singleton.LocalClientId == winnerId)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = "<color=yellow>VICTORY!</color>\n<size=40>+30 POINTS</size>";
            }
        }
    }

    private IEnumerator DelayedEnd()
    {
        yield return new WaitForSeconds(3f);
        RelayManager.Instance.LoadNextMinigame();
    }
}