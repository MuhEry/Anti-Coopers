using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class ThirdPersonCamera : NetworkBehaviour
{
    [Header("Hedef")]
    [SerializeField] private Transform followTarget;

    [Header("Kamera Mesafe ve Yükseklik")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float heightOffset = 1.5f;

    [Header("Mouse Hassasiyeti")]
    [SerializeField] private float mouseSensitivityX = 0.2f;
    [SerializeField] private float mouseSensitivityY = 0.2f;

    [Header("Dikey Açı Limitleri")]
    [SerializeField] private float minVerticalAngle = -20f;
    [SerializeField] private float maxVerticalAngle = 60f;

    [Header("Kamera Çarpışma")]
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private LayerMask collisionMask;

    private Camera cam;
    private float yaw;
    private float pitch = 15f;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Bu oyuncuya ait olmayan kameraları kapat
            Camera[] cams = GetComponentsInChildren<Camera>();
            foreach (var c in cams) c.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        cam = GetComponentInChildren<Camera>(true); // inactive olanı da bul
        if (cam != null)
        {
            cam.gameObject.SetActive(true);
        }
        else
        {
            GameObject camObj = new GameObject("PlayerCamera");
            camObj.transform.SetParent(transform);
            cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }

        // Başlangıç yaw'ını karakterin yönüne eşitle
        yaw = transform.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (followTarget == null) followTarget = transform;
    }

    private void LateUpdate()
    {
        if (!IsOwner || cam == null || followTarget == null) return;

        // Yeni Input System ile mouse delta
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // Karakterin dönüşünden BAĞIMSIZ — sadece mouse birikimli açı
        yaw   += mouseDelta.x * mouseSensitivityX;
        pitch -= mouseDelta.y * mouseSensitivityY;
        pitch  = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);

        // Pivot nokta (karakter pozisyonu değişse de yaw/pitch sabit kalır)
        Vector3 pivotPoint = followTarget.position + Vector3.up * heightOffset;

        // Kamera pozisyonunu yaw+pitch'e göre hesapla (karakterin rotasyonunu hiç kullanma)
        Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredOffset   = camRotation * new Vector3(0f, 0f, -distance);
        Vector3 desiredPosition = pivotPoint + desiredOffset;

        // Çarpışma kontrolü
        Vector3 finalPosition = desiredPosition;
        Vector3 dir = (desiredPosition - pivotPoint).normalized;
        float   dist = Vector3.Distance(pivotPoint, desiredPosition);

        if (Physics.SphereCast(pivotPoint, collisionRadius, dir, out RaycastHit hit, dist, collisionMask))
        {
            finalPosition = hit.point + hit.normal * collisionRadius;
        }

        cam.transform.position = finalPosition;
        cam.transform.LookAt(pivotPoint);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}