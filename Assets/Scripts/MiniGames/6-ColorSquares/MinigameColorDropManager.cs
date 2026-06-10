using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MinigameColorDropManager : BaseMinigameManager
{
    public static MinigameColorDropManager Instance { get; private set; }

    [Header("UI Elemanları")]
    [SerializeField] private TMP_Text timerText;        // Üstteki genel süre veya Tur bilgisi
    [SerializeField] private TMP_Text countdownText;    // Ortadaki büyük renk ve geri sayım yazısı
    [SerializeField] private TMP_Text statusText;       // Elendin yazısı için
    [SerializeField] private GameObject spectatorCamera; // Ölenlerin kamerası

    [Header("Oyun Ayarları")]
    [SerializeField] private float roundDuration = 4f;  // Oyuncuların rengi bulması için kaç saniyesi var?
    [SerializeField] private int maxRounds = 5;         // Toplam kaç tur sürecek?

    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override bool IsGameStarted => gameStarted.Value;

    // Karoların düşürülmesinde referans alınacak ana Türkçe renk listesi
    private List<string> colors = new List<string> 
    { 
        "KIRMIZI", "MAVİ", "YEŞİL", "SARI", "TURUNCU", "MOR", "PEMBE", "SİYAH", "BEYAZ" 
    };

    // Yazının bürünebileceği rastgele renk havuzu (Hex kodlarına gerek kalmadı)
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
        // Sahnede el ile yerleştirdiğimiz tüm karoları hafızaya alıyoruz
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
            UpdateTimerUIClientRpc($"TUR: {currentRound} / {maxRounds}");

            int targetColorIndex = Random.Range(0, colors.Count);
            string targetColorName = colors[targetColorIndex];

            int randomVisualColorIndex = Random.Range(0, textColors.Length);
            Color32 targetVisualColor = textColors[randomVisualColorIndex];

            float timeCount = roundDuration;
            while (timeCount > 0)
            {
                UpdateCountdownUIClientRpc($"{targetColorName}\n{timeCount:F1}s", targetVisualColor);
                yield return new WaitForSeconds(0.1f);
                timeCount -= 0.1f;
            }

            UpdateCountdownUIClientRpc("SÜRE BİTTİ!", Color.white);

            string englishColorName = ConvertToEnglish(targetColorName);
            TriggerTilesClientRpc(englishColorName);

            // Oyuncuların lavın içine düşmesi için beklenen süre
            yield return new WaitForSeconds(2f);

            // =================================================================
            // YENİ ADALETLİ PUAN SİSTEMİ: 
            // Tur bitti, karolar sıfırlanmadan önce HALA HAYATTA olan herkese puan veriyoruz!
            if (IsServer)
            {
                foreach (ulong clientId in alivePlayers)
                {
                    // Her başarılı tur için oyunculara 10'ar puan ekle
                    RelayManager.Instance.AddScore(clientId, 10);
                }
            }
            // =================================================================

            // Karoları sıfırla ve yeni tura hazırla
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
        foreach (var tile in allTiles)
        {
            if (tile.tileColorName != correctColor)
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

        // UI'da oyuncuya toplamda kaç puanla elendiğini dürüstçe gösteriyoruz
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
                statusText.text = $"<color=red>ELENDİN!</color>\n<size=40>+{score} Puan</size>";
            }

            var vCams = FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None);
            foreach (var vCam in vCams) vCam.gameObject.SetActive(false);
            if (Camera.main != null) Camera.main.gameObject.SetActive(false);

            if (spectatorCamera != null) spectatorCamera.SetActive(true);

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
                statusText.text = "<color=yellow>KAZANAN SENSİN!</color>\n<size=40>+30 PUAN</size>";
            }
        }
    }

    private IEnumerator DelayedEnd()
    {
        yield return new WaitForSeconds(3f);
        RelayManager.Instance.LoadNextMinigame();
    }

    private string ConvertToEnglish(string turkishColor)
    {
        if (turkishColor == "KIRMIZI") return "Red";
        if (turkishColor == "MAVİ") return "Blue";
        if (turkishColor == "YEŞİL") return "Green";
        if (turkishColor == "SARI") return "Yellow";
        if (turkishColor == "TURUNCU") return "Orange";
        if (turkishColor == "MOR") return "Purple";
        if (turkishColor == "PEMBE") return "Pink";
        if (turkishColor == "SİYAH") return "Black";
        if (turkishColor == "BEYAZ") return "White";
        return "";
    }
}