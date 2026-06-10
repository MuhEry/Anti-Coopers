using Unity.Netcode;
using UnityEngine;

public abstract class BaseMinigameManager : NetworkBehaviour
{
    public static BaseMinigameManager ActiveMinigame { get; protected set; }
    public abstract bool IsGameStarted { get; }

    protected virtual void Awake() => ActiveMinigame = this;

    public virtual void OnPlayerHit(ulong attackerId, ulong victimId) { }

    public virtual void PlayerEliminated(ulong clientId) { }
    public virtual void PlayerFinished(ulong clientId) { }
}