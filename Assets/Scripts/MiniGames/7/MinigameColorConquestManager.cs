using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MinigameColorConquestManager : BaseMinigameManager
{
    [Header("UI Elemanları")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text countdownText;

    [Header("Ayarlar")]
    [SerializeField] private float gameDuration = 45f;

    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        45f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override bool IsGameStarted => gameStarted.Value;

    private PaintTile[] allPaintTiles;
    private bool gameEnded = false;

    private void Start()
    {
        allPaintTiles = FindObjectsByType<PaintTile>(FindObjectsSortMode.None);
        
        if (IsServer)
        {
            timeRemaining.Value = gameDuration;
            StartCoroutine(GameRoutine());
        }

        timeRemaining.OnValueChanged += (oldV, newV) => {
            if (timerText != null) timerText.text = $"SÜRE: {Mathf.CeilToInt(newV)}s";
        };
    }

    private IEnumerator GameRoutine()
    {
        gameStarted.Value = false;
        for (int i = 3; i > 0; i--)
        {
            UpdateCountdownClientRpc(i.ToString());
            yield return new WaitForSeconds(1f);
        }
        UpdateCountdownClientRpc("BOYA!");
        yield return new WaitForSeconds(0.8f);
        UpdateCountdownClientRpc("");

        gameStarted.Value = true;

        while (timeRemaining.Value > 0)
        {
            yield return new WaitForSeconds(1f);
            timeRemaining.Value = Mathf.Max(0, timeRemaining.Value - 1f);
        }

        EndGame();
    }

    private void EndGame()
    {
        if (!IsServer || gameEnded) return;
        gameEnded = true;
        gameStarted.Value = false;

        // Skor Hesaplama Algoritması
        Dictionary<ulong, int> playerTileCounts = new Dictionary<ulong, int>();

        // Herkesi 0 karo ile başlat
        foreach (var clientId in NetworkManager.Singleton.ConnectedClients.Keys)
        {
            playerTileCounts[clientId] = 0;
        }

        // Tüm karoları tara
        foreach (var tile in allPaintTiles)
        {
            ulong ownerId = tile.paintedByPlayerId.Value;
            if (ownerId != 9999 && playerTileCounts.ContainsKey(ownerId))
            {
                playerTileCounts[ownerId]++;
            }
        }

        // Karolar oranında puan dağıt (Örn: Her 1 karo = 2 Puan)
        foreach (var kvp in playerTileCounts)
        {
            int earnedScore = kvp.Value * 2;
            RelayManager.Instance.AddScore(kvp.Key, earnedScore);
        }

        UpdateCountdownClientRpc("SÜRE BİTTİ!");
        StartCoroutine(DelayedEnd());
    }

    [ClientRpc]
    private void UpdateCountdownClientRpc(string text)
    {
        if (countdownText != null) countdownText.text = text;
    }

    private IEnumerator DelayedEnd()
    {
        yield return new WaitForSeconds(3f);
        RelayManager.Instance.LoadNextMinigame();
    }
}