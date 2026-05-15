using UnityEngine;
using NativeWebSocket;
using UnityEngine.SceneManagement;

public class WebSocketSceneClient : MonoBehaviour
{
    private WebSocket websocket;

    public string serverIP = "XXX.XXX.XXX.XXX";
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
                sceneLoader.RestartGame();
            }
            else if (value == "0")
            {
                Debug.Log("Button released");
            }
        }
    }
}
