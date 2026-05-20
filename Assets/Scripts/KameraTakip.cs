using UnityEngine;

public class KameraTakip : MonoBehaviour
{
    private Transform mainCameraTransform;

    private void Start()
    {
        // Sahnedeki ana kameranın transform bileşenini buluyoruz
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (mainCameraTransform != null)
        {
            // Objeyi (Canvas'ı) her zaman kameranın baktığı yöne çeviriyoruz
            transform.LookAt(transform.position + mainCameraTransform.forward);
        }
    }
}