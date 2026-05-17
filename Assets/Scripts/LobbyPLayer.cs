using Unity.Netcode;
using Unity.Collections;

public class LobbyPlayer : NetworkBehaviour
{
    // Oyuncu ismini ağdaki herkese otomatik senkronize eden değişken
    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        "Bağlanıyor...", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        // İsim ağda her değiştiğinde listeyi otomatik tazele
        playerName.OnValueChanged += OnNameChanged;

        if (IsOwner)
        {
            // İstemci (Client) kendi ismini Sunucuya (Server Rpc ile) güvenli yoldan bildirir
            SetNameServerRpc(RelayManager.Instance.LocalProfileName);
        }
        
        RelayManager.Instance.UpdatePlayerListUI();
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNameChanged;
        
        if (RelayManager.Instance != null)
        {
            RelayManager.Instance.UpdatePlayerListUI();
        }
    }

    private void OnNameChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal)
    {
        RelayManager.Instance.UpdatePlayerListUI();
    }

    [ServerRpc]
    private void SetNameServerRpc(string name)
    {
        playerName.Value = name;
    }
}