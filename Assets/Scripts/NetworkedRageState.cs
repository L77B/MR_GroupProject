using UnityEngine;
using Fusion;

/// <summary>
/// Single authoritative source of rage values and player identity for both peers.
///
/// IMPORTANT: This must be a SCENE OBJECT in the Xage scene, NOT a spawned prefab.
/// Add it to the scene hierarchy with a NetworkObject component. Fusion networks
/// scene objects automatically as soon as StartGame completes — no host spawn needed.
///
/// FLOW
/// ────
/// - StateAuthority (first joiner) runs decay in FixedUpdateNetwork.
/// - Each peer calls RPC_RegisterPlayer after joining so P1Player / P2Player are set.
/// - BatImpactHandler uses GetPlayerIndex(runner.LocalPlayer) instead of IsSharedModeMasterClient.
/// - Any peer receiving an ESP32 button calls RPC_BroadcastButtonPress (All→All).
/// - WebSocketSceneClient subscribes to OnButtonPressed / OnExplosionTriggered.
/// </summary>
public class NetworkedRageState : NetworkBehaviour
{
    public static NetworkedRageState Instance;

    // ── Static events fired after RPC delivery ───────────────────────────────
    /// <summary>Fires only on StateAuthority (master client). WebSocketSceneClient spawns objects here.</summary>
    public static event System.Action<int> OnSpawnRequested;
    /// <summary>Fires on ALL peers. WebSocketSceneClient plays explosion effect here.</summary>
    public static event System.Action      OnExplosionTriggered;

    [Header("Rage Settings")]
    [SerializeField] private float maxRage        = 100f;
    [SerializeField] private float decayPerSecond = 1f;

    // ── Networked State ──────────────────────────────────────────────────────
    [Networked] public float     RageP1   { get; set; }
    [Networked] public float     RageP2   { get; set; }
    [Networked] public PlayerRef P1Player { get; set; }
    [Networked] public PlayerRef P2Player { get; set; }

    public float MaxRage  => maxRage;
    public bool  IsSpawned { get; private set; }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Set Instance immediately so coroutines waiting on it don't time out
        // if Spawned() fires after the first poll.
        Instance = this;
    }

    public override void Spawned()
    {
        Instance   = this;
        IsSpawned  = true;

        if (HasStateAuthority)
        {
            RageP1   = 0f;
            RageP2   = 0f;
            P1Player = PlayerRef.None;
            P2Player = PlayerRef.None;
        }

        Debug.Log("[NetworkedRageState] Spawned — " +
                  $"HasStateAuthority: {HasStateAuthority}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        float dt = Runner.DeltaTime;
        if (RageP1 > 0f) RageP1 = Mathf.Max(0f, RageP1 - decayPerSecond * dt);
        if (RageP2 > 0f) RageP2 = Mathf.Max(0f, RageP2 - decayPerSecond * dt);
    }

    private void OnDestroy()
    {
        if (Instance == this) { Instance = null; IsSpawned = false; }
    }

    // ── Player Registration ──────────────────────────────────────────────────

    /// <summary>
    /// Each peer calls this once after joining. StateAuthority assigns P1 / P2 slots.
    /// P1 = first registrant (host / master client), P2 = second.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RegisterPlayer(PlayerRef player)
    {
        if (P1Player == PlayerRef.None)
        {
            P1Player = player;
            Debug.Log($"[NetworkedRageState] P1 registered: {player}");
        }
        else if (P2Player == PlayerRef.None && player != P1Player)
        {
            P2Player = player;
            Debug.Log($"[NetworkedRageState] P2 registered: {player}");
        }
    }

    /// <summary>
    /// Returns 0 (P1) or 1 (P2) for the given PlayerRef.
    /// Falls back to IsSharedModeMasterClient if registration has not yet propagated.
    /// </summary>
    public int GetPlayerIndex(PlayerRef player)
    {
        if (P1Player != PlayerRef.None && player == P1Player) return 0;
        if (P2Player != PlayerRef.None && player == P2Player) return 1;
        return (Runner != null && Runner.IsSharedModeMasterClient) ? 0 : 1;
    }

    // ── Spawn / Explosion RPCs ───────────────────────────────────────────────

    // Both headsets receive ESP32 WebSocket messages and both send this RPC.
    // The cooldown deduplicates the two near-simultaneous calls so only one spawn fires.
    private readonly float[] _lastSpawnTime = { -10f, -10f };
    private const float SpawnDedup = 1f;

    /// <summary>
    /// Any peer calls this (the one with the ESP32 connection).
    /// Only fires on StateAuthority (master client) which then spawns the objects.
    /// This avoids the race condition of waiting for All→All broadcasts.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSpawn(int buttonIndex)
    {
        if ((uint)buttonIndex > 1) return;
        if (Time.time - _lastSpawnTime[buttonIndex] < SpawnDedup) return;
        _lastSpawnTime[buttonIndex] = Time.time;
        Debug.Log($"[NetworkedRageState] RPC_RequestSpawn({buttonIndex}) on authority");
        OnSpawnRequested?.Invoke(buttonIndex);
    }

    /// <summary>Broadcast the main explosion event to all peers.</summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_BroadcastExplosion()
    {
        Debug.Log("[NetworkedRageState] Explosion → all peers");
        OnExplosionTriggered?.Invoke();
    }

    // ── Rage RPCs ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called on the hitting headset. Delivered to StateAuthority which writes
    /// the [Networked] value and Fusion propagates it to all clients.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AddRage(int playerIndex, float gain)
    {
        if (playerIndex == 0)
            RageP1 = Mathf.Clamp(RageP1 + gain, 0f, maxRage);
        else
            RageP2 = Mathf.Clamp(RageP2 + gain, 0f, maxRage);

        Debug.Log($"[NetworkedRageState] P{playerIndex + 1} +{gain:F1} " +
                  $"→ P1:{RageP1:F1} P2:{RageP2:F1}");
    }
}
