using UnityEngine;

public class ColorTile : MonoBehaviour
{
    [Header("Karo Ayarları")]
    public string tileColorName; // "Red", "Blue", "Green", "Yellow" şeklinde editörden yazacağız
    
    private MeshRenderer meshRenderer;
    private BoxCollider boxCollider;
    private Vector3 originalPosition;
    private bool isDropped = false;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        boxCollider = GetComponent<BoxCollider>();
        originalPosition = transform.position;
    }

    // Doğru renk değilse karoyu gizler/düşürür
    public void DropTile()
    {
        if (isDropped) return;
        isDropped = true;

        // En performanslı mobil yöntem: Karoyu ve collider'ını anında kapatmak
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (boxCollider != null) boxCollider.enabled = false;
    }

    // Yeni tur başladığında karoyu eski haline getirir
    public void ResetTile()
    {
        isDropped = false;
        if (meshRenderer != null) meshRenderer.enabled = true;
        if (boxCollider != null) boxCollider.enabled = true;
        transform.position = originalPosition;
    }
}