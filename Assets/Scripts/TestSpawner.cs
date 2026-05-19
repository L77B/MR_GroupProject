using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class TestSpawner : MonoBehaviour
{
    [SerializeField] private GameObject cubePrefab;
    [SerializeField] private int   cubeCount = 2;
    [SerializeField] private float spacing   = 0.8f;

    private bool _hasSpawned = false;

    public void SpawnCubes(Vector3 qrPosition,
                           Quaternion qrRotation)
    {
        Debug.Log("SpawnCubes called!");
        if (_hasSpawned) return;
        StartCoroutine(SpawnRoutine(
            qrPosition, qrRotation));
    }

    IEnumerator SpawnRoutine(Vector3 qrPos,
                              Quaternion qrRot)
    {
        // Wait for NetworkManager
        yield return new WaitUntil(() =>
            NetworkManager.Singleton != null);

        Debug.Log("NetworkManager found!");

        // Wait for connection
        yield return new WaitUntil(() =>
            NetworkManager.Singleton.IsHost ||
            NetworkManager.Singleton.IsClient ||
            NetworkManager.Singleton.IsServer);

        Debug.Log($"Connected!" +
                  $"\nIsHost: " +
                  $"{NetworkManager.Singleton.IsHost}" +
                  $"\nIsServer: " +
                  $"{NetworkManager.Singleton.IsServer}" +
                  $"\nIsClient: " +
                  $"{NetworkManager.Singleton.IsClient}");

        // Wait for session to stabilise
        yield return new WaitForSeconds(2f);

        bool canSpawn =
            NetworkManager.Singleton.IsHost ||
            NetworkManager.Singleton.IsServer;

        Debug.Log($"CanSpawn: {canSpawn}");

        if (!canSpawn)
        {
            Debug.Log("Client — waiting for host " +
                      "to spawn cubes");
            yield break;
        }

        if (cubePrefab == null)
        {
            Debug.LogError("Cube prefab not assigned!");
            yield break;
        }

        NetworkObject netObj = cubePrefab
            .GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError(
                "Cube prefab missing NetworkObject!");
            yield break;
        }

        _hasSpawned = true;

        for (int i = 0; i < cubeCount; i++)
        {
            // Spawn at QR position with offset
            Vector3 spawnPos = new Vector3(
                qrPos.x +
                    (i - (cubeCount - 1) / 2f) *
                    spacing,
                qrPos.y,
                qrPos.z);

            Debug.Log($"Spawning cube {i+1} " +
                      $"at {spawnPos}");

            // Instantiate then spawn over network
            GameObject instance = Instantiate(
                cubePrefab,
                spawnPos,
                Quaternion.identity);

            NetworkObject instanceNet =
                instance.GetComponent<NetworkObject>();

            if (instanceNet != null)
            {
                instanceNet.Spawn();
                Debug.Log($"Cube {i+1} spawned " +
                          $"successfully!");
            }
            else
            {
                Debug.LogError(
                    $"Cube {i+1} NetworkObject " +
                    "missing!");
                Destroy(instance);
            }

            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("All cubes spawned!");
    }
}