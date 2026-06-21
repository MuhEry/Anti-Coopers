using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class NetworkMovingObstacle : NetworkBehaviour
{
    [Header("Rota Ayarları")]
    [Tooltip("Nesne başlangıç noktasından itibaren X, Y ve Z ekseninde ne kadar uzağa gitsin?")]
    [SerializeField] private Vector3 movementOffset = new Vector3(5f, 0f, 0f); 
    
    [Header("Hız Ayarları")]
    [SerializeField] private float speed = 1.5f;

    [Header("Hareket Tipi")]
    [Tooltip("İşaretliyse nesne yumuşak (yavaşlayarak) döner. İşaretli değilse robotik (sabit hızla) çarparak döner.")]
    [SerializeField] private bool useSmoothMovement = true;

    [Header("Başlangıç Gecikmesi")]
    [Tooltip("Oyun başladıktan kaç saniye sonra hareket etmeye başlasın? (Geri sayım zaten bekletiyorsa 0 bırakabilirsin)")]
    [SerializeField] private float startDelay = 0f;

    private Vector3 startPosition;
    private Rigidbody rb;
    private double movementStartTime = -1; // Henüz başlamadı sinyali

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; 
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        startPosition = transform.position;
    }

    private void FixedUpdate()
    {
        double currentTime = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening 
            ? NetworkManager.Singleton.ServerTime.Time 
            : Time.time;

        // 1. Oyun henüz başlamadıysa (geri sayım sürüyorsa) tamamen sabit dur
        if (BaseMinigameManager.ActiveMinigame != null && !BaseMinigameManager.ActiveMinigame.IsGameStarted)
        {
            movementStartTime = -1; // Sıfırla, oyun başlayınca tekrar zamanlasın
            rb.MovePosition(startPosition); // 🚀 GÜVENLİK: Geri sayım bitene kadar buraya çivile!
            return; // startPosition'da sabit kalır, hiçbir hesap yapılmaz
        }

        // 2. Oyun başladı ama bizim ekstra gecikmemiz varsa, başlangıç zamanını mühürle
        if (movementStartTime < 0)
        {
            movementStartTime = currentTime;
        }

        double elapsed = currentTime - movementStartTime;

        // 3. Gecikme süresi dolmadıysa hâlâ sabit dur
        if (elapsed < startDelay)
        {
            rb.MovePosition(startPosition);
            return;
        }

        // 4. Gecikme bittiyse normal hareket matematiğine geç (zamanı gecikmeden başlatarak)
        float effectiveTime = (float)(elapsed - startDelay);
        float movementPercent;

        if (useSmoothMovement)
        {
            movementPercent = (Mathf.Sin(effectiveTime * speed) + 1f) / 2f; 
        }
        else
        {
            movementPercent = Mathf.PingPong(effectiveTime * speed, 1f);
        }

        Vector3 targetPosition = Vector3.Lerp(startPosition, startPosition + movementOffset, movementPercent);
        rb.MovePosition(targetPosition);
    }
}