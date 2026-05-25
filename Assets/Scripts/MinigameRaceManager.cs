using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;

public class MinigameRaceManager : NetworkBehaviour
{
    public static MinigameRaceManager Instance { get; private set; }

    [Header("Yarış Ayarları")]
    [SerializeField] private float gameDuration = 45f; // Başlangıç süresi
    [SerializeField] private TMP_Text timerText;

    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(45f);
    private bool gameEnded = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            timeRemaining.Value = gameDuration;
            StartCoroutine(TimerTick());
        }
        timeRemaining.OnValueChanged += UpdateTimerUI;
    }

    private IEnumerator TimerTick()
    {
        while (timeRemaining.Value > 0 && !gameEnded)
        {
            yield return new WaitForSeconds(1f);
            timeRemaining.Value -= 1f;
        }

        if (timeRemaining.Value <= 0 && !gameEnded)
        {
            EndMinigame();
        }
    }

    private void UpdateTimerUI(float oldVal, float newVal)
    {
        if (timerText != null)
        {
            timerText.text = $"SÜRE: {Mathf.CeilToInt(newVal)}s";
        }
    }

    // Server-Authoritative: Bir oyuncu bitiş çizgisine girdiğinde tetiklenir
    public void PlayerFinished(ulong clientId)
    {
        if (!IsServer || gameEnded) return;

        // Bitişe ulaşan oyuncunun skorunu ekle (Kalan saniye kadar puan)
        int scoreReward = Mathf.CeilToInt(timeRemaining.Value);
        if (scoreReward < 0) scoreReward = 0;

        RelayManager.Instance.AddScore(clientId, scoreReward);
        Debug.Log($"Oyuncu {clientId} bitirdi! Kazanılan Puan: {scoreReward}");

        // Her bitiren oyuncuda süreyi 5 saniye azalt (Minimum 2 saniyeye kadar düşebilir)
        if (timeRemaining.Value > 5f)
        {
            timeRemaining.Value -= 5f;
        }
    }

    public void EndMinigame()
    {
        if (!IsServer || gameEnded) return;
        gameEnded = false; // Döngü koruması

        Debug.Log("Süre bitti veya herkes bitirdi. Skor perdesine geçiliyor...");
        
        // RelayManager üzerinden bir sonraki sahneye (Scoreboard) geçiş tetikle
        RelayManager.Instance.LoadNextMinigame();
    }

    public override void OnNetworkDespawn()
    {
        timeRemaining.OnValueChanged -= UpdateTimerUI;
    }
}