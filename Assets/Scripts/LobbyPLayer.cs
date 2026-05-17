using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour
{
    [Header("Görsel Bileşenler")]
    [SerializeField] private MeshRenderer bodyRenderer; // Prefab içindeki Capsule'ün Mesh Renderer'ı

    // Ağ üzerinde otomatik senkronize olan değişkenler (NetworkVariables)
    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    
    public NetworkVariable<bool> isReady = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );
    
    public NetworkVariable<Color> playerColor = new NetworkVariable<Color>(
        Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        // Değişkenlerin değerleri ağda her değiştiğinde arayüzü ve renkleri tazeleyen olaylar (Events)
        playerName.OnValueChanged += OnPlayerNameChanged;
        isReady.OnValueChanged += OnPlayerReadyChanged;
        playerColor.OnValueChanged += OnPlayerColorChanged;

        if (IsOwner)
        {
            // Eğer bu karakter bana aitse, profil ismimi ve rastgele rengimi sunucuya bildiriyorum
            Color randomColor = new Color(Random.value, Random.value, Random.value);
            SetPlayerDataServerRpc(RelayManager.Instance.LocalProfileName, randomColor);
        }
        else
        {
            // Eğer başkasının karakteriyse, onun ağdan gelen mevcut rengini hemen uyguluyoruz
            ApplyColor(playerColor.Value);
        }

        // Arayüzü (UI) yeni gelen oyuncuya göre güncelle
        if (RelayManager.Instance != null)
        {
            RelayManager.Instance.UpdatePlayerListUI();
        }
    }

    public override void OnNetworkDespawn()
    {
        // Bellek sızıntılarını (Memory Leak) önlemek için abonelikleri temizliyoruz
        playerName.OnValueChanged -= OnPlayerNameChanged;
        isReady.OnValueChanged -= OnPlayerReadyChanged;
        playerColor.OnValueChanged -= OnPlayerColorChanged;

        if (RelayManager.Instance != null)
        {
            RelayManager.Instance.UpdatePlayerListUI();
        }
    }

    // --- Değişim Takip Fonksiyonları ---
    private void OnPlayerNameChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal)
    {
        RelayManager.Instance.UpdatePlayerListUI();
    }

    private void OnPlayerReadyChanged(bool oldVal, bool newVal)
    {
        RelayManager.Instance.UpdatePlayerListUI();
    }

    private void OnPlayerColorChanged(Color oldVal, Color newVal)
    {
        ApplyColor(newVal);
    }

    private void ApplyColor(Color targetColor)
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = targetColor;
        }
    }

    // --- ServerRpc Fonksiyonları (Sunucuda Çalışan Emirler) ---

    [ServerRpc]
    private void SetPlayerDataServerRpc(string name, Color color)
    {
        playerName.Value = name;
        playerColor.Value = color;
    }

    [ServerRpc]
    public void ToggleReadyServerRpc()
    {
        isReady.Value = !isReady.Value;
    }
}