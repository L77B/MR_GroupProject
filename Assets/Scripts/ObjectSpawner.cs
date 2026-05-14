using UnityEngine;
using System.Collections.Generic;

public class ObjectSpawner : MonoBehaviour
{
    public static ObjectSpawner Instance;

    [System.Serializable]
    public class SpawnData
    {
        public GameObject prefab;

        // LOCAL offset relative to QR
        public Vector3 localOffset;

        public Vector3 localRotation;
    }

    [Header("Objects To Spawn")]
    [SerializeField]
    private List<SpawnData> objects;

    void Awake()
    {
        Instance = this;
    }

    public void SpawnObjects()
    {
        if (QRManager.Instance == null)
        {
            Debug.LogError("QRManager missing");
            return;
        }

        Transform origin =
            QRManager.Instance.SharedOrigin;

        foreach (var item in objects)
        {
            if (item.prefab == null)
                continue;

            // Convert local offset to world
            Vector3 worldPos =
                origin.TransformPoint(
                    item.localOffset);

            Quaternion worldRot =
                origin.rotation *
                Quaternion.Euler(
                    item.localRotation);

            Instantiate(
                item.prefab,
                worldPos,
                worldRot);

            Debug.Log(
                $"Spawned {item.prefab.name}"
            );

            CreateDebugSphere(worldPos);
        }
    }

    void CreateDebugSphere(Vector3 pos)
    {
        GameObject sphere =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere);

        sphere.transform.position = pos;

        sphere.transform.localScale =
            Vector3.one * 0.1f;
    }
}