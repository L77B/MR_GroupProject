using UnityEngine;
using Meta.XR.MRUtilityKit;

public class SpawnManager : MonoBehaviour
{
    public FindSpawnPositions spawner;
    public GameObject[] spawnPrefabs;

    private int currentIndex = -1;

    public void SpawnNext()
    {
        if (!IsReady()) return;

        currentIndex = (currentIndex + 1) % spawnPrefabs.Length;
        SpawnByIndex(currentIndex);
    }

    public void SpawnByIndex(int index)
    {
        if (!IsReady()) return;

        if (index < 0 || index >= spawnPrefabs.Length)
        {
            Debug.LogWarning("Invalid prefab index.");
            return;
        }

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();

        if (room == null)
        {
            Debug.LogWarning("No MRUK room found yet. Wait until scene data is loaded.");
            return;
        }

        spawner.ClearSpawnedPrefabs();
        spawner.SpawnObject = spawnPrefabs[index];
        spawner.StartSpawn(room);

        currentIndex = index;
    }

    private bool IsReady()
    {
        if (spawner == null)
        {
            Debug.LogWarning("Spawner is not assigned.");
            return false;
        }

        if (spawnPrefabs == null || spawnPrefabs.Length == 0)
        {
            Debug.LogWarning("No spawn prefabs assigned.");
            return false;
        }

        if (MRUK.Instance == null)
        {
            Debug.LogWarning("MRUK instance not found.");
            return false;
        }

        return true;
    }
}