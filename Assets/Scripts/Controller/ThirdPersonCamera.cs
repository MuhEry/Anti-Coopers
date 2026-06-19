using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class ThirdPersonCamera : NetworkBehaviour
{
    [Header("Hedef")]
    [SerializeField] private Transform followTarget;

    [Header("Kamera Mesafe ve Yükseklik")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float heightOffset = 1.5f;

    [Header("PC Mouse Hassasiyeti")]
    [SerializeField] private float mouseSensitivityX = 0.2f;
    [SerializeField] private float mouseSensitivityY = 0.2f;

    [Header("Dikey Açı Limitleri")]
    [SerializeField] private float minVerticalAngle = -20f;
    [SerializeField] private float maxVerticalAngle = 60f;

    [Header("Kamera Çarpışma")]
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private LayerMask collisionMask;

    private Camera cam;
    public Transform CameraTransform => cam != null ? cam.transform : null;
    private float yaw;
    private float pitch = 15f;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            Camera[] cams = GetComponentsInChildren<Camera>();
            foreach (var c in cams) c.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        cam = GetComponentInChildren<Camera>(true);
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

        yaw = transform.eulerAngles.y;

        if (!IsMobilePlatform())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (followTarget == null) followTarget = transform;
    }

    private void LateUpdate()
    {
        if (!IsOwner || cam == null || followTarget == null) return;

        // Mobilde MobileTouchpadCamera kamerayı döndürüyor
        if (!IsMobilePlatform())
            HandleMouseInput();

        ApplyCameraPosition();
    }

    private void HandleMouseInput()
    {
        if (Mouse.current == null) return;
        Vector2 delta = Mouse.current.delta.ReadValue();
        yaw   += delta.x * mouseSensitivityX;
        pitch -= delta.y * mouseSensitivityY;
        pitch  = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
    }

    // MobileTouchpadCamera tarafından çağrılır
    public void AddCameraInput(float deltaX, float deltaY)
    {
        yaw   += deltaX;
        pitch -= deltaY;
        pitch  = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
    }

    private void ApplyCameraPosition()
    {
        Vector3 pivotPoint = followTarget.position + Vector3.up * heightOffset;
        Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = pivotPoint + camRotation * new Vector3(0f, 0f, -distance);

        Vector3 finalPosition = desiredPosition;
        Vector3 dir = (desiredPosition - pivotPoint).normalized;
        float dist = Vector3.Distance(pivotPoint, desiredPosition);

        if (Physics.SphereCast(pivotPoint, collisionRadius, dir, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
            finalPosition = hit.point + hit.normal * collisionRadius;

        cam.transform.position = finalPosition;
        cam.transform.LookAt(pivotPoint);
    }

    private bool IsMobilePlatform()
    {
        #if UNITY_EDITOR
        return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS
            || UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android;
        #else
        return Application.isMobilePlatform;
        #endif
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && !IsMobilePlatform())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}