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
        pausePanel.SetActive(isMenuOpen);
    }

    public void ResumeGame()
    {
        isMenuOpen = false;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    public void ReturnToMainMenu()
    {
        if (RelayManager.Instance != null)
        {
            RelayManager.Instance.LeaveLobby();
        }
    }
}