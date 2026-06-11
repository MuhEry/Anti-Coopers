using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PauseMenuManager : MonoBehaviour
{
    [Header("UI Panelleri")]
    [Tooltip("Açılıp kapanacak olan asıl menü paneli (Arka plan, butonlar vs.)")]
    [SerializeField] private GameObject pausePanel;
    [Header("Ses Arayüz Elemanları")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    private bool isMenuOpen = false;

    private void Start()
    {
        // Oyun başladığında menünün kesinlikle kapalı olduğundan emin ol
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        // Sahne ilk açıldığında, hiyerarşideki AudioManager'ı otomatik olarak buluyoruz!
        if (AudioManager.Instance != null)
        {
            // 1. Slider'ların OnValueChanged olaylarını kod üzerinden (dinamik) bağlıyoruz
            if (musicSlider != null)
            {
                // Önce eski eventleri temizle (çakışma olmasın)
                musicSlider.onValueChanged.RemoveAllListeners();
                // AudioManager'daki SetMusicVolume fonksiyonunu slider'a bağlıyoruz
                musicSlider.onValueChanged.AddListener(AudioManager.Instance.SetMusicVolume);
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.onValueChanged.AddListener(AudioManager.Instance.SetSFXVolume);
            }
        }
        else
        {
            Debug.LogError("[PauseMenu] Sahnede AudioManager bulunamadı!");
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