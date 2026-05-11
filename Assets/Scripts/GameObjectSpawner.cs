using UnityEngine;
using System.Collections.Generic;

public class GameObjectSpawner : MonoBehaviour
{
    public static GameObjectSpawner Instance;

    [Header("Weapon Prefabs (spawn on wall)")]
    [SerializeField] private List<WeaponSpawnData> weapons;

    [Header("Breakable Prefabs (spawn on floor)")]
    [SerializeField] private List<BreakableSpawnData> breakables;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SpawnAll() {
        SpawnWeapons();
        SpawnBreakables();
    }

    void SpawnWeapons() {
        foreach (var weapon in weapons) {
            if (weapon.prefab == null) continue;

            // Get wall position using offsets from QR anchor
            Vector3 spawnPos = QRAlignmentManager.Instance
                .GetWallPosition(weapon.rightOffset, weapon.upOffset);

            // Face outward from wall
            Quaternion spawnRot = QRAlignmentManager.Instance.AnchorRotation;

            GameObject spawned = Instantiate(
                weapon.prefab, spawnPos, spawnRot);

            // Add ObjectHanger to keep it on wall until grabbed
            ObjectHanger hanger = spawned.AddComponent<ObjectHanger>();
            hanger.Initialise(spawnPos, spawnRot);

            Debug.Log($"Spawned weapon: {weapon.prefab.name} " +
                      $"at wall position {spawnPos}");
        }
    }

    void SpawnBreakables() {
        foreach (var breakable in breakables) {
            if (breakable.prefab == null) continue;

            // Get floor position using offsets from QR anchor
            Vector3 spawnPos = QRAlignmentManager.Instance
                .GetFloorPosition(
                    breakable.rightOffset,
                    breakable.forwardOffset);

            Quaternion spawnRot = Quaternion.identity;

            Instantiate(breakable.prefab, spawnPos, spawnRot);

            Debug.Log($"Spawned breakable: {breakable.prefab.name} " +
                      $"at floor position {spawnPos}");
        }
    }

    // Draw spawn positions in Scene view for easy layout
    void OnDrawGizmos() {
        if (!showGizmos || !Application.isPlaying) return;
        if (QRAlignmentManager.Instance == null ||
            !QRAlignmentManager.Instance.IsCalibrated) return;

        // Weapon positions — blue spheres
        Gizmos.color = Color.blue;
        foreach (var weapon in weapons) {
            Vector3 pos = QRAlignmentManager.Instance
                .GetWallPosition(weapon.rightOffset, weapon.upOffset);
            Gizmos.DrawSphere(pos, 0.05f);
        }

        // Breakable positions — red spheres
        Gizmos.color = Color.red;
        foreach (var breakable in breakables) {
            Vector3 pos = QRAlignmentManager.Instance
                .GetFloorPosition(
                    breakable.rightOffset,
                    breakable.forwardOffset);
            Gizmos.DrawSphere(pos, 0.05f);
        }
    }
}

[System.Serializable]
public class WeaponSpawnData {
    public GameObject prefab;
    [Tooltip("Left/Right offset from QR code in metres")]
    public float rightOffset  = 0f;
    [Tooltip("Up/Down offset from QR code in metres")]
    public float upOffset     = 0f;
}

[System.Serializable]
public class BreakableSpawnData {
    public GameObject prefab;
    [Tooltip("Left/Right offset from QR anchor")]
    public float rightOffset   = 0f;
    [Tooltip("Forward offset from wall in metres")]
    public float forwardOffset = 1f;
}