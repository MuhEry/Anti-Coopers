using UnityEngine;

public class SpawnPointManager : MonoBehaviour
{
    public static SpawnPointManager Instance { get; private set; }

    [SerializeField] private Transform[] spawnPoints;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public Vector3 GetSpawnPosition(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
            return spawnPoints[index % spawnPoints.Length].position;

        return new Vector3(index * 2.5f, 1f, 0f); // Fallback
    }
}