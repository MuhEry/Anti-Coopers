using Unity.Netcode;
using UnityEngine;

public class MobileButtonRelay : MonoBehaviour
{
    // Mobil Zıplama Butonuna atanacak fonksiyon
    public void ExecuteJump()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            // Ağdaki taptaze doğmuş olan YEREL OYUNCU objesini bulur
            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (playerObj != null)
            {
                // Karaktere zıplama emrini gönderir
                playerObj.GetComponent<PlayerController>().OnMobileJumpPressed();
            }
        }
    }

    // Mobil Yumruk Butonuna atanacak fonksiyon
    public void ExecutePunch()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
        {
            var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (playerObj != null)
            {
                // Karaktere yumruk emrini gönderir
                playerObj.GetComponent<PlayerController>().OnMobilePunchPressed();
            }
        }
    }
}