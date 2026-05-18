using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using System;

public class WeaponSpawner : MonoBehaviour
{
    public static WeaponSpawner Instance;

    [Header("Bat Prefab")]
    [SerializeField] private GameObject batPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int   batCount       = 2;
    [SerializeField] private float spawnHeight    = 1.4f;
    [SerializeField] private float spacingBetween = 0.8f;

    private NetworkRunner _runner;
    private bool          _hasSpawned = false;

    void Awake()
    {
        Instance = this;
    }

    public void SpawnWeapons(Vector3 qrPosition,
                              Quaternion qrRotation)
    {
        if (_hasSpawned) return;
        StartCoroutine(SpawnRoutine(
            qrPosition, qrRotation));
    }

    IEnumerator SpawnRoutine(Vector3 qrPos,
                              Quaternion qrRot)
    {
        // Wait for runner
        yield return new WaitUntil(() => {
            _runner =
                FindFirstObjectByType<NetworkRunner>();
            return _runner != null && _runner.IsRunning;
        });

        // Wait for role
        yield return new WaitUntil(() =>
            _runner.IsServer || _runner.IsClient);

        bool canSpawn = _runner.IsServer ||
                        _runner.IsSharedModeMasterClient;

        Debug.Log($"WeaponSpawner - " +
                  $"CanSpawn: {canSpawn}");

        if (!canSpawn)
        {
            Debug.Log("Client — waiting for bats");
            yield break;
        }

        if (batPrefab == null)
        {
            Debug.LogError("Bat prefab not assigned!");
            yield break;
        }

        NetworkObject netObj = batPrefab
            .GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError(
                "Bat prefab missing NetworkObject!");
            yield break;
        }

        _hasSpawned = true;

        // Calculate wall positions from QR
        Vector3 wallNormal = qrRot * Vector3.forward;
        Vector3 wallRight  = qrRot * Vector3.right;

        float totalWidth  = (batCount - 1) *
                             spacingBetween;
        float startOffset = -totalWidth / 2f;

        for (int i = 0; i < batCount; i++)
        {
            float offset = startOffset +
                           i * spacingBetween;

            Vector3 spawnPos = qrPos +
                wallRight  * offset +
                Vector3.up * (spawnHeight -
                              qrPos.y) +
                wallNormal * 0.05f;

            Quaternion spawnRot =
                Quaternion.LookRotation(wallNormal);

            Debug.Log($"Spawning bat {i + 1} " +
                      $"at {spawnPos}");

            NetworkObject spawned = _runner.Spawn(
                netObj,
                spawnPos,
                spawnRot);

            if (spawned != null)
            {
                Debug.Log($"Bat {i + 1} spawned " +
                          $"successfully!");
            }
            else
            {
                Debug.LogError(
                    $"Bat {i + 1} spawn failed!");
            }

            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("All bats spawned!");
    }
}