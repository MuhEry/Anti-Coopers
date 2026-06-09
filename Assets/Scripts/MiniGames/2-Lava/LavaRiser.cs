using Unity.Netcode;
using UnityEngine;

public class LavaRiser : NetworkBehaviour
{
    [Header("Lav Ayarları")]
    [SerializeField] private float riseSpeed = 0.3f;
    [SerializeField] private float maxHeight = 15f;

    private NetworkVariable<float> lavaPosY = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float startY;

    public override void OnNetworkSpawn()
    {
        startY = transform.position.y;
        // Sadece sunucunun bu değişkene değer atamasına izin veriyoruz
        if (IsServer)
        {
            lavaPosY.Value = startY;
        }

        lavaPosY.OnValueChanged += (old, newY) =>
        {
            Vector3 pos = transform.position;
            pos.y = newY;
            transform.position = pos;
        };
    }

    private void Update()
    {
        if (!IsServer) return;
        if (MinigameLavaManager.Instance == null) return;
        if (!MinigameLavaManager.Instance.IsGameStarted) return;

        if (lavaPosY.Value < startY + maxHeight)
            lavaPosY.Value += riseSpeed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!other.CompareTag("Player")) return;

        NetworkObject netObj = other.GetComponent<NetworkObject>();
        if (netObj != null)
            MinigameLavaManager.Instance?.PlayerEliminated(netObj.OwnerClientId);
    }
}