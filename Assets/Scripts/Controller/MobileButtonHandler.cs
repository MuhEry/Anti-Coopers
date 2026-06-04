using UnityEngine;
using Unity.Netcode;

public class MobileButtonHandler : MonoBehaviour
{
    public enum ButtonType { Jump, Punch }
    [SerializeField] public ButtonType buttonType;

    // Button'ın OnClick() eventine bağla
    public void OnPressed()
    {
        if (NetworkManager.Singleton?.LocalClient?.PlayerObject == null) return;

        PlayerController pc = NetworkManager.Singleton.LocalClient.PlayerObject
                                .GetComponent<PlayerController>();
        if (pc == null) return;

        if (buttonType == ButtonType.Jump)  pc.OnMobileJumpPressed();
        else                                pc.OnMobilePunchPressed();
    }
}