using Unity.Netcode;
using UnityEngine;

public class PaintTile : NetworkBehaviour // KURAL: NetworkBehaviour olmalı!
{
    public NetworkVariable<ulong> paintedByPlayerId = new NetworkVariable<ulong>(
        9999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private MeshRenderer meshRenderer;
    private Color originalColor;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null) originalColor = meshRenderer.material.color;
    }

    public override void OnNetworkSpawn()
    {
        // Değişim aboneliğini ağ doğduğunda yapıyoruz
        paintedByPlayerId.OnValueChanged += OnTileColorChanged;

        // Misafir oyuncu sahneye yeni girdiğinde, yerdeki mevcut durumu hemen görsün diye:
        UpdateTileVisual(paintedByPlayerId.Value);
    }

    public override void OnNetworkDespawn()
    {
        // Bellek sızıntısını önlemek için abonelikten çıkıyoruz
        paintedByPlayerId.OnValueChanged -= OnTileColorChanged;
    }

    private void OnTileColorChanged(ulong oldId, ulong newId)
    {
        UpdateTileVisual(newId);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // Kısa yoldan IsServer kontrolü

        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                paintedByPlayerId.Value = netObj.OwnerClientId;
            }
        }
    }

    private void UpdateTileVisual(ulong playerId)
    {
        if (playerId == 9999)
        {
            if (meshRenderer != null) meshRenderer.material.color = originalColor;
            return;
        }

        if (RelayManager.Instance != null && RelayManager.Instance.GetMySavedData(playerId, out string name, out Color32 pColor))
        {
            if (meshRenderer != null) meshRenderer.material.color = pColor;
        }
    }

    public void ResetTile()
    {
        if (IsServer)
        {
            paintedByPlayerId.Value = 9999;
        }
    }
}