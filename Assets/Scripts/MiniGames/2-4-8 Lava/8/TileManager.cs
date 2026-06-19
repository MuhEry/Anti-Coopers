// TileManager.cs — Sahnede TEK NetworkObject
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class TileManager : NetworkBehaviour
{
    public static TileManager Instance { get; private set; }

    [SerializeField] private CrumblingTile[] tiles;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // Inspector'da doldurulmadıysa otomatik bul
        if (tiles == null || tiles.Length == 0)
            tiles = FindObjectsByType<CrumblingTile>(FindObjectsSortMode.None);

        for (int i = 0; i < tiles.Length; i++)
            tiles[i].Init(i, this);
    }

    public void RequestCrumble(int tileIndex)
    {
        if (!IsServer) return;
        StartCrumbleClientRpc(tileIndex);
        StartCoroutine(CrumbleRoutine(tileIndex));
    }

    private IEnumerator CrumbleRoutine(int tileIndex)
    {
        yield return new WaitForSeconds(tiles[tileIndex].DelayBeforeCrumble);
        HideTileClientRpc(tileIndex);
    }

    [ClientRpc] private void StartCrumbleClientRpc(int i) => tiles[i].ShowWarning();
    [ClientRpc] private void HideTileClientRpc(int i) => tiles[i].Hide();

    public void ResetAllTiles()
    {
        if (!IsServer) return;
        ResetAllTilesClientRpc();
    }

    [ClientRpc]
    private void ResetAllTilesClientRpc()
    {
        foreach (var t in tiles) t.ResetLocal();
    }
}