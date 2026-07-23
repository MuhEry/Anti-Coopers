using UnityEngine;
using UnityEngine.EventSystems;

public class MobileTouchpadCamera : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Hassasiyet Ayarı")]
    [SerializeField] private float XSensitivity = 0.2f;
    [SerializeField] private float YSensitivity = 0.2f;

    private ThirdPersonCamera thirdPersonCam;
    private int activePointerId = -1;
    private Vector2 lastPointerPosition;

    private ThirdPersonCamera FindLocalCamera()
    {
        foreach (var cam in FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None))
            if (cam.IsOwner) return cam;
        return null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != -1) return;

        if (thirdPersonCam == null)
            thirdPersonCam = FindLocalCamera();

        activePointerId = eventData.pointerId;
        lastPointerPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId || thirdPersonCam == null) return;

        Vector2 delta = eventData.position - lastPointerPosition;
        thirdPersonCam.AddCameraInput(delta.x * XSensitivity, delta.y * YSensitivity);
        lastPointerPosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointerId)
            activePointerId = -1;
    }

    private void OnEnable()
    {
        activePointerId = -1;
    }

    private void OnDisable()
    {
        activePointerId = -1;
    }
}