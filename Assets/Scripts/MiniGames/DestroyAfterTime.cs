using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    public float timeToDestroy = 5f;
    private void Start()
    {
        // Oyun sahnesi yüklendikten tam 5 saniye sonra bu bariyeri yok et!
        // (Bizim lobi geri sayım süremizle birebir eşit)
        Destroy(gameObject, timeToDestroy);
    }
}