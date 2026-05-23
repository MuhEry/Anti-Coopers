using Unity.Netcode;
using UnityEngine;

public class MinigameManager : NetworkBehaviour
{
    // Her oyuncunun ulong türündeki ID'sine karşılık gelen skor tablosu (Ağda senkronize)
    // Gerçek bir oyunda bunu ağ değişkenine bağlayacağız ama şimdilik döngüyü kuralım
    
    private void OnGUI()
    {
        // Sadece testi canlı görebilmemiz için ekranın sol üstüne geçici butonlar çiziyoruz
        if (!IsServer) return; // Sadece odayı kuran admin (Host) sonraki oyuna geçirebilir

        GUILayout.BeginArea(new Rect(20, 100, 250, 200));
        
        if (GUILayout.Button("Mini Oyunu Bitir (Sıradaki Map)", GUILayout.Height(40)))
        {
            // RelayManager'a "Sıradaki oyunu yükle" emrini veriyoruz
            if (RelayManager.Instance != null)
            {
                RelayManager.Instance.LoadNextMinigame();
            }
        }
        
        GUILayout.EndArea();
    }
}