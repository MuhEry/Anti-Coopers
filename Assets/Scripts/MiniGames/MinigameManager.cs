using Unity.Netcode;
using UnityEngine;

public class MinigameManager : NetworkBehaviour
{
    private void OnGUI()
    {
        // Ekran tasarımları bitene kadar test butonlarımızı OnGUI ile çizdiriyoruz
        if (!IsServer) return; 

        GUILayout.BeginArea(new Rect(20, 100, 300, 250));
        GUILayout.Label("=== ADMİN MİNİ OYUN PANELİ ===");

        // TEST PUANLAMA BUTONU
        if (GUILayout.Button("Rastgele Puan Dağıt (Skor Ekle)", GUILayout.Height(35)))
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                int randomPoints = Random.Range(5, 15);
                RelayManager.Instance.AddScore(client.ClientId, randomPoints);
            }
        }

        GUILayout.Space(10);

        // SIRADAKİ HARİTAYA GEÇİŞ BUTONU
        if (GUILayout.Button("Mini Oyunu Bitir ve Geç", GUILayout.Height(45)))
        {
            if (RelayManager.Instance != null)
            {
                RelayManager.Instance.LoadNextMinigame();
            }
        }
        
        GUILayout.EndArea();
    }
}