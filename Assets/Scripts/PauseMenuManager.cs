using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI Panelleri")]
    [Tooltip("Açılıp kapanacak olan asıl menü paneli (Arka plan, butonlar vs.)")]
    [SerializeField] private GameObject pausePanel;

    private bool isMenuOpen = false;

    private void Start()
    {
        // Oyun başladığında menünün kesinlikle kapalı olduğundan emin ol
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    private void Update()
    {
        // PC'de ESC tuşuna basıldığında menüyü aç/kapat
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleMenu();
        }
    }

    // Bu fonksiyonu mobil cihazlardaki bir 'Ayarlar/Duraklat' butonuna atayacağız
    public void ToggleMenu()
    {
        if (pausePanel == null) return;

        isMenuOpen = !isMenuOpen;
        pausePanel.SetActive(isMenuOpen);

        // Not: Multiplayer oyunlarda Time.timeScale = 0f YAPILMAZ! 
        // Çünkü ağ bağlantısı ve diğer oyuncular durdurulamaz. 
        // Sadece menü görsel olarak açılır.
    }

    // 'Devam Et' butonuna atanacak fonksiyon
    public void ResumeGame()
    {
        isMenuOpen = false;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    // 'Ana Menüye Dön' butonuna atanacak fonksiyon
    public void ReturnToMainMenu()
    {
        // RelayManager'daki güvenli çıkış fonksiyonumuzu çağırıyoruz
        if (RelayManager.Instance != null)
        {
            RelayManager.Instance.LeaveLobby();
        }
    }
}