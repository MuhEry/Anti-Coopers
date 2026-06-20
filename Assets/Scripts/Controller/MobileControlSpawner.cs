using UnityEngine;

public class MobileControlSpawner : MonoBehaviour
{
    [Header("Mobil Kontrol Prefab'ı")]
    [SerializeField] private GameObject mobileControlsPrefab;

    [Header("Geliştirici Ayarı")]
    [SerializeField] private bool forceShowInEditor = false;

    // Statik bayrak — oyun ömrü boyunca sadece BİR KEZ spawn olur
    private static bool hasSpawned = false;

    private void Awake()
    {
        if (hasSpawned)
        {
            Destroy(gameObject);
            return;
        }

        if (Application.isEditor && forceShowInEditor)
        {
            SpawnMobileControls();
            return;
        }

        if (Application.isMobilePlatform || 
            SystemInfo.deviceType == DeviceType.Handheld)
        {
            SpawnMobileControls();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SpawnMobileControls()
    {
        if (mobileControlsPrefab != null)
        {
            GameObject spawnedUI = Instantiate(mobileControlsPrefab);
            DontDestroyOnLoad(spawnedUI);

            hasSpawned = true; // Mühürle — bir daha asla spawn olmasın

            Debug.Log("<color=cyan>[MobileSpawner]:</color> Mobil kontroller başarıyla sahneye yüklendi.");
        }
        else
        {
            Debug.LogError("<color=red>[MobileSpawner]:</color> Mobil Kontrol Prefab'ı slotu boş!");
        }
    }
}