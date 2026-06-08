using Unity.Netcode;
using UnityEngine;

// DİKKAT: Bu soyut (abstract) bir sınıftır. Tek başına sahneye eklenemez.
// Sadece diğer Manager'ların ondan miras alması (türemesi) için bir şablondur.
public abstract class BaseMinigameManager : NetworkBehaviour
{
    // Sahnedeki o anki aktif oyun yöneticisini tutacak statik değişken
    public static BaseMinigameManager ActiveMinigame { get; protected set; }

    // Her oyunun kendine has "oyun başladı mı?" sorusunun ortak şablonu
    public abstract bool IsGameStarted { get; }

    protected virtual void Awake()
    {
        // Hangi harita yüklenirse yüklensin, o haritanın yöneticisi kendini "Aktif" olarak kaydeder
        ActiveMinigame = this;
    }
}