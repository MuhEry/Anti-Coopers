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
        if (controlsPanel == null) return;

        // 🚀 ESNEK ÇÖZÜM: Sahne adlarını tek tek kontrol etmek yerine, 
        // butonların KESİNLİKLE OLMAMASI gereken sahneleri (Menü ve Skor Tablosu) hariç tutuyoruz.
        if (scene.name == "MainMenu" || scene.name == "ScoreboardScene")
        {
            controlsPanel.SetActive(false);
        }
        else
        {
            // Menü ve Skor Tablosu dışındaki tüm oyun sahnelerinde (Bomba, Lav, Taç vb.) paneli aç
            #if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
                controlsPanel.SetActive(true);
            #endif
        }
    }
}