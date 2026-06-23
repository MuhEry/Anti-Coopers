using Unity.Netcode;
using UnityEngine;

public class MobileButtonRelay : MonoBehaviour
{
    private bool isJumpHeld = false;

    // Ekrana DOKUNULDUĞU AN çalışır
    public void PointerDownJump() => isJumpHeld = true;

    // Ekrandan PARMAK ÇEKİLDİĞİ AN çalışır
    public void PointerUpJump() => isJumpHeld = false;

    private void Update()
    {
        // Basılı tutulduğu sürece oyuncuya zıplama emri gönder (Yere değer değmez zıplayacak)
        if (isJumpHeld && NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (playerObj != null)
            {
                PlayerController pc = playerObj.GetComponent<PlayerController>();
                pc.OnMobileJumpHeld();
            }
        }
    }

    public void ExecutePunch()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (playerObj != null)
            {
                PlayerController pc = playerObj.GetComponent<PlayerController>();
                pc.OnMobilePunchPressed();
            }
        }
    }
}