using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Hareket Ayarları")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float jumpForce = 6f; // YENİ: Zıplama gücü
    [SerializeField] private Animator animator;

    [Header("Vurma Ayarları")]
    [SerializeField] private Transform punchPoint; 
    [SerializeField] private float punchRadius = 1.2f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float punchCooldown = 1.5f; // YENİ: Yumruk atma aralığı (1.5 saniye)

    [Header("Zemin Kontrolü")]
    [SerializeField] private Transform groundCheckPoint; // YENİ: Karakterin ayak ucuna koyulacak nokta
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask groundLayer; // Zemin katmanı

    // Network Değişkenleri
    public NetworkVariable<FixedString32Bytes> networkPlayerName = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color> networkPlayerColor = new NetworkVariable<Color>(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Rigidbody rb;
    private bool isStunned = false;
    private float stunTimer = 0f;
    
    private float nextPunchTime = 0f; // YENİ: Bir sonraki yumruk atılabilecek zamanı tutar
    private bool isGrounded = true;   // YENİ: Zeminde miyiz?

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        
        // Rigidbody'nin takla atmasını engelliyoruz (Sadece kodla dönecek)
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        }

        // Modelin alt nesnesinin lokal rotasyonunu oyun başlarken donduruyoruz
        if (animator != null)
        {
            animator.transform.localRotation = Quaternion.Euler(0, 0, 0); 
            // Buradaki -23f değerini karakterin oyunda tam düz bakacağı açıya göre ince ayar yapabilirsin.
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

        // Sersemleme Kontrolü
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

        // YENİ: Zemin Kontrolü (Ayaklarımız yere basıyor mu?)
        if (groundCheckPoint != null)
        {
            isGrounded = Physics.CheckSphere(groundCheckPoint.position, groundCheckRadius, groundLayer);
        }
        
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            
            // Zıplama Girişi
            if (keyboard.spaceKey.wasPressedThisFrame && isGrounded)
            {
                // Fiziksel Zıplama
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
                
                // YENİ: Animasyonu Ağda Tetikle
                SetJumpTriggerServerRpc();
            }
        }
        // Input System Hareket Girdileri
        float moveX = 0f;
        float moveZ = 0f;

        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveZ = 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveZ = -1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX = -1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX = 1f;

            // YENİ: Zıplama Aksiyonu (Space'e basıldıysa ve yerdeysek)
            if (keyboard.spaceKey.wasPressedThisFrame && isGrounded)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
                // NOT: Eğer asset içinde zıplama animasyonun varsa buraya tetikleyici ekleyebiliriz
            }
        }

        Vector3 moveDir = new Vector3(moveX, 0f, moveZ).normalized;

        if (moveDir.magnitude > 0.1f)
        {
            // DÜZELTİLDİ: Karakter sadece hız vektörüne göre değil, bastığın yöne (moveDir) anlık bakar
            rb.linearVelocity = new Vector3(moveDir.x * moveSpeed, rb.linearVelocity.y, moveDir.z * moveSpeed);
            
            // Gövdeyi pürüzsüzce koşulan yöne doğru çeviriyoruz
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
            
            SetRunningStateServerRpc(true);
        }
        else
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            SetRunningStateServerRpc(false);
        }

        // DÜZELTİLDİ: Yumruk Atma Şartı + Cooldown Süresi Kontrolü
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (Time.time >= nextPunchTime)
            {
                PunchActionServerRpc();
                nextPunchTime = Time.time + punchCooldown; // Süreyi ileri atıyoruz
            }
        }
    }

    private void UpdatePlayerVisuals()
    {
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
    private void SetJumpTriggerServerRpc()
    {
        // Sunucu bu emri alır ve tüm istemcilere (Clients) iletir
        SetJumpTriggerClientRpc();
    }

    [ClientRpc]
    private void SetJumpTriggerClientRpc()
    {
        // Herkesin ekranında bu karakterin zıplama animasyonu oynar
        if (animator != null)
        {
            animator.SetTrigger("Jump");
        }
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
                knockbackDir.y = 0.3f; // Sektirmeyi belirginleştirdik
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

        // YENİ: Editörde zemin kontrol küresini çizdiriyoruz
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