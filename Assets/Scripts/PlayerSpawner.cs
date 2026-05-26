using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections;
using System.Collections.Generic;
using System;

public class PlayerSpawner : MonoBehaviour,
                              INetworkRunnerCallbacks
{
    [SerializeField] private GameObject batPrefab;

    private NetworkRunner _runner;
    private Dictionary<PlayerRef, NetworkObject>
        _spawnedBats = new
            Dictionary<PlayerRef, NetworkObject>();

    void Start()
    {
        StartCoroutine(RegisterWithRunner());
    }

    IEnumerator RegisterWithRunner()
    {
        yield return new WaitUntil(() => {
            _runner = FindFirstObjectByType<NetworkRunner>();
            return _runner != null && _runner.IsRunning;
        });

        _runner.AddCallbacks(this);
        Debug.Log("PlayerSpawner registered with NetworkRunner");
    }

    public void OnPlayerJoined(
        NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player joined: {player}");

        if (!runner.IsSharedModeMasterClient)
            return;

        Vector3 spawnPos = Vector3.zero;
        if (WorldOrigin.Instance != null &&
            WorldOrigin.Instance.IsSet)
        {
            spawnPos = WorldOrigin.Instance
                .Origin.position;
            spawnPos += new Vector3(
                UnityEngine.Random.Range(-0.3f, 0.3f),
                1.4f,
                0.05f);
        }

        // Get NetworkObject from prefab
        NetworkObject netObj = batPrefab
            .GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("Bat prefab missing " +
                          "NetworkObject component!");
            return;
        }

        NetworkObject bat = runner.Spawn(
            netObj,
            spawnPos,
            Quaternion.identity,
            player);

        if (bat != null)
        {
            _spawnedBats[player] = bat;

            // Only equip on the peer that owns this bat. The client's bat is equipped
            // by BatImpactHandler.DetectPlayerIndex() after the session is ready.
            var handler = bat.GetComponent<BatImpactHandler>();
            if (handler != null && bat.HasInputAuthority)
                handler.SetEquipped(true);

            Debug.Log($"Bat spawned and equipped for: {player}");
        }
    }

    public void OnPlayerLeft(
        NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player left: {player}");

        if (_spawnedBats.TryGetValue(
            player, out var bat))
        {
            runner.Despawn(bat);
            _spawnedBats.Remove(player);
        }
    }

    // ── Required interface methods ────────────────────
    public void OnInput(NetworkRunner runner,
        NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner,
        PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner,
        ShutdownReason reason) { }
    public void OnConnectedToServer(
    NetworkRunner runner)
    {
        Debug.Log("Connected to server!");
    }

    public void OnDisconnectedFromServer(
    NetworkRunner runner,
    NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected: {reason}");
    }
    public void OnConnectRequest(
        NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token) { }
    public void OnConnectFailed(
        NetworkRunner runner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(
        NetworkRunner runner,
        SimulationMessagePtr message) { }
    public void OnSessionListUpdated(
        NetworkRunner runner,
        List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(
        NetworkRunner runner,
        Dictionary<string, object> data) { }
    public void OnHostMigration(
        NetworkRunner runner,
        HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        ArraySegment<byte> data) { }
    public void OnReliableDataProgress(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        float progress) { }
    public void OnSceneLoadDone(
        NetworkRunner runner) { }
    public void OnSceneLoadStart(
        NetworkRunner runner) { }
    public void OnObjectExitAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player) { }
    public void OnObjectEnterAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player) { }
}