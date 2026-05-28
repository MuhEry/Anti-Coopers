using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MinigameRaceManager : NetworkBehaviour
{
    public static MinigameRaceManager Instance { get; private set; }

    [Header("Yarış Ayarları")]
    [SerializeField] private float gameDuration = 120f;
    [SerializeField] private TMP_Text timerText;

    [Header("UI Bildirim Ayarları")]
    [SerializeField] private TMP_Text finishedStatusText;

    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        120f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool gameEnded = false;
    private int totalPlayers = 0;
    private int finishedCount = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            timeRemaining.Value = gameDuration;
            // Sahnedeki toplam oyuncu sayısını al
            totalPlayers = NetworkManager.Singleton.ConnectedClients.Count;
            StartCoroutine(TimerTick());
        }

        timeRemaining.OnValueChanged += UpdateTimerUI;
        // Başlangıçta UI'ı ayarla
        UpdateTimerUI(0, timeRemaining.Value);

        if (finishedStatusText != null)
            finishedStatusText.gameObject.SetActive(false);
    }

    private IEnumerator TimerTick()
    {
        while (timeRemaining.Value > 0 && !gameEnded)
        {
            yield return new WaitForSeconds(1f);
            timeRemaining.Value = Mathf.Max(0, timeRemaining.Value - 1f);
        }

        if (!gameEnded)
            EndMinigame();
    }

    private void UpdateTimerUI(float oldVal, float newVal)
    {
        if (timerText != null)
            timerText.text = $"SÜRE: {Mathf.CeilToInt(newVal)}s";
    }

    public void PlayerFinished(ulong clientId)
    {
        if (!IsServer || gameEnded) return;

        finishedCount++;

        // Kalan süre kadar puan ver
        int scoreReward = Mathf.Max(0, Mathf.CeilToInt(timeRemaining.Value));
        RelayManager.Instance.AddScore(clientId, scoreReward);

        Debug.Log($"Oyuncu {clientId} bitirdi! Puan: {scoreReward}");

        // Süreyi 10 azalt (minimum 5 saniye kalsın)
        timeRemaining.Value = Mathf.Max(5f, timeRemaining.Value - 10f);

        // O oyuncunun ekranına bildirim gönder
        NotifyFinishedClientRpc(clientId, scoreReward);

        // Tüm oyuncular bitirdiyse oyunu bitir
        if (finishedCount >= totalPlayers)
            EndMinigame();
    }

    [ClientRpc]
    private void NotifyFinishedClientRpc(ulong finishedClientId, int score)
    {
        if (NetworkManager.Singleton.LocalClientId != finishedClientId) return;

        if (finishedStatusText != null)
        {
            finishedStatusText.gameObject.SetActive(true);
            finishedStatusText.text =
                $"<color=green>✓ BİTİŞ ÇİZGİSİNE ULAŞILDI!</color>\n" +
                $"<color=yellow>+{score} puan kazandın!</color>\n" +
                $"Diğer oyuncular bekleniyor...";
        }
    }

    public void EndMinigame()
    {
        if (!IsServer || gameEnded) return;
        gameEnded = true; // DÜZELTME: false değil true olmalı
        Debug.Log("Minigame bitti. Scoreboard'a geçiliyor...");
        RelayManager.Instance.LoadNextMinigame();
    }

    public override void OnNetworkDespawn()
    {
        timeRemaining.OnValueChanged -= UpdateTimerUI;
    }
}