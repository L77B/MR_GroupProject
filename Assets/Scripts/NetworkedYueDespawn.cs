using System.Collections;
using UnityEngine;
using Fusion;

/// <summary>
/// Add to any spawnable prefab that uses YueDestructible.
/// When the local YueDestructible fires onObjectDestruct, asks StateAuthority
/// to Runner.Despawn the object so it disappears on ALL peers.
/// </summary>
public class NetworkedYueDespawn : NetworkBehaviour
{
    private YueDestructibles.YueDestructible _yue;

    public override void Spawned()
    {
        _yue = GetComponent<YueDestructibles.YueDestructible>();
        if (_yue != null)
            _yue.onObjectDestruct.AddListener(OnDestructed);
        else
            Debug.LogWarning($"[NetworkedYueDespawn] {gameObject.name}: no YueDestructible found.");
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_yue != null)
            _yue.onObjectDestruct.RemoveListener(OnDestructed);
    }

    private void OnDestructed()
    {
        RPC_RequestDespawn();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDespawn()
    {
        if (Object == null || !Object.IsValid) return;
        Debug.Log($"[NetworkedYueDespawn] Despawning {gameObject.name}");
        StartCoroutine(DespawnAfterDelay());
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(0.3f);
        if (Object != null && Object.IsValid)
            Runner.Despawn(Object);
    }
}
