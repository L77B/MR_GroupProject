using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Meta.XR.MRUtilityKit;
using Fusion;

public class BreakableSpawner : MonoBehaviour
{
    public static BreakableSpawner Instance;

    [Header("Breakable Prefabs")]
    [SerializeField] private List<GameObject> breakablePrefabs;

    [Header("Spawn Settings")]
    [SerializeField] private int   targetCount        = 10;
    [SerializeField] private float minDistanceBetween = 0.4f;
    [SerializeField] private int   maxAttempts        = 20;

    [Header("Debug")]
    [SerializeField] private TMP_Text debugText;

    private List<GameObject> activeBreakables =
        new List<GameObject>();

    private NetworkRunner _runner;
    private MRUKRoom      _room;
    private bool          _initialised = false;

    void Awake()
    {
        Instance = this;
    }

    public void Initialise()
    {
        if (_initialised) return;
        StartCoroutine(InitRoutine());
    }

    IEnumerator InitRoutine()
    {
        // Wait for MRUK room
        yield return new WaitUntil(() =>
            MRUK.Instance != null &&
            MRUK.Instance.GetCurrentRoom() != null);

        _room = MRUK.Instance.GetCurrentRoom();
        UpdateDebug("Room found!\nWaiting for network...");

        // Wait for runner
        yield return new WaitUntil(() => {
            _runner =
                FindFirstObjectByType<NetworkRunner>();
            return _runner != null && _runner.IsRunning;
        });

        // Wait for role
        yield return new WaitUntil(() =>
            _runner.IsServer || _runner.IsClient);

        bool canSpawn = _runner.IsServer ||
                        _runner.IsSharedModeMasterClient;

        Debug.Log($"BreakableSpawner - " +
                  $"CanSpawn: {canSpawn}");

        if (!canSpawn)
        {
            UpdateDebug("Client — waiting for " +
                        "breakables...");
            yield break;
        }

        _initialised = true;
        UpdateDebug("Spawning breakables...");

        yield return StartCoroutine(
            MaintainCount());

        UpdateDebug($"Breakables ready: " +
                    $"{activeBreakables.Count}/" +
                    $"{targetCount}");
    }

    public IEnumerator MaintainCount()
    {
        activeBreakables.RemoveAll(
            obj => obj == null);

        int toSpawn = targetCount -
                      activeBreakables.Count;

        Debug.Log($"Need to spawn {toSpawn} breakables");

        for (int i = 0; i < toSpawn; i++)
        {
            SpawnOne();
            yield return new WaitForSeconds(0.1f);
        }
    }

    void SpawnOne()
    {
        if (breakablePrefabs == null ||
            breakablePrefabs.Count == 0)
        {
            Debug.LogError("No breakable prefabs!");
            return;
        }

        if (_room   == null) return;
        if (_runner == null) return;

        bool canSpawn = _runner.IsServer ||
                        _runner.IsSharedModeMasterClient;
        if (!canSpawn) return;

        GameObject prefab = breakablePrefabs[
            Random.Range(0, breakablePrefabs.Count)];

        if (prefab == null) return;

        NetworkObject netObj = prefab
            .GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError(
                $"{prefab.name} missing NetworkObject!");
            return;
        }

        // Find floor position
        Vector3 spawnPos = Vector3.zero;
        bool    found    = false;
        int     attempts = 0;

        while (!found && attempts < maxAttempts)
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
                Random.Range(0.5f, 2f));
        }

        Debug.Log($"Spawning breakable: {prefab.name}" +
                  $" at {spawnPos}");

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
                      $"{prefab.name}");
        }
        else
        {
            Debug.LogError(
                $"Spawn failed: {prefab.name}");
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
        Debug.Log($"Broken: {broken.name}. " +
                  $"Remaining: {activeBreakables.Count}");
        StartCoroutine(RespawnAfterDelay(2f));
    }

    IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        yield return StartCoroutine(MaintainCount());
        UpdateDebug($"Breakables: " +
                    $"{activeBreakables.Count}/" +
                    $"{targetCount}");
    }

    void UpdateDebug(string msg)
    {
        Debug.Log(msg);
        if (debugText != null)
            debugText.text = msg;
    }
}