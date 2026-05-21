using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using Oculus.Interaction;

public class NetworkDebugger : NetworkBehaviour
{
    private TextMesh _floatingText;
    private NetworkObject _netObj;
    private Grabbable _grabbable;

    public override void OnNetworkSpawn()
    {
        _netObj    = GetComponent<NetworkObject>();
        _grabbable = GetComponent<Grabbable>();

        // Create floating 3D text above cube
        CreateFloatingText();

        Debug.Log($"NetworkDebugger spawned!" +
                  $"\nIsOwner: {IsOwner}" +
                  $"\nIsServer: {IsServer}" +
                  $"\nOwnerClientId: {OwnerClientId}");
    }

    void CreateFloatingText()
    {
        // Create child GameObject with TextMesh
        GameObject textGO = new GameObject("DebugText");
        textGO.transform.SetParent(transform);
        textGO.transform.localPosition =
            new Vector3(0, 0.5f, 0);
        textGO.transform.localScale =
            Vector3.one * 0.02f;

        _floatingText = textGO.AddComponent<TextMesh>();
        _floatingText.fontSize  = 50;
        _floatingText.color     = Color.yellow;
        _floatingText.anchor    = TextAnchor.MiddleCenter;
        _floatingText.alignment = TextAlignment.Center;
        _floatingText.text      = "Initialising...";
    }

    void Update()
    {
        if (_floatingText == null) return;
        if (_netObj == null) return;

        // Face camera
        if (Camera.main != null)
            _floatingText.transform.LookAt(
                Camera.main.transform);

        // Check grab state
        bool isHeld = _grabbable != null &&
                      _grabbable.SelectingPointsCount > 0;

        // Update text
        _floatingText.text =
            $"Owner: {_netObj.OwnerClientId}\n" +
            $"IsOwner: {IsOwner}\n" +
            $"IsServer: {IsServer}\n" +
            $"Held: {isHeld}\n" +
            $"Pos: {transform.position.x:F1}," +
            $"{transform.position.y:F1}," +
            $"{transform.position.z:F1}";
    }
}