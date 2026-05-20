using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour
{
    [Header("Görsel Bileşenler")]
    [SerializeField] private MeshRenderer bodyRenderer; 

    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color> playerColor = new NetworkVariable<Color>(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnPlayerNameChanged;
        isReady.OnValueChanged += OnPlayerReadyChanged;
        playerColor.OnValueChanged += OnPlayerColorChanged;

        // LOBİDE ÜST ÜSTE DOĞMA ÇÖZÜLDÜ: Oyuncu ID'sine göre lobi sahnesinde yan yana diziyoruz
        transform.position = new Vector3(OwnerClientId * 2.0f, 1.0f, 0f);

        if (IsOwner)
        {
            Color randomColor = new Color(Random.value, Random.value, Random.value);
            SetPlayerDataServerRpc(RelayManager.Instance.LocalProfileName, randomColor);
        }
        else
        {
            ApplyColor(playerColor.Value);
        }

        if (RelayManager.Instance != null) RelayManager.Instance.UpdatePlayerListUI();
    }

    // YENİ: Sahne değişirken bu lobi objesinin tamamen silinmesini garanti ediyoruz
    private void Awake()
    {
        // Eğer bu obje DontDestroyOnLoad moduna girmişse engellemek için
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
    {
        if (newScene.name == "GameScene")
        {
            // Yeni sahneye geçildiyse ve bu obje sunucu tarafındaysa ağdan temizle
            if (IsServer)
            {
                GetComponent<NetworkObject>().Despawn(true);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnPlayerNameChanged;
        isReady.OnValueChanged -= OnPlayerReadyChanged;
        playerColor.OnValueChanged -= OnPlayerColorChanged;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;

        if (RelayManager.Instance != null) RelayManager.Instance.UpdatePlayerListUI();
    }

    private void OnPlayerNameChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal) => RelayManager.Instance.UpdatePlayerListUI();
    private void OnPlayerReadyChanged(bool oldVal, bool newVal) => RelayManager.Instance.UpdatePlayerListUI();
    private void OnPlayerColorChanged(Color oldVal, Color newVal) => ApplyColor(newVal);
    private void ApplyColor(Color targetColor) { if (bodyRenderer != null) bodyRenderer.material.color = targetColor; }

    [ServerRpc]
    private void SetPlayerDataServerRpc(string name, Color color)
    {
        playerName.Value = name;
        playerColor.Value = color;
    }

    [ServerRpc]
    public void ToggleReadyServerRpc() => isReady.Value = !isReady.Value;
}