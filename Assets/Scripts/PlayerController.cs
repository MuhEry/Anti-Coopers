using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerController : NetworkBehaviour
{
    [Header("Hareket Ayarları")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private MeshRenderer bodyRenderer;
    [SerializeField] private TMP_Text nameTagText; // Karakterin kafasındaki yazı nesnesi

    // Ağda oyuncunun ismini ve rengini taşımak için senkronize değişkenler
    public NetworkVariable<FixedString32Bytes> networkPlayerName = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color> networkPlayerColor = new NetworkVariable<Color>(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Rigidbody rb;
    private Vector2 rawInput;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // Değerler ağda değiştikçe görseli güncelle
        networkPlayerName.OnValueChanged += (oldV, newV) => UpdateNameTag(newV.ToString());
        networkPlayerColor.OnValueChanged += (oldV, newV) => UpdateColor(newV);

        if (IsOwner)
        {
            // Lobi yöneticisinden kendi ismimizi çekip sunucuya gönderiyoruz
            string localName = RelayManager.Instance != null ? RelayManager.Instance.LocalProfileName : "Player";
            Color randomColor = new Color(Random.value, Random.value, Random.value);
            
            SetPlayerDataServerRpc(localName, randomColor);
        }
        else
        {
            // Diğer oyuncuların mevcut verilerini uygula
            UpdateNameTag(networkPlayerName.Value.ToString());
            UpdateColor(networkPlayerColor.Value);
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Yeni Input System girdileri
        if (Keyboard.current != null)
        {
            float moveX = 0f;
            float moveZ = 0f;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveZ = 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveZ = -1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX = -1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX = 1f;

            rawInput = new Vector2(moveX, moveZ).normalized;
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        Vector3 moveInput = new Vector3(rawInput.x, 0f, rawInput.y);
        Vector3 targetVelocity = moveInput * moveSpeed;
        
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);

        if (moveInput != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveInput);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f));
        }
    }

    private void UpdateNameTag(string name)
    {
        if (nameTagText != null) nameTagText.text = name;
    }

    private void UpdateColor(Color color)
    {
        if (bodyRenderer != null) bodyRenderer.material.color = color;
    }

    [ServerRpc]
    private void SetPlayerDataServerRpc(string name, Color color)
    {
        networkPlayerName.Value = name;
        networkPlayerColor.Value = color;
    }

    public override void OnNetworkDespawn()
    {
        networkPlayerName.OnValueChanged -= (oldV, newV) => UpdateNameTag(newV.ToString());
        networkPlayerColor.OnValueChanged -= (oldV, newV) => UpdateColor(newV);
    }
}