using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using TMPro;

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
    [SerializeField] private TMP_Text nameTagText;

    public NetworkVariable<FixedString32Bytes> networkPlayerName = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<Color> networkPlayerColor = new NetworkVariable<Color>(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Rigidbody rb;
    private bool isStunned = false;
    private float stunTimer = 0f;
    
    private float nextPunchTime = 0f; 
    private bool isGrounded = true;   
    private Transform cameraTransform;

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

        if (IsOwner)
        {
            var vCam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (vCam != null)
            {
                vCam.Follow = transform;
            }
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (MinigameRaceManager.Instance != null && !MinigameRaceManager.Instance.IsGameStarted)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            SetRunningStateServerRpc(false);
            return;
        }
        // Sersemleme Kontrolü
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                isStunned = false;
                SetStunStateServerRpc(false);
            }
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return; 
        }

        // Zemin Kontrolü
        if (groundCheckPoint != null)
        {
            isGrounded = Physics.CheckSphere(groundCheckPoint.position, groundCheckRadius, groundLayer);
        }
        
        // --- HAREKET GİRDİLERİ ---
        float moveX = 0f;
        float moveZ = 0f;

        // 1. Klavye (PC)
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveZ = 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveZ = -1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX = -1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX = 1f;

            if (keyboard.spaceKey.wasPressedThisFrame && isGrounded)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
                SetJumpTriggerServerRpc();
            }
        }

        // 2. Mobil Joystick
        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            Vector2 stickInput = UnityEngine.InputSystem.Gamepad.current.leftStick.ReadValue();
            if (stickInput.magnitude > 0.05f)
            {
                moveX = stickInput.x;
                moveZ = stickInput.y;
            }

            if (UnityEngine.InputSystem.Gamepad.current.buttonSouth.wasPressedThisFrame && isGrounded)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
                SetJumpTriggerServerRpc();
            }
        }

        Vector3 inputDir = new Vector3(moveX, 0f, moveZ);

        if (inputDir.magnitude > 0.05f)
        {
            inputDir.Normalize();
            
            // Kamerayı çalışma anında garantiye alıyoruz
            if (cameraTransform == null)
            {
                var tpCam = GetComponent<ThirdPersonCamera>();
                if (tpCam != null && tpCam.CameraTransform != null)
                    cameraTransform = tpCam.CameraTransform;
            }

            Vector3 moveDir = inputDir;

            if (cameraTransform != null)
            {
                Vector3 cameraForward = cameraTransform.forward;
                cameraForward.y = 0f;
                cameraForward.Normalize();

                Vector3 cameraRight = cameraTransform.right;
                cameraRight.y = 0f;
                cameraRight.Normalize();

                moveDir = (cameraForward * inputDir.z + cameraRight * inputDir.x).normalized;
            }

            // Fiziksel İlerleme
            rb.linearVelocity = new Vector3(moveDir.x * moveSpeed, rb.linearVelocity.y, moveDir.z * moveSpeed);
            
            // DÜZELTİLDİ: Karakter artık her karede bastığın yöne doğru pürüzsüzce yüzünü dönecek
            if (moveDir.magnitude > 0.05f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
            }
            
            SetRunningStateServerRpc(true);
        }
        else
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            SetRunningStateServerRpc(false);
        }

        // --- DÜZELTİLDİ: UI DOSTU YUMRUK SİSTEMİ ---
        bool punchPressed = false;
        
        // ÖNEMLİ: Eğer fare tıklaması bir UI butonunun üzerindeyse, PC mekaniği olan yumruğu tetikleme!
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                punchPressed = true;
            }
        }
            
        if (UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.buttonWest.wasPressedThisFrame)
        {
            punchPressed = true;
        }

        if (punchPressed && Time.time >= nextPunchTime)
        {
            PunchActionServerRpc();
            nextPunchTime = Time.time + punchCooldown; 
        }

        if (animator != null)
        {
            animator.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }

    private void UpdatePlayerVisuals()
    {
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
        if (IsOwner)
        {
            rb.AddForce(force, ForceMode.Impulse);
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
    public void OnMobileJumpPressed()
    {
        if (!IsOwner || isStunned) return;
        if (isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            SetJumpTriggerServerRpc();
        }
    }

    public void OnMobilePunchPressed()
    {
        if (!IsOwner || isStunned) return;
        if (Time.time >= nextPunchTime)
        {
            PunchActionServerRpc();
            nextPunchTime = Time.time + punchCooldown;
        }
    }
    public override void OnNetworkDespawn()
    {
        networkPlayerName.OnValueChanged -= (oldV, newV) => UpdatePlayerVisuals();
        networkPlayerColor.OnValueChanged -= (oldV, newV) => UpdatePlayerVisuals();
    }
}