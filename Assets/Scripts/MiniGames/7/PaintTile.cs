using Unity.Netcode;
using UnityEngine;

public class PaintTile : MonoBehaviour
{
    // Şu an bu karoyu hangi Client ID'ye sahip oyuncu boyadı? (Varsayılan 9999 = Sahipsiz)
    public NetworkVariable<ulong> paintedByPlayerId = new NetworkVariable<ulong>(
        9999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private MeshRenderer meshRenderer;
    private Color originalColor;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null) originalColor = meshRenderer.material.color;
    }

    private void Start()
    {
        // Ağ değişkeni değiştikçe rengi tüm oyuncularda güncelle
        paintedByPlayerId.OnValueChanged += (oldId, newId) => UpdateTileVisual(newId);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (other.CompareTag("Player"))
        {
            NetworkObject netObj = other.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                // Karoyu basan oyuncunun ID'sine mühürle
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

        // RelayManager'da kayıtlı olan oyuncu rengini çekip karoya boyuyoruz!
        if (RelayManager.Instance != null && RelayManager.Instance.GetMySavedData(playerId, out string name, out Color32 pColor))
        {
            if (meshRenderer != null) meshRenderer.material.color = pColor;
        }
    }

    public void ResetTile()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            paintedByPlayerId.Value = 9999;
        }
    }
}