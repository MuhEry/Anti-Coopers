using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MinigameCrownManager : BaseMinigameManager
{
    public static MinigameCrownManager Instance { get; private set; }

    [Header("Oyun Ayarları")]
    [SerializeField] private float gameDuration = 60f;
    [SerializeField] private Transform crownObject; // Sahnedeki Taç objesi

    [Header("UI")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text statusText;

    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(60f);
    private NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(false);
    
    // Tacın şu anki sahibinin ID'sini tutar (99999 = Sahipsiz)
    private NetworkVariable<ulong> currentKingId = new NetworkVariable<ulong>(99999);

    public override bool IsGameStarted => gameStarted.Value;

    protected override void Awake()
    {
        base.Awake();
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            timeRemaining.Value = gameDuration;
            StartCoroutine(StartCountdown());
            StartCoroutine(PointTick()); // Puan verme döngüsü
        }
        timeRemaining.OnValueChanged += (oldV, newV) => timerText.text = $"SÜRE: {Mathf.CeilToInt(newV)}s";
    }

    private IEnumerator StartCountdown()
    {
        for (int i = 5; i > 0; i--) { ShowUIClientRpc(i.ToString(), false); yield return new WaitForSeconds(1f); }
        ShowUIClientRpc("TACI ELE GEÇİR!", true);
        yield return new WaitForSeconds(1f);
        statusText.gameObject.SetActive(false);
        gameStarted.Value = true;
    }

    // YENİ KRİTİK FONKSİYON: Yumruk atıldığında taç el değiştirir
    public override void OnPlayerHit(ulong attackerId, ulong victimId)
    {
        if (!IsServer || !gameStarted.Value) return;

        // Eğer yumruk yiyen kişi şu anki Kralsa, taç vuran kişiye geçer!
        if (victimId == currentKingId.Value)
        {
            currentKingId.Value = attackerId;
            NotifyNewKingClientRpc(attackerId);
        }
    }

    // Taç yerdeyken ilk dokunan kral olur
    public void InitialPickup(ulong clientId)
    {
        if (IsServer && currentKingId.Value == 99999)
        {
            currentKingId.Value = clientId;
            NotifyNewKingClientRpc(clientId);
        }
    }

    private IEnumerator PointTick()
    {
        while (timeRemaining.Value > 0)
        {
            yield return new WaitForSeconds(1f);
            if (gameStarted.Value)
            {
                timeRemaining.Value -= 1f;
                // Kral hayattaysa her saniye 2 puan kazanır
                if (currentKingId.Value != 99999)
                    RelayManager.Instance.AddScore(currentKingId.Value, 2);
            }
        }
        gameStarted.Value = false;
        RelayManager.Instance.LoadNextMinigame();
    }

    private void Update()
    {
        // Tacın görsel olarak Kralın üzerinde durmasını sağlar
        if (currentKingId.Value != 99999 && crownObject != null)
        {
            GameObject kingObj = null;

            // Sunucu (Host) Netcode kütüphanesini kullanarak doğrudan bulabilir
            if (IsServer)
            {
                var netObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(currentKingId.Value);
                if (netObj != null) kingObj = netObj.gameObject;
            }
            else
            {
                // DÜZELTME: Client'lar güvenlik engeline takılmamak için sahnedeki oyuncuları tarar
                PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                foreach (var player in allPlayers)
                {
                    if (player.OwnerClientId == currentKingId.Value)
                    {
                        kingObj = player.gameObject;
                        break;
                    }
                }
            }

            if (kingObj != null)
            {
                crownObject.position = Vector3.Lerp(crownObject.position, kingObj.transform.position + Vector3.up * 2.5f, Time.deltaTime * 10f);
                crownObject.Rotate(Vector3.up, 100f * Time.deltaTime);
            }
        }
    }

    [ClientRpc]
    private void NotifyNewKingClientRpc(ulong kingId)
    {
        string kingName = "Biri"; 
        // İsmi RelayManager'dan çekebiliriz
        statusText.gameObject.SetActive(true);
        statusText.text = $"<color=yellow>YENİ KRAL BELİRLENDİ!</color>";
        Invoke(nameof(HideStatus), 1.5f);
    }

    private void HideStatus() => statusText.gameObject.SetActive(false);

    [ClientRpc]
    private void ShowUIClientRpc(string t, bool go) { 
        statusText.gameObject.SetActive(true); 
        statusText.text = t; 
    }
}