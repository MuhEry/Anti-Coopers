using Unity.Netcode;
using UnityEngine;

// DİKKAT: Bu soyut (abstract) bir sınıftır. Tek başına sahneye eklenemez.
// Sadece diğer Manager'ların ondan miras alması (türemesi) için bir şablondur.
public abstract class BaseMinigameManager : NetworkBehaviour
{
    public static BaseMinigameManager ActiveMinigame { get; protected set; }
    public abstract bool IsGameStarted { get; }

    protected virtual void Awake() => ActiveMinigame = this;

    // YENİ: Yumruk atıldığında çalışacak ortak fonksiyon
    public virtual void OnPlayerHit(ulong attackerId, ulong victimId) { }
}