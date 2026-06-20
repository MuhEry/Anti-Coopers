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
    private HashSet<ulong> finishedPlayers = new HashSet<ulong>();
    
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

        ShowCountdownClientRpc("GO!", true);
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
    public override void PlayerFinished(ulong clientId)
    {
        if (!IsServer || gameEnded || finishedPlayers.Contains(clientId)) return;

        finishedPlayers.Add(clientId);
        RelayManager.Instance.AddScore(clientId, 15);
        NotifyScoreClientRpc(clientId, 15, -1);

        int totalConnectedPlayers = NetworkManager.Singleton.ConnectedClients.Count;

        if (finishedPlayers.Count >= totalConnectedPlayers)
        {
            EndGame(); 
        }
    }

    private void UpdateTimerUI(float oldVal, float newVal)
    {
        if (timerText != null)
            timerText.text = $"TIME: {Mathf.CeilToInt(newVal)}s";
    }

    public bool PlayerCollectedPoint(ulong clientId, int pointId)
    {
        if (!IsServer || gameEnded || !gameStarted.Value) return false;

        if (!playerProgress.ContainsKey(clientId))
        {
            playerProgress[clientId] = new HashSet<int>();
        }

        if (!playerProgress[clientId].Contains(pointId))
        {
            playerProgress[clientId].Add(pointId);
            
            RelayManager.Instance.AddScore(clientId, pointsPerStage);
     
            NotifyScoreClientRpc(clientId, pointsPerStage, pointId);
            
            return true;
        }

        return false;
    }

    [ClientRpc]
    private void NotifyScoreClientRpc(ulong targetClientId, int scoreAmount, int stageId)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = $"<color=yellow>+{scoreAmount}";
                
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
        gameStarted.Value = false;
        
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