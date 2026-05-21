using UnityEngine;
using Fusion;

public class NetworkedBreakable : MonoBehaviour
{
    private DestructibleObject destructible;
    private NetworkObject networkObject;

    void Start()
    {
        destructible  = GetComponent<DestructibleObject>();
        networkObject = GetComponent<NetworkObject>();

        if (destructible != null)
            destructible.OnBroken += OnLocalBroken;
    }

    void OnLocalBroken(GameObject obj)
    {
        if (networkObject == null) return;
        if (!networkObject.HasStateAuthority) return;

        // Find all clients and notify break
        Debug.Log($"{gameObject.name} broken — " +
                  $"notifying all clients");

        NetworkRunner runner =
            FindFirstObjectByType<NetworkRunner>();
        if (runner != null)
            runner.Despawn(networkObject);
    }

    void OnDestroy()
    {
        if (destructible != null)
            destructible.OnBroken -= OnLocalBroken;
    }
}