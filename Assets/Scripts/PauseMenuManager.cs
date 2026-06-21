using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PauseMenuManager : MonoBehaviour
{
    // YENİ: Diğer scriptlerin (örneğin kameranın) menünün açık olup olmadığını anlaması için
    public static bool IsPaused { get; private set; }

    [Header("UI Panelleri")]
    [Tooltip("Açılıp kapanacak olan asıl menü paneli (Arka plan, butonlar vs.)")]
    [SerializeField] private GameObject pausePanel;
    
    [Header("Ses Arayüz Elemanları")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    
    private bool isMenuOpen = false;

    private void Start()
    {
        IsPaused = false; // Sahne başlarken sıfırla

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (AudioManager.Instance != null)
        {
            if (musicSlider != null)
            {
                musicSlider.onValueChanged.RemoveAllListeners();
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
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        if (pausePanel == null) return;

        isMenuOpen = !isMenuOpen;
        IsPaused = isMenuOpen;
        pausePanel.SetActive(isMenuOpen);

        UpdateCursorState();
    }

    public void ResumeGame()
    {
        isMenuOpen = false;
        IsPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);

        UpdateCursorState();
    }

    public void ReturnToMainMenu()
    {
        IsPaused = false;
        
        // Ana menüye dönerken fareyi kesinlikle serbest bırak
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (RelayManager.Instance != null)
        {
            RelayManager.Instance.LeaveLobby();
        }
    }

    // YENİ: Fare imlecini menü durumuna göre kilitleyen veya serbest bırakan metot
    private void UpdateCursorState()
    {
        // Mobilde imleç yönetimine gerek yok
        if (Application.isMobilePlatform) return;

        if (IsPaused)
        {
            // Menü açıksa fareyi serbest bırak ve görünür yap (Butonlara tıklayabilmek için)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // Oyun devam ediyorsa fareyi ortaya kilitle ve gizle (Kamerayı çevirebilmek için)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDestroy()
    {
        IsPaused = false; // Obje silinirse güvenliğe al
    }
}