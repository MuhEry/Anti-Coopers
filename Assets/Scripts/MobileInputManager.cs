using UnityEngine;
using UnityEngine.SceneManagement;

public class MobileInputManager : MonoBehaviour
{
    public static MobileInputManager Instance { get; private set; }

    [Header("Mobil Kontrol Paneli")]
    [Tooltip("Joystick ve butonları içeren iç paneli buraya sürükleyin.")]
    [SerializeField] private GameObject controlsPanel;

    private void Awake()
    {
        // Sadece tek bir Mobil Menajer olmasına izin veriyoruz
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Sahneler arası geçişte yok olma
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Eğer Ana Menü sahnesindeysek mobil butonları gizle
        if (scene.name == "MainMenu")
        {
            if (controlsPanel != null) controlsPanel.SetActive(false);
        }
        // Eğer bir mini oyun haritasındaysak butonları göster
        else if (scene.name.StartsWith("MiniGame_") || scene.name == "GameScene")
        {
            // Editörde veya Mobil cihazlarda görünmesini tetikle
            #if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
            if (controlsPanel != null) controlsPanel.SetActive(true);
            #endif
        }
    }
}