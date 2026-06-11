using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class CrumblingTile : NetworkBehaviour // KURAL: NetworkBehaviour olmalı!
{
    [Header("Zaman Ayarları")]
    [SerializeField] private float delayBeforeCrumble = 0.7f; 
    
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
        // Sadece sunucu fizik işlemlerine karar verir
        if (!IsServer) return;

        // Karakterin Tag'inin "Player" olduğundan emin ol!
        if (collision.gameObject.CompareTag("Player") && !isSteppedOn)
        {
            isSteppedOn = true;
            
            // Tüm istemcilerde karonun rengini kırmızı yap
            StartCrumbleClientRpc();
            
            // Sunucuda kırılma zamanlamasını başlat
            StartCoroutine(CrumbleRoutine());
        }
    }

    [ClientRpc]
    private void StartCrumbleClientRpc()
    {
        if (meshRenderer != null) meshRenderer.material.color = Color.red;
    }

    private IEnumerator CrumbleRoutine()
    {
        yield return new WaitForSeconds(delayBeforeCrumble);
        
        // Karoyu tamamen gizle ve collider'ını kapat
        HideTileClientRpc();
    }

    [ClientRpc]
    private void HideTileClientRpc()
    {
        if (meshRenderer != null) meshRenderer.enabled = false;
        if (boxCollider != null) boxCollider.enabled = false;
    }

    public void ResetTile()
    {
        isSteppedOn = false;
        if (IsServer)
        {
            // Eğer harita sıfırlanacaksa tüm ağda geri açma emri gönderilebilir
            ResetTileClientRpc();
        }
    }

    [ClientRpc]
    private void ResetTileClientRpc()
    {
        isSteppedOn = false;
        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
            meshRenderer.material.color = Color.white; 
        }
        if (boxCollider != null) boxCollider.enabled = true;
    }
}