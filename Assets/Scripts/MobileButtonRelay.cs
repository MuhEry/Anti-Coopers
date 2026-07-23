using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class MobileButtonRelay : MonoBehaviour
{
    [Header("Punch Ayarları")]
    [SerializeField] private Image punchCooldownImage;
    [SerializeField] private float punchCooldownDuration = 1.5f;

    private float nextPunchTime = 0f;
    private bool isJumpHeld = false;

    // Ekrana DOKUNULDUĞU AN çalışır
    public void PointerDownJump() => isJumpHeld = true;

    // Ekrandan PARMAK ÇEKİLDİĞİ AN çalışır
    public void PointerUpJump() => isJumpHeld = false;

    private void Update()
    {
        // Zıplama Mantığı
        if (isJumpHeld && NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (playerObj != null)
            {
                PlayerController pc = playerObj.GetComponent<PlayerController>();
                pc.OnMobileJumpHeld();
            }
        }

        // Punch Bekleme Efekti (Cooldown)
        if (punchCooldownImage != null)
        {
            if (Time.time < nextPunchTime)
            {
                // Butonu grileştir ve dolma efekti ver
                punchCooldownImage.fillAmount = 1f - ((nextPunchTime - Time.time) / punchCooldownDuration);
                punchCooldownImage.color = new Color(0.5f, 0.5f, 0.5f, 1f); 
            }
            else
            {
                // Butonu normal hale getir
                punchCooldownImage.fillAmount = 1f;
                punchCooldownImage.color = Color.white; 
            }
        }
    }

    public void ExecutePunch()
    {
        if (Time.time < nextPunchTime) return; // Süre dolmadıysa vurmayı engelle

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (playerObj != null)
            {
                PlayerController pc = playerObj.GetComponent<PlayerController>();
                pc.OnMobilePunchPressed();
                nextPunchTime = Time.time + punchCooldownDuration; // Süreyi başlat
            }
        }
    }
}