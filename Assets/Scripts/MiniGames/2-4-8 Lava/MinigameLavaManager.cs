using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MinigameLavaManager : BaseMinigameManager
{
    public static MinigameLavaManager Instance { get; private set; }

    [Header("Oyun Ayarları")]
    [SerializeField] private float gameDuration = 60f; // Haritaya göre editörden değiştirebilirsin
    [SerializeField] private TMP_Text timerText;

    [Header("Geri Sayım & UI")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text statusText;

    [Header("Garantili İzleyici Kamerası")]
    [Tooltip("Sahnedeki Spectator Camera objesini doğrudan buraya sürükleyin")]
    [SerializeField] private GameObject spectatorCamera;

    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        60f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override bool IsGameStarted => gameStarted.Value;

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
        if (IsServer)
        {
            timeRemaining.Value = gameDuration;
            
            // Odadaki herkesi hayatta olarak listeye ekle
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

        // Oyun başlarken izleyici kamerasını zorla kapalı tutalım
        if (spectatorCamera != null) spectatorCamera.SetActive(false);
    }

    private IEnumerator StartCountdown()
    {
        gameStarted.Value = false;

        for (int i = 5; i > 0; i--)
        {
            ShowCountdownClientRpc(i.ToString(), false);
            yield return new WaitForSeconds(1f);
        }

        ShowCountdownClientRpc("STAY ALIVE!", true);
        yield return new WaitForSeconds(1.3f);
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
        
        // Süre dolduğunda hala birden fazla kişi hayattaysa herkesi bitir
        if (!gameEnded) EndGame(false);
    }

    private void UpdateTimerUI(float oldVal, float newVal)
    {
        if (timerText != null)
            timerText.text = $"TIME: {Mathf.CeilToInt(newVal)}s";
    }

    public override void PlayerEliminated(ulong clientId)
    {
        if (!IsServer || gameEnded) return;
        if (!alivePlayers.Contains(clientId)) return;

        alivePlayers.Remove(clientId);

        // Elenen kişiye o ana kadar hayatta kaldığı süre kadar puan ver
        int score = Mathf.FloorToInt(gameDuration - timeRemaining.Value);
        RelayManager.Instance.AddScore(clientId, score);
        
        EliminatePlayerClientRpc(clientId, score);

        // Geriye sadece 1 kişi kaldıysa oyunu bitir
        if (alivePlayers.Count <= 1)
        {
            EndGame(true);
        }
    }

    private void EndGame(bool hasWinner)
    {
        if (!IsServer || gameEnded) return;
        gameEnded = true;
        gameStarted.Value = false;

        if (hasWinner && alivePlayers.Count == 1)
        {
            // Son kalanı bul ve ona toplam süre (Maksimum) kadar puan ver!
            ulong winnerId = 99999;
            foreach (var id in alivePlayers) winnerId = id;

            int maxScore = Mathf.FloorToInt(gameDuration);
            RelayManager.Instance.AddScore(winnerId, maxScore);
            ShowWinnerUIClientRpc(winnerId, maxScore);
        }

        StartCoroutine(DelayedEnd());
    }

    [ClientRpc]
    private void EliminatePlayerClientRpc(ulong eliminatedId, int score)
    {
        // Elenen kişi "BİZ" isek
        if (NetworkManager.Singleton.LocalClientId == eliminatedId)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.deathSound);
            }
            // 1. Karakteri dondur
            var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObj != null)
            {
                var pc = playerObj.GetComponent<PlayerController>();
                if (pc != null) pc.enabled = false;
            }

            // 2. UI Bildirimi
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = $"<color=red>YOU ARE ELIMINATED!</color>\n<size=40>+{score} Points</size>";
            }

            // 3. KAMERA GARANTİSİ: İsimle aramak yerine sahnede çalışan tüm oyuncu kameralarını zorla kapatıyoruz
            var vCams = FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None);
            foreach (var vCam in vCams) vCam.gameObject.SetActive(false);

            if (Camera.main != null) Camera.main.gameObject.SetActive(false);

            // 4. Editörden bağladığımız İzleyici Kamerasını açıyoruz (Asla şaşmaz)
            if (Instance.spectatorCamera != null)
            {
                Instance.spectatorCamera.SetActive(true);
                var specCamComp = Instance.spectatorCamera.GetComponent<Camera>();
                if (specCamComp != null) specCamComp.enabled = true;
            }
        }
    }

    [ClientRpc]
    private void ShowWinnerUIClientRpc(ulong winnerId, int maxScore)
    {
        if (NetworkManager.Singleton.LocalClientId == winnerId)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = $"<color=yellow>YOU ARE THE LAST ONE STANDING!</color>\n<size=40>+{maxScore} POINTS</size>";
            }
        }
    }

    [ClientRpc]
    private void ShowCountdownClientRpc(string text, bool isGo)
    {
        if (countdownText == null) return;
        countdownText.gameObject.SetActive(true);
        countdownText.text = isGo ? $"<color=red><b>{text}</b></color>" : $"<color=white><b>{text}</b></color>";
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