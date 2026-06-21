using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BombMinigameManager : BaseMinigameManager 
{
    [Header("Bomba Ayarları")]
    [SerializeField] private float minExplosionTime = 10f;
    [SerializeField] private float maxExplosionTime = 15f;
    [SerializeField] private GameObject bombVisualPrefab; 
    [SerializeField] private GameObject explosionEffectPrefab; 
    [SerializeField] private float startDelay = 3f; // Oyun başlamadan önceki geri sayım süresi

    public NetworkVariable<ulong> currentBombHolderId = new NetworkVariable<ulong>(9999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> bombTimer = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 🚀 DÜZELTME 1: Abstract IsGameStarted özelliğini implemente ediyoruz
    private NetworkVariable<bool> isGameStarted = new NetworkVariable<bool>(false);
    public override bool IsGameStarted => isGameStarted.Value;

    private GameObject activeBombVisual;
    private List<ulong> alivePlayers = new List<ulong>();
    private float startTimer;

    protected override void Awake()
    {
        base.Awake(); // BaseMinigameManager'daki ActiveMinigame = this satırını çalıştırır
        useTopDownCamera = true;
        startTimer = startDelay;
    }

    public override void OnNetworkSpawn()
    {
        if (bombVisualPrefab != null)
        {
            activeBombVisual = Instantiate(bombVisualPrefab);
            activeBombVisual.SetActive(false);
        }

        if (IsServer)
        {
            // Oyun başlarken odadaki herkesi "Hayatta" olarak listeye ekliyoruz
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                alivePlayers.Add(clientId);
            }
        }
    }

    // 🚀 DÜZELTME 2: 'override Update' yerine düz 'Update' kullanıyoruz
    private void Update()
    {
        UpdateBombVisual();

        if (!IsServer) return;

        // --- GERİ SAYIM AŞAMASI ---
        if (!isGameStarted.Value)
        {
            startTimer -= Time.deltaTime;
            if (startTimer <= 0f)
            {
                isGameStarted.Value = true;
                AssignBombToRandomPlayer(); // Geri sayım bittiğinde bombayı birine ver
            }
            return;
        }

        // --- OYUN AŞAMASI ---
        if (alivePlayers.Count <= 1) return; // 1 kişi kaldıysa oyun bitmiştir

        bombTimer.Value -= Time.deltaTime;

        if (bombTimer.Value <= 0f)
        {
            ExplodeBomb();
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
        if (!IsServer || !IsGameStarted) return;

        // Vuran kişi bombaya sahipse ve vurulan kişi hâlâ hayattaysa bombayı devret
        if (currentBombHolderId.Value == attackerId && alivePlayers.Contains(victimId))
        {
            currentBombHolderId.Value = victimId;
        }
    }

    private void ExplodeBomb()
    {
        ulong deadPlayerId = currentBombHolderId.Value;
        
        TriggerExplosionEffectClientRpc(deadPlayerId);

        // Oyuncuyu hayattakiler listesinden çıkar
        alivePlayers.Remove(deadPlayerId);
        
        // Base sınıftaki event'i çağır
        PlayerEliminated(deadPlayerId);

        if (alivePlayers.Count > 1)
        {
            AssignBombToRandomPlayer(); // Hâlâ oyuncu varsa yeni bomba ata
        }
        else
        {
            // Sadece 1 kişi kaldı! Şampiyonu belirle
            ulong winnerId = alivePlayers[0];
            if (RelayManager.Instance != null)
            {
                RelayManager.Instance.AddScore(winnerId, 10); // Kazanana 10 puan ekle
                RelayManager.Instance.LoadNextMinigame(); // Skor tablosuna geç
            }
        }
    }

    [ClientRpc]
    private void TriggerExplosionEffectClientRpc(ulong playerId)
    {
        // Ses efekti
        if (AudioManager.Instance != null && AudioManager.Instance.explosionSound != null) 
            AudioManager.Instance.PlaySFX(AudioManager.Instance.explosionSound);

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var client))
        {
            // Patlama görselini oluştur
            if (explosionEffectPrefab != null)
            {
                Instantiate(explosionEffectPrefab, client.PlayerObject.transform.position, Quaternion.identity);
            }
            
            // Ölen oyuncunun karakterini görsel olarak gizle
            if (client.PlayerObject != null)
            {
                client.PlayerObject.gameObject.SetActive(false); 
            }
        }
    }

    private void UpdateBombVisual()
    {
        if (activeBombVisual == null) return;

        if (currentBombHolderId.Value != 9999 && NetworkManager.Singleton.ConnectedClients.TryGetValue(currentBombHolderId.Value, out var client))
        {
            // Oyuncu sahnede aktifse bombayı kafasına koy
            if (client.PlayerObject != null && client.PlayerObject.gameObject.activeInHierarchy)
            {
                activeBombVisual.SetActive(true);
                Vector3 targetPos = client.PlayerObject.transform.position + Vector3.up * 2.5f;
                activeBombVisual.transform.position = Vector3.Lerp(activeBombVisual.transform.position, targetPos, Time.deltaTime * 15f);
                activeBombVisual.transform.Rotate(0, 180f * Time.deltaTime, 0); 
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
}