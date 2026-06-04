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

    [Header("Geri Sayım Ayarları")]
    [SerializeField] private TMP_Text countdownText; // YENİ: Sahneye eklenecek

    [Header("UI Bildirim Ayarları")]
    [SerializeField] private TMP_Text finishedStatusText;

    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        120f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // PlayerController bu değeri okuyarak hareket edip edemeyeceğini anlıyor
    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsGameStarted => gameStarted.Value;

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
            totalPlayers = NetworkManager.Singleton.ConnectedClients.Count;
            StartCoroutine(StartCountdown()); // Timer yerine önce geri sayım
        }

        timeRemaining.OnValueChanged += UpdateTimerUI;
        UpdateTimerUI(0, timeRemaining.Value);

        if (finishedStatusText != null)
            finishedStatusText.gameObject.SetActive(false);

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    private IEnumerator StartCountdown()
    {
        gameStarted.Value = false;

        for (int i = 3; i > 0; i--)
        {
            ShowCountdownClientRpc(i.ToString(), false);
            yield return new WaitForSeconds(1f);
        }

        ShowCountdownClientRpc("Başla!", true);
        yield return new WaitForSeconds(0.8f);
        HideCountdownClientRpc();

        gameStarted.Value = true;
        StartCoroutine(TimerTick());
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
        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    private IEnumerator TimerTick()
    {
        while (timeRemaining.Value > 0 && !gameEnded)
        {
            yield return new WaitForSeconds(1f);
            timeRemaining.Value = Mathf.Max(0, timeRemaining.Value - 1f);
        }
        if (!gameEnded) EndMinigame();
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
        int scoreReward = Mathf.Max(0, Mathf.CeilToInt(timeRemaining.Value));
        RelayManager.Instance.AddScore(clientId, scoreReward);
        timeRemaining.Value = Mathf.Max(5f, timeRemaining.Value - 10f);
        NotifyFinishedClientRpc(clientId, scoreReward);

        if (finishedCount >= totalPlayers) EndMinigame();
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
        gameEnded = true;
        RelayManager.Instance.LoadNextMinigame();
    }

    public override void OnNetworkDespawn()
    {
        timeRemaining.OnValueChanged -= UpdateTimerUI;
    }
}