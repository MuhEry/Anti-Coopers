using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MinigameSurvivalManager : NetworkBehaviour
{
    public static MinigameSurvivalManager Instance { get; private set; }

    [Header("Oyun Ayarları")]
    [SerializeField] private float gameDuration = 120f;
    [SerializeField] private TMP_Text timerText;

    [Header("Geri Sayım")]
    [SerializeField] private TMP_Text countdownText;

    [Header("UI Bildirim")]
    [SerializeField] private TMP_Text statusText;

    private NetworkVariable<float> timeElapsed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsGameStarted => gameStarted.Value;

    private bool gameEnded = false;
    private int totalPlayers = 0;
    private int eliminatedCount = 0;
    private HashSet<ulong> eliminatedPlayers = new HashSet<ulong>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            totalPlayers = NetworkManager.Singleton.ConnectedClients.Count;
            StartCoroutine(StartCountdown());
        }

        timeElapsed.OnValueChanged += UpdateTimerUI;
        UpdateTimerUI(0, timeElapsed.Value);

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

        ShowCountdownClientRpc("YUKARI ÇIK!", true);
        yield return new WaitForSeconds(0.8f);
        HideCountdownClientRpc();
        gameStarted.Value = true;
        StartCoroutine(GameTimer());
    }

    private IEnumerator GameTimer()
    {
        while (!gameEnded)
        {
            yield return new WaitForSeconds(1f);
            timeElapsed.Value += 1f;
            if (timeElapsed.Value >= gameDuration) EndGame();
        }
    }

    private void UpdateTimerUI(float oldVal, float newVal)
    {
        if (timerText != null)
            timerText.text = $"SÜRE: {Mathf.FloorToInt(newVal)}s";
    }

    public void PlayerEliminated(ulong clientId)
    {
        if (!IsServer || gameEnded) return;
        if (eliminatedPlayers.Contains(clientId)) return;

        eliminatedPlayers.Add(clientId);
        eliminatedCount++;

        int survivalScore = Mathf.FloorToInt(timeElapsed.Value);
        RelayManager.Instance.AddScore(clientId, survivalScore);

        EliminatePlayerClientRpc(clientId, survivalScore);

        if (eliminatedCount >= totalPlayers) EndGame();
    }

    [ClientRpc]
    private void EliminatePlayerClientRpc(ulong clientId, int score)
    {
        // Elenen oyuncunun kontrolünü durdur
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObj != null)
            {
                var pc = playerObj.GetComponent<PlayerController>();
                if (pc != null) pc.enabled = false;

                var rb = playerObj.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;
            }

            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text =
                    $"<color=red>✗ ELENDİN!</color>\n" +
                    $"<color=yellow>+{score} puan</color>  (Hayatta kalma: {score}s)";
            }
        }
    }

    private void EndGame()
    {
        if (!IsServer || gameEnded) return;
        gameEnded = true;

        // Hayatta kalan oyunculara puan ver
        foreach (var clientId in NetworkManager.Singleton.ConnectedClients.Keys)
        {
            if (!eliminatedPlayers.Contains(clientId))
            {
                int bonusScore = Mathf.FloorToInt(timeElapsed.Value) + 50;
                RelayManager.Instance.AddScore(clientId, bonusScore);
                NotifySurvivedClientRpc(clientId, bonusScore);
            }
        }

        StartCoroutine(DelayedEnd());
    }

    [ClientRpc]
    private void NotifySurvivedClientRpc(ulong survivorId, int score)
    {
        if (NetworkManager.Singleton.LocalClientId != survivorId) return;
        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
            statusText.text =
                $"<color=green>✓ HAYATTA KALDIN!</color>\n" +
                $"<color=yellow>+{score} puan</color>  (+50 hayatta kalma bonusu)";
        }
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

    private IEnumerator DelayedEnd()
    {
        yield return new WaitForSeconds(2.5f);
        RelayManager.Instance.LoadNextMinigame();
    }

    public override void OnNetworkDespawn()
    {
        timeElapsed.OnValueChanged -= UpdateTimerUI;
    }
}