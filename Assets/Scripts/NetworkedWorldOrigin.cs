using UnityEngine;
using Fusion;

public class NetworkedWorldOrigin : NetworkBehaviour
{
    public static NetworkedWorldOrigin Instance;

    [Networked] public Vector3    Origin   { get; set; }
    [Networked] public Quaternion Rotation { get; set; }
    [Networked] public NetworkBool IsSet   { get; set; }

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Origin   = Vector3.zero;
            Rotation = Quaternion.identity;
            IsSet    = false;
        }
    }

    // Called by host when QR is detected
    public void SetOrigin(Vector3 position,
                          Quaternion rotation)
    {
        if (!Object.HasStateAuthority) return;

        Origin   = position;
        Rotation = rotation;
        IsSet    = true;

        Debug.Log($"NetworkedWorldOrigin set: " +
                  $"{position}");
    }

    // Get wall position relative to QR
    public Vector3 GetWallPosition(
        float rightOffset, float upOffset)
    {
        Vector3 wallNormal =
            Rotation * Vector3.forward;
        Vector3 wallRight  =
            Rotation * Vector3.right;

        return Origin +
               wallRight  * rightOffset +
               Vector3.up * upOffset +
               wallNormal * 0.05f;
    }
}