using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro; // UI elemanları için gerekli

public class BombMinigameManager : BaseMinigameManager 
{
    [Header("Bomba Ayarları")]
    [SerializeField] private float minExplosionTime = 10f;
    [SerializeField] private float maxExplosionTime = 15f;
    [SerializeField] private GameObject bombVisualPrefab; 
    
    [Header("Geri Sayım & UI")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text statusText;

    [Header("Bomba Ses Ayarları")]
    [Tooltip("En yavaş tık sesi aralığı (saniye)")]
    [SerializeField] private float slowTickInterval = 1.0f;
    [Tooltip("Patlamaya saniyeler kala tık sesi aralığı")]
    [SerializeField] private float fastTickInterval = 0.2f;

    [Header("İzleyici Ayarları")]
    [SerializeField] private GameObject spectatorCamera;

    public NetworkVariable<ulong> currentBombHolderId = new NetworkVariable<ulong>(9999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> bombTimer = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> isGameStarted = new NetworkVariable<bool>(false);
    public override bool IsGameStarted => isGameStarted.Value;

    private GameObject activeBombVisual;
    private List<ulong> alivePlayers = new List<ulong>();
    private bool isEnding = false;
    private float nextTickTime;

    protected override void Awake()
    {
        base.Awake(); 
    }

    public override void OnNetworkSpawn()
    {
        if (bombVisualPrefab != null)
        {
            activeBombVisual = Instantiate(bombVisualPrefab);
            activeBombVisual.SetActive(false);
        }

        currentBombHolderId.OnValueChanged += OnBombHolderChanged;

        if (IsServer)
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                alivePlayers.Add(clientId);
            }
            // 🚀 OYUN BAŞLADIĞINDA GERİ SAYIMI BAŞLAT
            StartCoroutine(StartCountdown());
        }

        // Başlangıçta yazıları gizle
        if (statusText != null) statusText.gameObject.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    // 🚀 LAVA HARİTASINDAKİ GERİ SAYIM SİSTEMİ
    private IEnumerator StartCountdown()
    {
        isGameStarted.Value = false;

        for (int i = 4; i > 0; i--) // Bomba haritası için 3 saniye idealdir
        {
            ShowCountdownClientRpc(i.ToString(), false);
            yield return new WaitForSeconds(1f);
        }

        ShowCountdownClientRpc("Give the bomb to your rivals!", true);
        yield return new WaitForSeconds(1.5f);
        HideCountdownClientRpc();
        
        isGameStarted.Value = true;
        AssignBombToRandomPlayer(); // Geri sayım bittiğinde bombayı birine ver
    }

    private void Update()
    {
        UpdateBombVisual();

        if (isGameStarted.Value && currentBombHolderId.Value != 9999 && !isEnding)
        {
            HandleTickingSound();
        }

        if (!IsServer || isEnding || !isGameStarted.Value) return;

        if (alivePlayers.Count == 1)
        {
            isEnding = true;
            ulong winnerId = alivePlayers[0];
            
            ShowWinnerUIClientRpc(winnerId);
            StartCoroutine(EndGameRoutine(winnerId));
            return;
        }
        
        // Eğer bir hata sonucu odada kimse kalmadıysa oyunu güvenle kapat
        if (alivePlayers.Count == 0)
        {
            isEnding = true;
            StartCoroutine(EndGameRoutine(9999));
            return;
        }

        // Ağ trafiğini rahatlatmak için süreyi normal şekilde düşürüp ağa veriyoruz
        float previousTime = bombTimer.Value;
        float newTime = previousTime - Time.deltaTime;
        bombTimer.Value = newTime;

        if (bombTimer.Value <= 0f)
        {
            ExplodeBomb();
        }
    }

    private void HandleTickingSound()
    {
        if (Time.time >= nextTickTime)
        {
            if (AudioManager.Instance != null && AudioManager.Instance.tickSound != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.tickSound);
            }

            float timeRatio = Mathf.Clamp01(bombTimer.Value / maxExplosionTime);
            float currentInterval = Mathf.Lerp(fastTickInterval, slowTickInterval, timeRatio);
            nextTickTime = Time.time + currentInterval;
        }
    }

    private void AssignBombToRandomPlayer()
    {
        if (alivePlayers.Count > 0)
        {
            int randomIndex = Random.Range(0, alivePlayers.Count);
            currentBombHolderId.Value = alivePlayers[randomIndex];
            bombTimer.Value = Random.Range(minExplosionTime, maxExplosionTime);
        }
    }

    public override void OnPlayerHit(ulong attackerId, ulong victimId)
    {
        if (!IsServer || !IsGameStarted || isEnding) return;

        if (currentBombHolderId.Value == attackerId && alivePlayers.Contains(victimId))
        {
            currentBombHolderId.Value = victimId;
        }
    }

    private void ExplodeBomb()
    {
        ulong deadPlayerId = currentBombHolderId.Value;
        
        TriggerExplosionEffectClientRpc(deadPlayerId);

        alivePlayers.Remove(deadPlayerId);
        PlayerEliminated(deadPlayerId);

        // Hayatta kalanlara puan ver
        if (RelayManager.Instance != null)
        {
            foreach (ulong survivorId in alivePlayers)
            {
                RelayManager.Instance.AddScore(survivorId, 15);
            }
        }

        if (alivePlayers.Count > 1)
        {
            AssignBombToRandomPlayer(); 
        }
        else
        {
            isEnding = true; 
            ulong winnerId = alivePlayers.Count > 0 ? alivePlayers[0] : 9999;
            
            // 🚀 LAVA SİSTEMİ: Kazanan varsa UI ekranını göster
            if (winnerId != 9999) ShowWinnerUIClientRpc(winnerId);
            
            StartCoroutine(EndGameRoutine(winnerId)); 
        }
    }

    private IEnumerator EndGameRoutine(ulong winnerId)
    {
        if (winnerId != 9999 && RelayManager.Instance != null)
        {
            RelayManager.Instance.AddScore(winnerId, 15);
        }

        if (activeBombVisual != null) activeBombVisual.SetActive(false);

        yield return new WaitForSeconds(4f);

        if (RelayManager.Instance != null)
        {
            RelayManager.Instance.LoadNextMinigame();
        }
    }

    [ClientRpc]
    private void TriggerExplosionEffectClientRpc(ulong playerId)
    {
        if (AudioManager.Instance != null && AudioManager.Instance.explosionSound != null) 
            AudioManager.Instance.PlaySFX(AudioManager.Instance.explosionSound);

        PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        PlayerController deadPlayer = null;

        foreach (var player in allPlayers)
        {
            if (player.OwnerClientId == playerId)
            {
                deadPlayer = player;
                break;
            }
        }

        if (deadPlayer != null)
        {
            if (NetworkManager.Singleton.LocalClientId == playerId)
            {
                deadPlayer.enabled = false; 

                // 🚀 LAVA SİSTEMİ: Ölüm Bildirimi
                if (statusText != null)
                {
                    statusText.gameObject.SetActive(true);
                    statusText.text = $"<color=red>YOU EXPLODED!</color>";
                }

                var vCams = FindObjectsByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsSortMode.None);
                foreach (var vCam in vCams) vCam.gameObject.SetActive(false);

                if (Camera.main != null) Camera.main.gameObject.SetActive(false);

                if (spectatorCamera != null) 
                {
                    spectatorCamera.SetActive(true);
                    
                    var specCamComp = spectatorCamera.GetComponent<Camera>();
                    if (specCamComp != null) specCamComp.enabled = true;
                }
            }
            deadPlayer.gameObject.SetActive(false); 
        }
    }

    // 🚀 YENİ UI FONKSİYONLARI 
    [ClientRpc]
    private void ShowWinnerUIClientRpc(ulong winnerId)
    {
        if (NetworkManager.Singleton.LocalClientId == winnerId)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(true);
                statusText.text = $"<color=yellow>YOU SURVIVED!</color>\n<size=40>+15 BONUS POINTS</size>";
            }
        }
    }

    [ClientRpc]
    private void ShowCountdownClientRpc(string text, bool isGo)
    {
        if (countdownText == null) return;
        countdownText.gameObject.SetActive(true);
        countdownText.text = text;
    }

    [ClientRpc]
    private void HideCountdownClientRpc()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    private void UpdateBombVisual()
    {
        if (activeBombVisual == null || isEnding) 
        {
            if (activeBombVisual != null) activeBombVisual.SetActive(false);
            return;
        }

        if (currentBombHolderId.Value != 9999)
        {
            GameObject holderObj = null;

            PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (player.OwnerClientId == currentBombHolderId.Value)
                {
                    holderObj = player.gameObject;
                    break;
                }
            }

            if (holderObj != null && holderObj.activeInHierarchy)
            {
                activeBombVisual.SetActive(true);
                Vector3 targetPos = holderObj.transform.position + Vector3.up * 2f;
                activeBombVisual.transform.position = Vector3.Lerp(activeBombVisual.transform.position, targetPos, Time.deltaTime * 15f);
                activeBombVisual.transform.Rotate(0, 70f * Time.deltaTime, 0); 
            }
            else
            {
                activeBombVisual.SetActive(false);
            }
        }
        else
        {
            activeBombVisual.SetActive(false);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentBombHolderId.OnValueChanged -= OnBombHolderChanged;
    }

    private void OnBombHolderChanged(ulong oldHolderId, ulong newHolderId)
    {
        PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            if (player.OwnerClientId == oldHolderId)
            {
                player.moveSpeed = 7f; // Hızı normale döndür
            }
            if (player.OwnerClientId == newHolderId)
            {
                player.moveSpeed = 7f * 1.2f; // Bomba olan oyuncuya %20 hız bonusu
            }
        }
    }
}