using UnityEngine;
using UnityEditor;

//This script allows us to add buttons to the inspector of WebSocketClientExample to call functions from there

[CustomEditor(typeof(WebSocketClientExample))]
public class EditorButton : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WebSocketClientExample myScript = (WebSocketClientExample)target;
        if (GUILayout.Button("Send Test Message"))
        {
            myScript.SendHello();
        }
                if (GUILayout.Button("Send \"LED ON\" instruction"))
        {
            myScript.SendLedON();
        }
                if (GUILayout.Button("Send \"LED OFF\" instruction"))
        {
            myScript.SendLedOFF();
        }
                if (GUILayout.Button("Send \"LED Intensity\" instruction"))
        {
            myScript.SendLedIntensity();
        }
    }
}
