using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Mikser Ayarları")] // YENİ
    [SerializeField] private AudioMixer audioMixer;

    [Header("Ses Kaynakları (Audio Sources)")]
    [SerializeField] private AudioSource musicSource; 
    [SerializeField] private AudioSource sfxSource;   
    [Header("Bomba Sesleri")]
    public AudioClip tickSound;
    public AudioClip explosionSound;
    [Header("Arka Plan Müzikleri")]
    public AudioClip menuMusic;
    [Tooltip("Oyun içinde çalacak alternatif müzikleri buraya ekleyin")]
    public AudioClip[] gameMusics; // TEKİL YERİNE DİZİ (ARRAY) YAPTIK!

    [Header("Ses Efektleri (SFX)")]
    public AudioClip punchHitSound;  // DEĞİŞTİ: Oturaklı, isabet eden yumruk sesi
    public AudioClip punchMissSound; // YENİ: Hafif, boşa sallanan rüzgar sesi
    public AudioClip deathSound;
    public AudioClip buttonClickSound;

    private int lastPlayedMusicIndex = -1; // Üst üste aynı müziğin çalmasını önlemek için

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name.StartsWith("MiniGame_") || scene.name == "GameScene")
            PlayRandomGameMusic();
        else if (scene.name == "MainMenu")
            PlayMusic(menuMusic);
    }

    private void Start()
    {
        PlayMusic(menuMusic);
    }

    private void Update()
    {
        // Eğer çalan müzik bittiyse ve lobi/oyun içindeysek otomatik yeni müzik seç
        if (!musicSource.isPlaying && musicSource.clip != menuMusic && gameMusics.Length > 0)
        {
            PlayRandomGameMusic();
        }
    }

    // Belirli bir müziği çalma fonksiyonu (Menü için)
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return; 

        musicSource.clip = clip;
        musicSource.Play();
    }

    // YENİ: Oyun müzikleri arasından rastgele ve benzersiz seçim yapan fonksiyon
    public void PlayRandomGameMusic()
    {
        if (gameMusics == null || gameMusics.Length == 0) return;

        // Eğer sadece 1 tane oyun müziği varsa direkt onu çal
        if (gameMusics.Length == 1)
        {
            PlayMusic(gameMusics[0]);
            return;
        }

        // Rastgele bir indeks seç, ama bir önceki çalanla AYNI OLMASIN
        int randomIndex = Random.Range(0, gameMusics.Length);
        while (randomIndex == lastPlayedMusicIndex)
        {
            randomIndex = Random.Range(0, gameMusics.Length);
        }

        // Seçilen indeksi hafızaya al ve müziği başlat
        lastPlayedMusicIndex = randomIndex;
        PlayMusic(gameMusics[randomIndex]);
    }

    // Anlık efekt çalma fonksiyonu
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }
    public void SetMusicVolume(float value)
    {
        // Slider 0-1 arası gelir ama mikser -80dB ile 20dB arası çalışır. 
        // Matematiksel olarak logaritmik çevrim yapıyoruz ki ses pürüzsüz azalsın.
        if (value <= 0)
        {
            audioMixer.SetFloat("MusicVol", -80f); // Sıfıra çekince sesi tamamen kes (Mute)
        }
        else
        {
            audioMixer.SetFloat("MusicVol", Mathf.Log10(value) * 20f);
        }
    }
    public void SetSFXVolume(float value)
    {
        if (value <= 0)
        {
            audioMixer.SetFloat("SFXVol", -80f);
        }
        else
        {
            audioMixer.SetFloat("SFXVol", Mathf.Log10(value) * 20f);
        }
    }
}