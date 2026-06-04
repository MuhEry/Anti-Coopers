using UnityEngine;

public class MobileControlSpawner : MonoBehaviour
{
    [Header("Mobil Kontrol Prefab'ı")]
    [SerializeField] private GameObject mobileControlsPrefab;

    [Header("Geliştirici Ayarı")]
    [SerializeField] private bool forceShowInEditor = false; // Editörde test edebilmek için hile butonu

    private void Awake()
    {
        // 1. Durum: Unity Editöründeyiz ama "Zorla Göster" tıkı açık (PC'de test etmek için)
        if (Application.isEditor && forceShowInEditor)
        {
            SpawnMobileControls();
            return;
        }

        // 2. Durum: Gerçek platform kontrolleri
        // Gerçek bir mobil cihazda (Android/iOS) veya WebGL üzerinden dokunmatik bir cihazda açıldıysa
        if (Application.isMobilePlatform || 
            SystemInfo.deviceType == DeviceType.Handheld)
        {
            SpawnMobileControls();
        }
        else
        {
            // Eğer PC/Mac veya konsol ise bu spawner nesnesi kendini yok eder, belleği yormaz
            Destroy(gameObject);
        }
    }

    private void SpawnMobileControls()
    {
        if (mobileControlsPrefab != null)
        {
            // Prefab'ı sahneye doğuruyoruz
            GameObject spawnedUI = Instantiate(mobileControlsPrefab);
            
            // Sahne değiştiğinde kontrollerin silinmemesini garantiye alıyoruz
            DontDestroyOnLoad(spawnedUI);
            
            Debug.Log("<color=cyan>[MobileSpawner]:</color> Mobil kontroller başarıyla sahneye yüklendi.");
        }
        else
        {
            Debug.LogError("<color=red>[MobileSpawner]:</color> Mobil Kontrol Prefab'ı slotu boş! Lütfen atama yapın.");
        }
    }
}