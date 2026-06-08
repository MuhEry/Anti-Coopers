// Sahnedeki TÜM butonların OnClick'i bunu çağırır
// Böylece hangi RelayManager instance'ı yaşıyorsa onu bulur
using UnityEngine;

public class UIButtonRelay : MonoBehaviour
{
    public void CreateRelay()  => RelayManager.Instance?.CreateRelay();
    public void JoinRelay()    => RelayManager.Instance?.JoinRelay();
    public void OnReadyClicked()    => RelayManager.Instance?.OnReadyClicked();
    public void OnStartGameClicked() => RelayManager.Instance?.OnStartGameClicked();
    public void LeaveLobby()   => RelayManager.Instance?.LeaveLobby();
    public void AddMapToPlaylist()  => RelayManager.Instance?.AddMapToPlaylist();
    public void ClearPlaylist() => RelayManager.Instance?.ClearPlaylist();

    public void ChangeMyColor(string colorHex) => RelayManager.Instance?.ChangeMyColor(colorHex);
}