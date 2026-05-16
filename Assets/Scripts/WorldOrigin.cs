using UnityEngine;

public class WorldOrigin : MonoBehaviour
{
    public static WorldOrigin Instance;

    public Transform Origin => transform;
    public bool IsSet { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetOrigin(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
        IsSet = true;
        Debug.Log($"WorldOrigin set at: {position}");
    }
}