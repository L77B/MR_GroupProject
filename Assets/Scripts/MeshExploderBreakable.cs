using UnityEngine;
using Fusion;
using SBS.ME;
using System;

public class MeshExploderBreakable : NetworkBehaviour
{
    [Networked] private NetworkBool IsBroken
        { get; set; }

    private MeshExploder _exploder;
    private Collider      _collider;

    // Event for BreakableSpawner to subscribe to
    public event Action<GameObject> OnBroken;

    public override void Spawned()
    {
        _exploder = GetComponent<MeshExploder>();
        _collider = GetComponent<Collider>();

        if (_exploder == null)
            Debug.LogError(
                "MeshExploder not found!");

        // Wire explosion finished event
        if (_exploder != null)
            _exploder.onExplosionFinished
                .AddListener(OnExplosionFinished);
    }

    // Called when bat hits this object
    public void TakeHit(int playerIndex = 0, float force = 0f, float swingSpeed = 0f)
    {
        if (IsBroken) return;

        // Compute rage gain the same way DestructibleObject does for a break
        float gain = force * 0.25f + swingSpeed * 0.8f + 8f;
        NetworkedRageState.Instance?.RPC_AddRage(playerIndex, gain);

        RPC_Explode();
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_Explode()
    {
        if (IsBroken) return;
        IsBroken = true;

        Debug.Log($"{gameObject.name} exploding!");

        // Disable collider so no more hits
        if (_collider != null)
            _collider.enabled = false;

        // Trigger mesh explosion
        if (_exploder != null)
            _exploder.EXPLODE();
    }

    void OnExplosionFinished()
    {
        // Notify BreakableSpawner
        OnBroken?.Invoke(gameObject);

        // Despawn from network
        if (Object.HasStateAuthority)
            StartCoroutine(DespawnAfterDelay());
    }

    System.Collections.IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        if (Object != null && Object.IsValid)
            Runner.Despawn(Object);
    }

    public override void Despawned(
        NetworkRunner runner, bool hasState)
    {
        if (_exploder != null)
            _exploder.onExplosionFinished
                .RemoveListener(OnExplosionFinished);
    }
}