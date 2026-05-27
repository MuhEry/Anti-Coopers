using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using TMPro; // YENİ: TextMeshPro bileşenini kullanabilmek için ekledik

public class PlayerController : NetworkBehaviour
{
    [Header("Hareket Ayarları")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float jumpForce = 6f; 
    [SerializeField] private Animator animator;

    [Header("Vurma Ayarları")]
    [SerializeField] private Transform punchPoint; 
    [SerializeField] private float punchRadius = 1.2f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float punchCooldown = 1.5f; 

    [Header("Zemin Kontrolü")]
    [SerializeField] private Transform groundCheckPoint; 
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask groundLayer; 

    [Header("UI Ayarları")]
    [SerializeField] private TMP_Text nameTagText; // YENİ: Başın üzerindeki metin kutusu slotu

    // Network Değişkenleri
    public NetworkVariable<FixedString32Bytes> networkPlayerName = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color> networkPlayerColor = new NetworkVariable<Color>(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Rigidbody rb;
    private bool isStunned = false;
    private float stunTimer = 0f;
    
    private float nextPunchTime = 0f; 
    private bool isGrounded = true;   

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }

        if (animator != null)
        {
            animator.transform.localRotation = Quaternion.Euler(0, 0, 0); 
        }
    }

    public override void OnNetworkSpawn()
    {
        networkPlayerName.OnValueChanged += (oldV, newV) => UpdatePlayerVisuals();
        networkPlayerColor.OnValueChanged += (oldV, newV) => UpdatePlayerVisuals();
        UpdatePlayerVisuals();
        
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                SetStunStateServerRpc(false);
            }
            return; 
        }

        if (groundCheckPoint != null)
        {
            isGrounded = Physics.CheckSphere(groundCheckPoint.position, groundCheckRadius, groundLayer);
        }
        
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            
            if (keyboard.spaceKey.wasPressedThisFrame && isGrounded)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
                SetJumpTriggerServerRpc();
            }
        }

        float moveX = 0f;
float moveZ = 0f;

if (UnityEngine.InputSystem.Keyboard.current != null)
{
    var keyboard = UnityEngine.InputSystem.Keyboard.current;

    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)    moveZ =  1f;
    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)  moveZ = -1f;
    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)  moveX = -1f;
    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX =  1f;
}

Vector3 inputDir = new Vector3(moveX, 0f, moveZ).normalized;

    if (inputDir.magnitude > 0.1f)
    {
        // Kameranın yatay (yaw) yönünü al — pitch olmadan
        Camera playerCam = GetComponentInChildren<Camera>();
        Vector3 camForward, camRight;

        if (playerCam != null)
        {
            // Kameranın yalnızca yatay bileşenini kullan
            camForward = Vector3.ProjectOnPlane(playerCam.transform.forward, Vector3.up).normalized;
            camRight   = Vector3.ProjectOnPlane(playerCam.transform.right,   Vector3.up).normalized;
        }
        else
        {
            // Kamera yoksa dünya yönlerini kullan
            camForward = Vector3.forward;
            camRight   = Vector3.right;
        }

        // Girdiyi kamera yönüne göre dönüştür
        Vector3 moveDir = (camForward * inputDir.z + camRight * inputDir.x).normalized;

        rb.linearVelocity = new Vector3(moveDir.x * moveSpeed, rb.linearVelocity.y, moveDir.z * moveSpeed);

        // Karakteri hareket yönüne döndür (kameradan bağımsız)
        Quaternion targetRotation = Quaternion.LookRotation(moveDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);

        SetRunningStateServerRpc(true);
    }
    else
    {
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        SetRunningStateServerRpc(false);
    }

        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (Time.time >= nextPunchTime)
            {
                PunchActionServerRpc();
                nextPunchTime = Time.time + punchCooldown; 
            }
        }
    }

    private void UpdatePlayerVisuals()
    {
        // YENİ: Lobiden gelen ağ ismini TextMeshPro alanına zorla yazdırıyoruz
        if (nameTagText != null)
        {
            nameTagText.text = networkPlayerName.Value.ToString();
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r.gameObject.name != "Text (TMP)") 
            {
                r.material.color = networkPlayerColor.Value;
            }
        }
    }

    [ServerRpc]
    private void SetJumpTriggerServerRpc() => SetJumpTriggerClientRpc();

    [ClientRpc]
    private void SetJumpTriggerClientRpc()
    {
        if (animator != null) animator.SetTrigger("Jump");
    }

    [ServerRpc]
    private void SetRunningStateServerRpc(bool running) => SetRunningStateClientRpc(running);

    [ClientRpc]
    private void SetRunningStateClientRpc(bool running)
    {
        if (animator != null) animator.SetBool("isRunning", running);
    }

    [ServerRpc]
    private void PunchActionServerRpc()
    {
        PunchActionClientRpc();
        Collider[] hitPlayers = Physics.OverlapSphere(punchPoint.position, punchRadius, playerLayer);
        foreach (Collider hit in hitPlayers)
        {
            if (hit.gameObject == gameObject) continue;

            PlayerController targetPlayer = hit.GetComponent<PlayerController>();
            if (targetPlayer != null)
            {
                Vector3 knockbackDir = (hit.transform.position - transform.position).normalized;
                knockbackDir.y = 0.3f; 
                targetPlayer.TakeHitServerRpc(knockbackDir * knockbackForce);
            }
        }
    }

    [ClientRpc]
    private void PunchActionClientRpc()
    {
        if (animator != null) animator.SetTrigger("Punch");
    }

    [ServerRpc(RequireOwnership = false)]
    public void TakeHitServerRpc(Vector3 force) => TakeHitClientRpc(force);

    [ClientRpc]
    private void TakeHitClientRpc(Vector3 force)
    {
        rb.AddForce(force, ForceMode.Impulse);
        if (IsOwner)
        {
            isStunned = true;
            stunTimer = 1.5f; 
            SetStunStateServerRpc(true);
        }
    }

    [ServerRpc]
    private void SetStunStateServerRpc(bool stunned) => SetStunStateClientRpc(stunned);

    [ClientRpc]
    private void SetStunStateClientRpc(bool stunned)
    {
        if (animator != null) animator.SetBool("isStunned", stunned);
    }

    private void OnDrawGizmosSelected()
    {
        if (punchPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(punchPoint.position, punchRadius);
        }

        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }

    public override void OnNetworkDespawn()
    {
        networkPlayerName.OnValueChanged -= (oldV, newV) => UpdatePlayerVisuals();
        networkPlayerColor.OnValueChanged -= (oldV, newV) => UpdatePlayerVisuals();
    }
}