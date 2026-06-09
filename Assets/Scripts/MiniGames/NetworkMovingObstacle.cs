using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))] // Bu script eklendiğinde otomatik olarak Rigidbody de eklenir
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

    private Vector3 startPosition;
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Nesnenin yerçekimiyle düşmemesi ve fizik motoruyla değil bizim kodumuzla hareket etmesi için
        rb.isKinematic = true; 
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Ekran titremesini önler

        startPosition = transform.position;
    }

    private void FixedUpdate()
    {
        // 1. Ağın ortak saatini alıyoruz (Eğer ağa bağlı değilsek test için normal saati al)
        double currentTime = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening 
            ? NetworkManager.Singleton.ServerTime.Time 
            : Time.time;

        float movementPercent;

        // 2. Zamanı 0 ile 1 arasında gidip gelen bir değere dönüştürüyoruz
        if (useSmoothMovement)
        {
            // Sinüs dalgası (0 ile 1 arası): Uç noktalara gelince yavaşlar, ortaya gelince hızlanır
            movementPercent = (Mathf.Sin((float)currentTime * speed) + 1f) / 2f; 
        }
        else
        {
            // PingPong dalgası: Dümdüz gider, duvara çarpmış gibi aynı hızla geri döner
            movementPercent = Mathf.PingPong((float)currentTime * speed, 1f);
        }

        // 3. Hedef pozisyonu hesapla (Başlangıç noktası ile gidilecek maksimum nokta arası)
        Vector3 targetPosition = Vector3.Lerp(startPosition, startPosition + movementOffset, movementPercent);

        // 4. Fizik motorunu kullanarak nesneyi taşı
        // MovePosition kullanıyoruz ki eğer bir oyuncu üstüne çıkarsa onun altından kayıp gitmesin, oyuncuyu da taşısın!
        rb.MovePosition(targetPosition);
    }
}