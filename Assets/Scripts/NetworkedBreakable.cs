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
    private BreakableObject    _breakable;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void Spawned()
    {
        _destructible = GetComponent<DestructibleObject>();
        if (_destructible != null)
            _destructible.OnBroken += OnLocalBroken;

        _breakable = GetComponent<BreakableObject>();
        if (_breakable != null)
            _breakable.OnBroken += OnLegacyBroken;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_destructible != null)
            _destructible.OnBroken -= OnLocalBroken;
        if (_breakable != null)
            _breakable.OnBroken -= OnLegacyBroken;
    }

    // ── Break Handling ────────────────────────────────────────────────────────

    private void OnLocalBroken(GameObject obj)
    {
        OnBroken?.Invoke(gameObject);
        RPC_RequestDespawn();
    }

    private void OnLegacyBroken()
    {
        OnBroken?.Invoke(gameObject);
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
