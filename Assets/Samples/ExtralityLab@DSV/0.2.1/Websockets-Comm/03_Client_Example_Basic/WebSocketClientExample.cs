using UnityEngine;
using NativeWebSocket;
using UnityEngine.Events;
using System;


#if UNITY_EDITOR
using UnityEditor;
#endif

public class WebSocketClientExample : MonoBehaviour
{
    private WebSocket websocket;
    public string serverIP = "XXX.XXX.XXX.XXX"; // Replace with your server's IP address
    public int serverPort = 8081; // Replace with your server's port number (8081 is the default)

    [Range(0, 255)]
    public int ledIntensity = 0;

    async void Start()
    {
        websocket = new WebSocket("ws://" + serverIP + ":" + serverPort);

        //Runs when connected to the server
        websocket.OnOpen += async () =>
        {
            Debug.Log("Connected to WebSocket server");
            string UUID = SystemInfo.deviceUniqueIdentifier; // Certain devices block MAC address access for privacy reasons so we send a UUID instead

            await websocket.SendText("Device (Unity):" + SystemInfo.deviceName + " ... Device's Unique Identifier: " + UUID);
        };

        //Runs when a message is received from the server
        websocket.OnMessage += (bytes) =>
        {
            string message = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("Received: " + message);

            IncomingMessageParser(message);

        };

        //Runs when disconnected from the server
        websocket.OnClose += (code) =>
        {
            Debug.Log("WebSocket closed");
        };

        await websocket.Connect();
    }

    void Update()
    {
        //Although not necessary for our lab, I have left this here as a reference
        //Websockets will not work on WebGL builds so with this preprocessor directive we include all builds except WebGL as well as including the editor for testing purposes
        #if !UNITY_WEBGL || UNITY_EDITOR 

            websocket.DispatchMessageQueue();
        #endif
    }

    async void OnDestroy()
    {
        if (websocket != null)
            await websocket.Close();
    }

    public async void SendHello()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.SendText("Hello from Unity");
            Debug.Log("Sent: Hello from Unity");
        }
        else
        {
            Debug.LogWarning("WebSocket not connected");
        }
    }

    public async void SendLedON()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.SendText("LED_INTENSITY:255");
            Debug.Log("Sent: LED_INTENSITY:255");
        }
        else
        {
            Debug.LogWarning("WebSocket not connected");
        }
    }

    public async void SendLedOFF()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.SendText("LED_INTENSITY:0");
            Debug.Log("Sent: LED_INTENSITY:0");
        }
        else
        {
            Debug.LogWarning("WebSocket not connected");
        }
    }

    public async void SendLedIntensity()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.SendText("LED_INTENSITY:" + ledIntensity);
            Debug.Log("Sent: LED_INTENSITY:" + ledIntensity);
        }
        else
        {
            Debug.LogWarning("WebSocket not connected");
        }
    }

    public void IncomingMessageParser(String msg)
    {
        string valueParsed = msg.Substring( msg.IndexOf(":") + 1);

        if(msg.Contains("button")) {
            if(valueParsed == "1") 
            {
                //do something if button pressed
                Debug.Log("ESP32 Button Pressed");
            }
            if(valueParsed == "0") 
            {
                //do something if button released
                Debug.Log("ESP32 Button Released");
            }

        }

    }

}

