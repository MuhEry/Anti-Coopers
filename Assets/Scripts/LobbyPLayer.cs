using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class LobbyPlayer : NetworkBehaviour
{
    private SkinnedMeshRenderer[] bodyRenderers; 

    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color32> playerColor = new NetworkVariable<Color32>(new Color32(255, 255, 255, 255), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // YENİ: Hangi koltukta oturduğunu takip eden ağ değişkeni
    public NetworkVariable<int> slotIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly string[] allowedLobbyColors = new string[]
    {
        "#000000", "#FFFFFF", "#00FFFF", "#FF0000", "#970094", "#0000FF", "#007300", "#FFFF00" 
    };

    public override void OnNetworkSpawn()
    {
        bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        playerName.OnValueChanged += OnPlayerNameChanged;
        isReady.OnValueChanged += OnPlayerReadyChanged;
        playerColor.OnValueChanged += OnPlayerColorInternalChanged;
        slotIndex.OnValueChanged += OnSlotIndexChanged; // YENİ: Koltuk değişirse konumu güncelle

        if (IsServer)
        {
            // Sunucu, yeni gelen oyuncuya RelayManager'dan boş bir koltuk ister
            slotIndex.Value = RelayManager.Instance.AssignSlot(OwnerClientId);
        }

        // Oyuncuyu ilk baştaki pozisyonuna (koltuğuna) yerleştir
        UpdatePosition(slotIndex.Value);

        if (IsOwner)
        {
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
    public void SelectColorServerRpc(Color32 chosenColor)
    {
        // 1. Oyuncunun ağ üzerindeki rengini güncelle
        playerColor.Value = chosenColor;

        // 2. SİHİRLİ DOKUNUŞ: Oyuncu renk değiştirdiği için "Hazır" durumunu bozuyoruz!
        isReady.Value = false;

        // 3. Odadaki tüm listeyi anında tazelemek için RelayManager'ı tetikliyoruz
        if (RelayManager.Instance != null)
        {
            RelayManager.Instance.UpdatePlayerListUI();
        }
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
        slotIndex.OnValueChanged -= OnSlotIndexChanged;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;

        if (RelayManager.Instance != null) RelayManager.Instance.UpdatePlayerListUI();
    }

    private void OnPlayerNameChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal) => RelayManager.Instance.UpdatePlayerListUI();
    private void OnPlayerReadyChanged(bool oldVal, bool newVal) => RelayManager.Instance.UpdatePlayerListUI();
    private void OnPlayerColorInternalChanged(Color32 oldVal, Color32 newVal) => ApplyColor(newVal);
    
    // YENİ: Ağda koltuk verisi geldiğinde oyuncuyu pürüzsüzce o koltuğa ışınla
    private void OnSlotIndexChanged(int oldVal, int newVal) => UpdatePosition(newVal);

    private void UpdatePosition(int slot)
    {
        // 2.5 birim aralıklarla sırayla dizer (İstediğin gibi ayarlayabilirsin)
        transform.position = new Vector3(slot * 2.5f, 1.0f, 0f);
    }

    private void ApplyColor(Color32 targetColor) 
    { 
        if (bodyRenderers == null || bodyRenderers.Length == 0)
        {
            bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }

        foreach (var renderer in bodyRenderers)
        {
            if (renderer != null)
            {
                renderer.material.color = targetColor;
            }
        }
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