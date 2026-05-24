using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour
{
    [Header("Görsel Bileşenler")]
    [SerializeField] private MeshRenderer bodyRenderer; 

    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color32> playerColor = new NetworkVariable<Color32>(new Color32(255, 255, 255, 255), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // İSTEDİĞİN SÜRPRİZ OLMAYAN 8 SABİT RENK HAVUZU
    private readonly string[] allowedLobbyColors = new string[]
    {
        "#000000", // Siyah
        "#FFFFFF", // Beyaz
        "#00FFFF", // Turkuaz
        "#FF0000", // Kırmızı
        "#970094", // Mor
        "#0000FF", // Mavi
        "#007300", // Yeşil
        "#FFFF00"  // Sarı
    };

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnPlayerNameChanged;
        isReady.OnValueChanged += OnPlayerReadyChanged;
        playerColor.OnValueChanged += OnPlayerColorInternalChanged;

        transform.position = new Vector3(OwnerClientId * 2.0f, 1.0f, 0f);

        if (IsOwner)
        {
            // İlk girişte elindeki 8 renkten rastgele birini seçiyoruz
            string randomHex = allowedLobbyColors[Random.Range(0, allowedLobbyColors.Length)];
            Color32 defaultColor = Color.white;
            if (ColorUtility.TryParseHtmlString(randomHex, out Color parsedColor))
            {
                defaultColor = parsedColor;
            }

            SetPlayerDataServerRpc(RelayManager.Instance.LocalProfileName, defaultColor);
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
        playerColor.OnValueChanged -= OnPlayerColorInternalChanged;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;

        if (RelayManager.Instance != null) RelayManager.Instance.UpdatePlayerListUI();
    }

    private void OnPlayerNameChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal) => RelayManager.Instance.UpdatePlayerListUI();
    private void OnPlayerReadyChanged(bool oldVal, bool newVal) => RelayManager.Instance.UpdatePlayerListUI();
    private void OnPlayerColorInternalChanged(Color32 oldVal, Color32 newVal) => ApplyColor(newVal);

    private void ApplyColor(Color32 targetColor) 
    { 
        if (bodyRenderer != null) bodyRenderer.material.color = targetColor; 
    }

    [ServerRpc]
    private void SetPlayerDataServerRpc(string name, Color32 color) 
    {
        playerName.Value = name;
        playerColor.Value = color;
    }

    [ServerRpc]
    public void ToggleReadyServerRpc() => isReady.Value = !isReady.Value;
}