using UnityEngine;
using NativeWebSocket;
using UnityEngine.SceneManagement;
using Meta.XR.MRUtilityKit;
using System.Collections.Generic;
using System.Collections;




public class WebSocketSceneClient : MonoBehaviour
{
    private WebSocket websocket;
    public GameObject explosionPrefab;
public Transform explosionSpawnPoint;
public DestructibleObject dynamite;
private bool hasExploded = false;
public FindSpawnPositions spawnFinder;
public GameObject[] spawnPrefabsA;
public GameObject[] spawnPrefabsB;






    public string serverIP = "10.204.0.57";
    public int serverPort = 8081;

    public SceneLoaderTwo sceneLoader;   // Assign in Inspector

    async void Start()
    {
        websocket = new WebSocket("ws://" + serverIP + ":" + serverPort);

        websocket.OnOpen += async () =>
        {
            Debug.Log("Connected to WebSocket server");
            await websocket.SendText("Unity connected");
        };

        websocket.OnMessage += (bytes) =>
        {
            string msg = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("Received: " + msg);
            HandleMessage(msg);
        };

        websocket.OnClose += (code) =>
        {
            Debug.Log("WebSocket closed");
        };

        await websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket.DispatchMessageQueue();
#endif
    }

    async void OnDestroy()
    {
        if (websocket != null)
            await websocket.Close();
    }

    private void HandleMessage(string msg)
    {
        if (!msg.Contains(":")) return;

        string value = msg.Substring(msg.IndexOf(":") + 1);

        if (msg.Contains("button"))
        {
            if (value == "1")
            {
                Debug.Log("Button pressed → Reloading scene");
                FindFirstObjectByType<SceneLoaderTwo>().RestartGame();

                //sceneLoader.RestartGame();
            }
            else if (value == "0")
            {
                Debug.Log("Button released");
            }
        }
        //Big arcade button triggers explosion
    if (msg.Contains("main"))
    {
        if (value == "1" && !hasExploded)
        {
            hasExploded = true; 
            Debug.Log("Both buttons pressed → Dynamite breaks!");
            // Hide the dynamite
        if (dynamite != null)
            dynamite.gameObject.SetActive(false);

        // Spawn explosion
        TriggerExplosion();
        }
    }
    if (msg.Contains("spawnA"))
{
    if (value == "1")
        SpawnRandom(spawnPrefabsA);
}

if (msg.Contains("spawnB"))
{
    if (value == "1")
        SpawnRandom(spawnPrefabsB);
}

    }
    private void TriggerExplosion()
{
    Instantiate(explosionPrefab, explosionSpawnPoint.position, explosionSpawnPoint.rotation);
    Debug.Log("Explosion triggered!");
}



private void SpawnRandom(GameObject[] prefabs)
{
    if (prefabs.Length == 0) return;

    Vector3 pos = GetRandomMRPosition();
    if (pos == Vector3.zero) return;

    GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
    Instantiate(prefab, pos, Quaternion.identity);
}



private Vector3 GetRandomMRPosition()
{
    MRUKRoom room = MRUK.Instance.GetCurrentRoom();
    if (room == null)
    {
        Debug.LogWarning("No MR room found!");
        return Vector3.zero;
    }

    // We want only upward-facing surfaces (floor, tables, beds, couches)
    MRUK.SurfaceType surfaceType = MRUK.SurfaceType.FACING_UP;

    // Filter to FLOOR + TABLE
    LabelFilter filter = new LabelFilter(
        MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.TABLE
    );

    float clearance = spawnFinder.SurfaceClearanceDistance;

    Vector3 pos;
    Vector3 normal;

    bool success = room.GenerateRandomPositionOnSurface(
        surfaceType,
        clearance,
        filter,
        out pos,
        out normal
    );

    if (!success)
    {
        Debug.LogWarning("Could not find a valid surface spawn position.");
        return Vector3.zero;
    }

    return pos;
}









}
