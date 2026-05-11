using UnityEngine;
using Meta.XR.MRUtilityKit;

public class RoomDataLoader : MonoBehaviour
{
    [SerializeField] private TextAsset roomDataJson;
    [SerializeField] private bool useFileInEditor = true;

    void Start() {
        if (useFileInEditor && roomDataJson != null) {
            Debug.Log("Loading room from saved JSON file...");
            MRUK.Instance.RegisterSceneLoadedCallback(OnRoomLoaded);
            MRUK.Instance.LoadSceneFromJsonString(roomDataJson.text);
        }
        else {
            Debug.Log("Loading room from device...");
            MRUK.Instance.RegisterSceneLoadedCallback(OnRoomLoaded);
            MRUK.Instance.LoadSceneFromDevice();
        }
    }

    void OnRoomLoaded() {
        Debug.Log("Room loaded successfully!");

        // Trigger visualiser
        RoomVisualiser visualiser =
            GetComponent<RoomVisualiser>();
        if (visualiser != null)
            visualiser.VisualiseRoom();
    }
}