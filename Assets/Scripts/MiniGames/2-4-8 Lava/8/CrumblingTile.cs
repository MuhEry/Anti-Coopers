using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class CrumblingTile : NetworkBehaviour
{
    [Header("Zaman Ayarları")]
    [SerializeField] private float delayBeforeCrumble = 0.6f; // Basıldıktan kaç sn sonra yok olsun?
    
    private MeshRenderer meshRenderer;
    private BoxCollider boxCollider;
    private bool isSteppedOn = false;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        boxCollider = GetComponent<BoxCollider>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Sadece sunucu fizik tetiklenmesini hesaplar
        if (!IsServer) return;

        if (collision.gameObject.CompareTag("Player") && !isSteppedOn)
        {
            isSteppedOn = true;
            StartCrumbleClientRpc();
            StartCoroutine(CrumbleRoutine());
        }
    }

    [ClientRpc]
    private void StartCrumbleClientRpc()
    {
        // Oyuncuya karonun kırılacağını belli etmek için rengini kırmızıya boyuyoruz (Görsel Uyarı)
        if (meshRenderer != null) meshRenderer.material.color = Color.red;
        
        // İsteğe bağlı: Burada hafif bir sallanma efekti de verilebilir
    }

    private IEnumerator CrumbleRoutine()
    {
        yield return new WaitForSeconds(delayBeforeCrumble);
        
        // Karoyu ve katı Collider'ını ağdaki herkeste kapatıyoruz
        HideTileClientRpc();
    }

    [ClientRpc]
    private void HideTileClientRpc()
    {
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (boxCollider != null) boxCollider.enabled = false;
    }

    // Yeni sahne yüklendiğinde veya oyun sıfırlandığında çağrılır
    public void ResetTile()
    {
        isSteppedOn = false;
        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
            meshRenderer.material.color = Color.white; // Eski orijinal rengi
        }
        if (boxCollider != null) boxCollider.enabled = true;
    }
}