using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.IO;

public class RoomExporter : MonoBehaviour
{
    void Start() {
        // Auto-export when app launches
        // Gives MRUK time to load the room first
        Invoke("ExportRoom", 3f);
    }

    public void ExportRoom() {
    if (MRUK.Instance == null) {
        Debug.LogError("MRUK Instance not found!");
        return;
    }

    var room = MRUK.Instance.GetCurrentRoom();
    if (room == null) {
        Debug.LogError("No room loaded! " +
                       "Make sure Room Setup is complete on headset.");
        return;
    }

    // Explicit parameters to resolve ambiguity
    string json = MRUK.Instance.SaveSceneToJsonString(
        includeGlobalMesh: false,
        rooms: null);

    if (string.IsNullOrEmpty(json)) {
        Debug.LogError("Room JSON is empty!");
        return;
    }

    string path = Path.Combine(
        Application.persistentDataPath,
        "room_scan.json");

    File.WriteAllText(path, json);
    Debug.Log($"Room exported successfully to: {path}");
    Debug.Log($"JSON length: {json.Length} characters");
    }
}