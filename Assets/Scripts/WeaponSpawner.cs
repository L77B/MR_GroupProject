using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;

public class WeaponSpawner : MonoBehaviour
{
    public static WeaponSpawner Instance;

    [Header("Bat Prefab")]
    [SerializeField] private GameObject batPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int   batCount       = 2;
    [SerializeField] private float spawnHeight    = 1.4f;
    [SerializeField] private float spacingBetween = 0.8f;

    [Header("Debug")]
    [SerializeField] private TMP_Text debugText;

    private NetworkRunner _runner;
    private bool          _hasSpawned = false;

    void Awake()
    {
        Instance = this;
    }

    public void SpawnWeapons(Vector3 qrPosition,
                              Quaternion qrRotation)
    {
        UpdateDebug("SpawnWeapons called!");
        if (_hasSpawned)
        {
            UpdateDebug("Already spawned — skipping");
            return;
        }
        StartCoroutine(SpawnRoutine(
            qrPosition, qrRotation));
    }

    IEnumerator SpawnRoutine(Vector3 qrPos,
                              Quaternion qrRot)
    {
        UpdateDebug("SpawnRoutine started!\n" +
                    $"Bat prefab null: " +
                    $"{batPrefab == null}");

        // Wait for runner
        yield return new WaitUntil(() => {
            _runner =
                FindFirstObjectByType<NetworkRunner>();
            return _runner != null && _runner.IsRunning;
        });

        UpdateDebug("Runner found!\n" +
                    $"IsServer: {_runner.IsServer}\n" +
                    $"IsClient: {_runner.IsClient}");

        // Wait for role
        yield return new WaitUntil(() =>
            _runner.IsServer || _runner.IsClient);

        yield return new WaitForSeconds(2f);

        bool canSpawn = _runner.IsServer ||
                        _runner.IsSharedModeMasterClient;

        UpdateDebug($"CanSpawn: {canSpawn}\n" +
                    $"IsServer: {_runner.IsServer}\n" +
                    $"IsMaster: " +
                    $"{_runner.IsSharedModeMasterClient}");

        if (!canSpawn)
        {
            UpdateDebug("Client — waiting for bats");
            yield break;
        }

        if (batPrefab == null)
        {
            UpdateDebug("ERROR: Bat prefab null!");
            yield break;
        }

        NetworkObject netObj = batPrefab
            .GetComponent<NetworkObject>();

        if (netObj == null)
        {
            UpdateDebug("ERROR: No NetworkObject " +
                        "on bat prefab!");
            yield break;
        }

        UpdateDebug($"Ready to spawn {batCount} bats!\n" +
                    $"NetObj: {netObj.name}");

        _hasSpawned = true;

        Vector3 wallNormal = qrRot * Vector3.forward;
        Vector3 wallRight  = qrRot * Vector3.right;

        float totalWidth  = (batCount - 1) *
                             spacingBetween;
        float startOffset = -totalWidth / 2f;

        for (int i = 0; i < batCount; i++)
        {
            float offset = startOffset +
                           i * spacingBetween;

            Vector3 spawnPos = new Vector3(
                qrPos.x + wallRight.x * offset,
                spawnHeight,
                qrPos.z + wallRight.z * offset)
                + wallNormal * 0.1f;

            Quaternion spawnRot =
                Quaternion.LookRotation(wallNormal);

            UpdateDebug($"Spawning bat {i+1}/{batCount}\n" +
                        $"at {spawnPos}");

            NetworkObject spawned = _runner.Spawn(
                netObj,
                spawnPos,
                spawnRot);

            if (spawned != null)
            {
                UpdateDebug($"Bat {i+1} spawned! ✓\n" +
                            $"at {spawnPos}");
                Debug.Log($"Bat {i+1} spawned!");
            }
            else
            {
                UpdateDebug($"ERROR: Bat {i+1} " +
                            "spawn failed!\n" +
                            "Check Fusion config");
                Debug.LogError(
                    $"Bat {i+1} spawn failed!");
            }

            yield return new WaitForSeconds(0.1f);
        }

        UpdateDebug($"All {batCount} bats spawned! ✓");
        Debug.Log("All bats spawned!");
    }

    void UpdateDebug(string msg)
    {
        Debug.Log($"[WeaponSpawner] {msg}");
        if (debugText != null)
            debugText.text = msg;
    }
}