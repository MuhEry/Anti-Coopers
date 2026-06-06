using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MinigameParkourManager : BaseMinigameManager
{
    public static MinigameParkourManager Instance { get; private set; }

    [Header("Oyun Ayarları")]
    [SerializeField] private float gameDuration = 90f;
    [SerializeField] private int pointsPerStage = 10;
    [SerializeField] private TMP_Text timerText;

    [Header("Geri Sayım")]
    [SerializeField] private TMP_Text countdownText;

    [Header("UI Bildirim")]
    [SerializeField] private TMP_Text statusText;

    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        90f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override bool IsGameStarted => gameStarted.Value;

    private bool gameEnded = false;
    
    // SİHİRLİ LİSTE: Her oyuncunun (ulong) hangi noktaları (int) aldığını tutar
    private Dictionary<ulong, HashSet<int>> playerProgress = new Dictionary<ulong, HashSet<int>>();

    protected override void Awake()
    {
        base.Awake();
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            timeRemaining.Value = gameDuration;
            StartCoroutine(StartCountdown());
        }

        timeRemaining.OnValueChanged += UpdateTimerUI;
        UpdateTimerUI(0, timeRemaining.Value);

        if (statusText != null) statusText.gameObject.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    private IEnumerator StartCountdown()
    {
        gameStarted.Value = false;

        for (int i = 5; i > 0; i--)
        {
            ShowCountdownClientRpc(i.ToString(), false);
            yield return new WaitForSeconds(1f);
        }

        ShowCountdownClientRpc("BAŞLA!", true);
        yield return new WaitForSeconds(0.8f);
        HideCountdownClientRpc();
        
        gameStarted.Value = true;
        StartCoroutine(GameTimer());
    }

    private IEnumerator GameTimer()
    {
        while (timeRemaining.Value > 0 && !gameEnded)
        {
            yield return new WaitForSeconds(1f);
            timeRemaining.Value = Mathf.Max(0, timeRemaining.Value - 1f);
        }
        if (!gameEnded) EndGame();
    }

    private void UpdateTimerUI(float oldVal, float newVal)
    {
        if (timerText != null)
            timerText.text = $"SÜRE: {Mathf.CeilToInt(newVal)}s";
    }

    // Puan Noktası (ParkourPoint) nesnesinden çağrılır
    public bool PlayerCollectedPoint(ulong clientId, int pointId)
    {
        if (!IsServer || gameEnded || !gameStarted.Value) return false;

        // Oyuncu listede yoksa kaydını oluştur
        if (!playerProgress.ContainsKey(clientId))
        {
            playerProgress[clientId] = new HashSet<int>();
        }

        // Eğer oyuncu bu noktayı DAHA ÖNCE ALMADIYSA
        if (!playerProgress[clientId].Contains(pointId))
        {
            // Noktayı oyuncunun siciline "Alındı" olarak ekle
            playerProgress[clientId].Add(pointId);
            
            // RelayManager üzerinden ana skoruna +10 puan ekle
            RelayManager.Instance.AddScore(clientId, pointsPerStage);
            
            // Oyuncunun kendi ekranında "+10 Puan" yazısını çıkart
            NotifyScoreClientRpc(clientId, pointsPerStage, pointId);
            
            return true; // Başarıyla alındı
        }

        return false; // Zaten alınmış
    }

    [ClientRpc]
    private void NotifyScoreClientRpc(ulong targetClientId, int scoreAmount, int stageId)
    {
        // Bu mesaj sadece puanı alan kişinin ekranında çıkar
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = $"<color=yellow>+{scoreAmount}+10 PUAN";
                
                // 2 saniye sonra yazıyı ekrandan sil
                CancelInvoke(nameof(HideStatusText));
                Invoke(nameof(HideStatusText), 2f);
            }
        }
    }

    private void HideStatusText()
    {
        if (statusText != null) statusText.gameObject.SetActive(false);
    }

    private void EndGame()
    {
        if (!IsServer || gameEnded) return;
        gameEnded = true;
        gameStarted.Value = false; // Hareketi durdur
        
        RelayManager.Instance.LoadNextMinigame();
    }

    public override void OnNetworkDespawn()
    {
        timeRemaining.OnValueChanged -= UpdateTimerUI;
    }
    [ClientRpc]
    private void ShowCountdownClientRpc(string text, bool isGo)
    {
        if (countdownText == null) return;
        countdownText.gameObject.SetActive(true);
        countdownText.text = isGo
            ? $"<color=green><b>{text}</b></color>"
            : $"<color=white><b>{text}</b></color>";
    }

    [ClientRpc]
    private void HideCountdownClientRpc()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }
}