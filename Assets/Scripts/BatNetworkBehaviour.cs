using UnityEngine;
using Fusion;

public class BatNetworkBehaviour : NetworkBehaviour
{
    // Empty NetworkBehaviour just to register
    // the bat with Fusion's prefab system
    
    public override void Spawned()
    {
        Debug.Log($"Bat spawned on network: " + $"{gameObject.name}");
    }
}