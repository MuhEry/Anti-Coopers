using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MinigameKnockoutManager : BaseMinigameManager
{
    public static MinigameKnockoutManager Instance { get; private set; }

    [Header("Oyun Ayarları")]
    [SerializeField] private float gameDuration = 50f;
    [SerializeField] private TMP_Text timerText;

    [Header("Geri Sayım & UI")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text statusText;

    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        50f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Yeni mimarimizdeki mecburi kural:
    public override bool IsGameStarted => gameStarted.Value;

    private bool gameEnded = false;
    private HashSet<ulong> alivePlayers = new HashSet<ulong>();

    protected override void Awake()
    {
        base.Awake(); // Ana menajere "Aktif oyun benim" diyoruz
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            timeRemaining.Value = gameDuration;
            
            // Oyuna katılan herkesi "Hayatta" listesine ekle
            foreach (var clientId in NetworkManager.Singleton.ConnectedClients.Keys)
            {
                alivePlayers.Add(clientId);
            }
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

        ShowCountdownClientRpc("DÜŞÜR ONLARI!", true);
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
        
        // Süre bittiyse (50 saniye dolduysa) oyunu bitir (Lav yükselecek)
        if (!gameEnded) EndGame(false);
    }

    private void UpdateTimerUI(float oldVal, float newVal)
    {
        if (timerText != null)
            timerText.text = $"SÜRE: {Mathf.CeilToInt(newVal)}s";
    }

    // Lav tetikleyicisi bu fonksiyonu çağırır
    public void PlayerEliminated(ulong clientId)
    {
        if (!IsServer || gameEnded) return;
        if (!alivePlayers.Contains(clientId)) return;

        alivePlayers.Remove(clientId);

        // Elenen kişiye o ana kadar hayatta kaldığı süre kadar puan ver
        int score = Mathf.FloorToInt(gameDuration - timeRemaining.Value);
        RelayManager.Instance.AddScore(clientId, score);
        
        ShowEliminatedUIClientRpc(clientId, score);

        // Sadece 1 kişi kaldıysa oyunu "Kazanan Var" şeklinde bitir
        if (alivePlayers.Count <= 1)
        {
            EndGame(true);
        }
    }

    [ClientRpc]
    private void ShowEliminatedUIClientRpc(ulong eliminatedId, int score)
    {
        if (NetworkManager.Singleton.LocalClientId == eliminatedId)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = $"<color=red>ELENDİN!</color>\n<size=40>+{score} Puan</size>";
            }
        }
    }

    private void EndGame(bool hasWinner)
    {
        if (!IsServer || gameEnded) return;
        gameEnded = true;
        gameStarted.Value = false; // Hareketi kilitler

        if (hasWinner && alivePlayers.Count == 1)
        {
            // Son kalanı bul ve ona +50 Bonus puan çak!
            ulong winnerId = 0;
            foreach (var id in alivePlayers) winnerId = id;

            RelayManager.Instance.AddScore(winnerId, 50);
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
                statusText.text = $"<color=yellow>SON KALAN SENSİN!</color>\n<size=40>+50 KAZANAN BONUSU</size>";
            }
        }
    }

    [ClientRpc]
    private void ShowCountdownClientRpc(string text, bool isGo)
    {
        if (countdownText == null) return;
        countdownText.gameObject.SetActive(true);
        countdownText.text = isGo
            ? $"<color=red><b>{text}</b></color>"
            : $"<color=white><b>{text}</b></color>";
    }

    [ClientRpc]
    private void HideCountdownClientRpc()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    private IEnumerator DelayedEnd()
    {
        yield return new WaitForSeconds(3f);
        RelayManager.Instance.LoadNextMinigame();
    }

    public override void OnNetworkDespawn()
    {
        timeRemaining.OnValueChanged -= UpdateTimerUI;
    }
}