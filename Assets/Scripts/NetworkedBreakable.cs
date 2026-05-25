using UnityEngine;
using Fusion;

/// <summary>
/// NetworkBehaviour wrapper around DestructibleObject.
/// When any peer's bat breaks this object, an RPC notifies StateAuthority
/// which calls Runner.Despawn — removing the object from ALL peers.
/// </summary>
public class NetworkedBreakable : NetworkBehaviour
{
    public System.Action<GameObject> OnBroken;

    private DestructibleObject _destructible;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void Spawned()
    {
        _destructible = GetComponent<DestructibleObject>();
        if (_destructible != null)
            _destructible.OnBroken += OnLocalBroken;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_destructible != null)
            _destructible.OnBroken -= OnLocalBroken;
    }

    // ── Break Handling ────────────────────────────────────────────────────────

    private void OnLocalBroken(GameObject obj)
    {
        OnBroken?.Invoke(gameObject);

        // Any peer that detects a break asks StateAuthority to despawn.
        // Despawn is authoritative — it removes the NetworkObject on all peers.
        RPC_RequestDespawn();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDespawn()
    {
        if (Object == null || !Object.IsValid) return;
        Debug.Log($"[NetworkedBreakable] Despawning {gameObject.name}");
        Runner.Despawn(Object);
    }
}
