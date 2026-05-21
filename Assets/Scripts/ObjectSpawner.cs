using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Meta.XR.MRUtilityKit;
using Fusion;

public class ObjectSpawner : MonoBehaviour
{
    public static ObjectSpawner Instance;

    [Header("Weapon Prefabs (wall)")]
    [SerializeField] private List<NetworkObject> weaponPrefabs;

    [Header("Breakable Prefabs (floor)")]
    [SerializeField] private List<NetworkObject> breakablePrefabs;

    [Header("Spawn Settings")]
    [SerializeField] private int   targetBreakableCount = 10;
    [SerializeField] private float wallSpawnHeight      = 1.4f;
    [SerializeField] private float minDistanceBetween   = 0.4f;
    [SerializeField] private int   maxSpawnAttempts     = 20;

    [Header("Debug")]
    [SerializeField] private TMP_Text debugText;

    private List<GameObject> activeBreakables =
        new List<GameObject>();

    private bool          _hasSpawned = false;
    private MRUKRoom      _room;
    private NetworkRunner _runner;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UpdateDebug("Ready.\nLook at QR code...");
    }

    public void SpawnAtPosition(Vector3 position,
                                 Quaternion rotation)
    {
        if (_hasSpawned) return;
        _hasSpawned = true;

        UpdateDebug("QR Detected!\nFinding room...");
        StartCoroutine(SpawnRoutine(position, rotation));
    }

    public void SpawnAll()
    {
        if (WorldOrigin.Instance == null ||
            !WorldOrigin.Instance.IsSet)
        {
            UpdateDebug("Waiting for origin...");
            StartCoroutine(WaitAndSpawn());
            return;
        }

        SpawnAtPosition(
            WorldOrigin.Instance.Origin.position,
            WorldOrigin.Instance.Origin.rotation);
    }

    IEnumerator WaitAndSpawn()
    {
        yield return new WaitUntil(() =>
            WorldOrigin.Instance != null &&
            WorldOrigin.Instance.IsSet);
        SpawnAtPosition(
            WorldOrigin.Instance.Origin.position,
            WorldOrigin.Instance.Origin.rotation);
    }

    IEnumerator SpawnRoutine(Vector3 qrPos,
                              Quaternion qrRot)
    {
        yield return new WaitUntil(() =>
            MRUK.Instance != null &&
            MRUK.Instance.GetCurrentRoom() != null);

        _room = MRUK.Instance.GetCurrentRoom();
        UpdateDebug("Room found!\nWaiting for Fusion...");

        yield return new WaitUntil(() => {
            _runner =
                FindFirstObjectByType<NetworkRunner>();
            return _runner != null;
        });

        UpdateDebug("Runner found!\n" +
                   "Waiting for session...");

        yield return new WaitUntil(() =>
            _runner.IsRunning);

        UpdateDebug("Runner running!\nChecking role...");

        yield return new WaitForSeconds(2f);

        Debug.Log($"IsServer: {_runner.IsServer}");
        Debug.Log($"IsClient: {_runner.IsClient}");

        UpdateDebug(
            $"Session: {_runner.SessionInfo.Name}\n" +
            $"Players: " +
            $"{_runner.SessionInfo.PlayerCount}\n" +
            $"Server: {_runner.IsServer}\n" +
            $"Client: {_runner.IsClient}");

        yield return new WaitUntil(() =>
            _runner.IsServer || _runner.IsClient);

        bool shouldSpawn =
            _runner.IsServer ||
            _runner.IsSharedModeMasterClient;

        if (shouldSpawn)
        {
            UpdateDebug("HOST!\nSpawning objects...");
            SpawnWeapons();

            yield return StartCoroutine(
                MaintainBreakableCount());

            UpdateDebug("Spawn complete!\n" +
                       $"Breakables: " +
                       $"{activeBreakables.Count}");
        }
        else
        {
            UpdateDebug("CLIENT!\n" +
                       "Waiting for objects...");
        }
    }

    // ── WEAPONS ──────────────────────────────────────────

    void SpawnWeapons()
    {
        if (weaponPrefabs == null ||
            weaponPrefabs.Count == 0)
        {
            Debug.LogError("No weapon prefabs!");
            return;
        }

        bool canSpawn = _runner.IsServer ||
                        _runner.IsSharedModeMasterClient;
        if (!canSpawn) return;

        foreach (var netObj in weaponPrefabs)
        {
            if (netObj == null)
            {
                Debug.LogError("Weapon prefab is null!");
                continue;
            }

            Vector3 pos;
            Vector3 normal;

            if (TryGetWallPosition(out pos, out normal))
            {
                Quaternion rot =
                    Quaternion.LookRotation(normal);

                Debug.Log($"Spawning weapon: " +
                          $"{netObj.name}");

                NetworkObject spawned =
                    _runner.Spawn(netObj, pos, rot);

                if (spawned != null)
                {
                    ObjectHanger hanger =
                        spawned.gameObject
                            .GetComponent<ObjectHanger>();

                    if (hanger != null)
                        hanger.Initialise(pos, rot);

                    Debug.Log($"Weapon spawned: " +
                              $"{netObj.name} at {pos}");
                }
                else
                {
                    Debug.LogError(
                        $"Spawn failed for {netObj.name}");
                }
            }
        }
    }

    bool TryGetWallPosition(out Vector3 position,
                             out Vector3 normal)
    {
        position = Vector3.zero;
        normal   = Vector3.forward;

        if (_room == null) return false;

        var walls = _room.WallAnchors;
        if (walls == null || walls.Count == 0)
            return false;

        var wall = walls[
            Random.Range(0, walls.Count)];
        normal   = wall.transform.forward;

        position = new Vector3(
            wall.transform.position.x,
            wallSpawnHeight,
            wall.transform.position.z);

        position += normal * 0.05f;
        return true;
    }

    // ── BREAKABLES ───────────────────────────────────────

    public IEnumerator MaintainBreakableCount()
    {
        activeBreakables.RemoveAll(
            obj => obj == null);

        int toSpawn = targetBreakableCount -
                      activeBreakables.Count;

        Debug.Log($"Spawning {toSpawn} breakables");

        for (int i = 0; i < toSpawn; i++)
        {
            SpawnOneBreakable();
            yield return new WaitForSeconds(0.1f);
        }
    }

    void SpawnOneBreakable()
    {
        if (breakablePrefabs == null ||
            breakablePrefabs.Count == 0)
        {
            Debug.LogError("No breakable prefabs!");
            return;
        }

        if (_room == null) return;
        if (_runner == null) return;

        bool canSpawn = _runner.IsServer ||
                        _runner.IsSharedModeMasterClient;
        if (!canSpawn) return;

        NetworkObject netObj = breakablePrefabs[
            Random.Range(0, breakablePrefabs.Count)];

        if (netObj == null)
        {
            Debug.LogError("Breakable prefab null!");
            return;
        }

        Vector3 spawnPos = Vector3.zero;
        bool    found    = false;
        int     attempts = 0;

        while (!found && attempts < maxSpawnAttempts)
        {
            attempts++;
            Vector3 candidatePos;
            Vector3 candidateNormal;

            bool result =
                _room.GenerateRandomPositionOnSurface(
                    MRUK.SurfaceType.FACING_UP,
                    minDistanceBetween,
                    new LabelFilter(
                        MRUKAnchor.SceneLabels.FLOOR |
                        MRUKAnchor.SceneLabels.TABLE |
                        MRUKAnchor.SceneLabels.COUCH),
                    out candidatePos,
                    out candidateNormal);

            if (!result) continue;

            if (!IsTooClose(candidatePos))
            {
                spawnPos   = candidatePos;
                spawnPos.y += 0.05f;
                found      = true;
            }
        }

        if (!found)
        {
            Debug.LogWarning("No floor position — " +
                             "using fallback");
            spawnPos = new Vector3(
                Random.Range(-2f, 2f),
                0.1f,
                Random.Range(-2f, 2f));
        }

        Debug.Log($"Spawning breakable: {netObj.name} " +
                  $"at {spawnPos}");

        NetworkObject spawned = _runner.Spawn(
            netObj,
            spawnPos,
            Quaternion.Euler(
                0, Random.Range(0f, 360f), 0));

        if (spawned != null)
        {
            DestructibleObject destructible =
                spawned.GetComponent<DestructibleObject>();

            if (destructible != null)
                destructible.OnBroken +=
                    OnBreakableDestroyed;

            activeBreakables.Add(spawned.gameObject);
            Debug.Log($"Breakable spawned: " +
                      $"{netObj.name}");
        }
        else
        {
            Debug.LogError(
                $"Spawn failed for {netObj.name}");
        }
    }

    bool IsTooClose(Vector3 position)
    {
        foreach (var obj in activeBreakables)
        {
            if (obj == null) continue;
            if (Vector3.Distance(
                obj.transform.position,
                position) < minDistanceBetween)
                return true;
        }
        return false;
    }

    void OnBreakableDestroyed(GameObject broken)
    {
        activeBreakables.Remove(broken);
        StartCoroutine(RespawnAfterDelay(2f));
    }

    IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        yield return StartCoroutine(
            MaintainBreakableCount());

        UpdateDebug($"Breakables: " +
                    $"{activeBreakables.Count}/" +
                    $"{targetBreakableCount}");
    }

    void UpdateDebug(string msg)
    {
        Debug.Log(msg);
        if (debugText != null)
            debugText.text = msg;
    }
}