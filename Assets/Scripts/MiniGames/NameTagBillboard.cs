using UnityEngine;

public class NameTagBillboard : MonoBehaviour
{
    private Camera targetCamera;

    private void LateUpdate()
    {
        // Her frame yerel kamerayı bul (multiplayer'da her client kendi kamerasını kullanır)
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        // Kameraya doğru bak ama sadece yatay eksen — eğilme olmaz
        Vector3 lookDir = transform.position - targetCamera.transform.position;
        lookDir.y = 0f;

        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }
}