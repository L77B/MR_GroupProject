using UnityEngine;
using System.Collections;
using TMPro;
using Meta.XR.MRUtilityKit;

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

    private bool _hasSpawned = false;

    void Awake()
    {
        Instance = this;
    }

    public void SpawnWeapons(Vector3 qrPosition,
                              Quaternion qrRotation)
    {
        if (_hasSpawned) return;
        _hasSpawned = true;
        StartCoroutine(SpawnRoutine(
            qrPosition, qrRotation));
    }

    IEnumerator SpawnRoutine(Vector3 qrPos,
                              Quaternion qrRot)
    {
        UpdateDebug("Spawning local weapons...");

        if (batPrefab == null)
        {
            UpdateDebug("ERROR: Bat prefab null!");
            yield break;
        }

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

            // Simple local instantiate - no networking
            GameObject bat = Instantiate(
                batPrefab, spawnPos, spawnRot);

            if (bat != null)
            {
                Debug.Log($"Bat {i+1} spawned locally!");
                UpdateDebug($"Bat {i+1} spawned ✓");
            }

            yield return new WaitForSeconds(0.1f);
        }

        UpdateDebug($"All {batCount} bats ready!");
    }

    void UpdateDebug(string msg)
    {
        Debug.Log($"[WeaponSpawner] {msg}");
        if (debugText != null)
            debugText.text = msg;
    }
}