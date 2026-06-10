using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Ses Kaynakları (Audio Sources)")]
    [SerializeField] private AudioSource musicSource; 
    [SerializeField] private AudioSource sfxSource;   

    [Header("Arka Plan Müzikleri")]
    public AudioClip menuMusic;
    [Tooltip("Oyun içinde çalacak alternatif müzikleri buraya ekleyin")]
    public AudioClip[] gameMusics; // TEKİL YERİNE DİZİ (ARRAY) YAPTIK!

    [Header("Ses Efektleri (SFX)")]
    public AudioClip punchSound;
    public AudioClip deathSound;
    public AudioClip buttonClickSound;

    private int lastPlayedMusicIndex = -1; // Üst üste aynı müziğin çalmasını önlemek için

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
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
}