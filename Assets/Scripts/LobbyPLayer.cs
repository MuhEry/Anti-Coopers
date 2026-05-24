using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour
{
    [Header("Görsel Bileşenler")]
    [SerializeField] private MeshRenderer bodyRenderer; 

    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color32> playerColor = new NetworkVariable<Color32>(
        new Color32(255, 255, 255, 255), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnPlayerNameChanged;
        isReady.OnValueChanged += OnPlayerReadyChanged;
        playerColor.OnValueChanged += (oldV, newV) => ApplyColor(newV);

        transform.position = new Vector3(OwnerClientId * 2.0f, 1.0f, 0f);

        if (IsOwner)
        {
            Color32 randomColor = new Color(Random.value, Random.value, Random.value);
            SetPlayerDataServerRpc(RelayManager.Instance.LocalProfileName, randomColor);
        }
        else
        {
            ApplyColor(playerColor.Value);
        }

        if (RelayManager.Instance != null) RelayManager.Instance.UpdatePlayerListUI();
    }

    [ServerRpc]
    public void SelectColorServerRpc(Color32 newColor)
    {
        playerColor.Value = newColor;
    }

    private void Awake()
    {
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
    {
        if (newScene.name.StartsWith("MiniGame_") || newScene.name == "GameScene")
        {
            if (IsServer) GetComponent<NetworkObject>().Despawn(true);
        }
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnPlayerNameChanged;
        isReady.OnValueChanged -= OnPlayerReadyChanged;
        playerColor.OnValueChanged -= (oldV, newV) => ApplyColor(newV); // HATAYI DÜZELTTİK: -= Yaptık
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;

        if (RelayManager.Instance != null) RelayManager.Instance.UpdatePlayerListUI();
    }

    private void OnPlayerNameChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal) => RelayManager.Instance.UpdatePlayerListUI();
    private void OnPlayerReadyChanged(bool oldVal, bool newVal) => RelayManager.Instance.UpdatePlayerListUI();
    private void ApplyColor(Color32 targetColor) 
    { 
        if (bodyRenderer != null) bodyRenderer.material.color = targetColor; 
    }

    [ServerRpc]
    private void SetPlayerDataServerRpc(string name, Color32 color) // HATAYI DÜZELTTİK: Color32 yaptık
    {
        playerName.Value = name;
        playerColor.Value = color;
    }

    [ServerRpc]
    public void ToggleReadyServerRpc() => isReady.Value = !isReady.Value;
}