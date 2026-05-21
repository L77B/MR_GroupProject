using UnityEngine;

public class WorldOrigin : MonoBehaviour
{
    public static WorldOrigin Instance;

    public bool IsSet { get; private set; }
    public Transform Origin => transform;

    void Awake()
    {
        Instance = this;
    }

    public void SetOrigin(Vector3 position,
                          Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
        IsSet = true;
        Debug.Log($"WorldOrigin set: {position}");
    }
}