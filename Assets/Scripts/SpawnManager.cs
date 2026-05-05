using UnityEngine;
using Meta.XR.MRUtilityKit;

public class SpawnManager : MonoBehaviour
{
    public FindSpawnPositions spawner;

    public GameObject[] spawnPrefabs;

    private int currentIndex = 0;

    public void SpawnNext()
    {
        if (spawnPrefabs.Length == 0) return;

        // Cycle index
        currentIndex = (currentIndex + 1) % spawnPrefabs.Length;

        // Remove old objects
        spawner.ClearSpawnedPrefabs();

        // Assign new prefab
        spawner.SpawnObject = spawnPrefabs[currentIndex];

        // Spawn again
        spawner.StartSpawn();
    }

    public void SpawnByIndex(int index)
    {
        if (index < 0 || index >= spawnPrefabs.Length) return;

        spawner.ClearSpawnedPrefabs();
        spawner.SpawnObject = spawnPrefabs[index];
        spawner.StartSpawn();

        currentIndex = index;
    }
}