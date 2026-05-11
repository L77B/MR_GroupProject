using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Collections.Generic;

public class RoomVisualiser : MonoBehaviour
{
    [Header("Materials")]
    [SerializeField] private Color wallColor      = new Color(0.5f, 0.7f, 1f, 0.2f);
    [SerializeField] private Color floorColor     = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    [SerializeField] private Color furnitureColor = new Color(0.8f, 0.6f, 0.2f, 0.3f);
    [SerializeField] private Color lightColor     = new Color(1f, 1f, 0f, 0.5f);

    [Header("Settings")]
    [SerializeField] private bool showWalls      = true;
    [SerializeField] private bool showFloor      = true;
    [SerializeField] private bool showFurniture  = true;
    [SerializeField] private bool showLights     = true;

    // Store references for repositioning
    private List<GameObject> furnitureVisuals = new List<GameObject>();
    private List<GameObject> lightVisuals     = new List<GameObject>();

    public void VisualiseRoom() {
        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null) {
            Debug.LogError("No room data found!");
            return;
        }

        if (showFloor && room.FloorAnchors != null){
            foreach (var floorAnchor in room.FloorAnchors)
            VisualiseFloor(floorAnchor);
        }

        if (showWalls)
            foreach (var wall in room.WallAnchors)
                VisualiseWall(wall);

        if (showFurniture || showLights)
            foreach (var anchor in room.Anchors)
                VisualiseAnchor(anchor);

        Debug.Log("Room visualisation complete!");
    }

    void VisualiseFloor(MRUKAnchor anchor) {
        GameObject floor = CreateSurface(
            "Floor",
            anchor.transform.position,
            anchor.transform.rotation,
            new Vector3(5f, 0.01f, 5f),
            floorColor);
    }

    void VisualiseWall(MRUKAnchor anchor) {
        if (!showWalls) return;
        CreateSurface(
            "Wall",
            anchor.transform.position,
            anchor.transform.rotation,
            new Vector3(3f, 0.01f, 2.5f),
            wallColor);
    }

    void VisualiseAnchor(MRUKAnchor anchor) {
    string label = anchor.Label.ToString();
    Debug.Log($"Anchor found: {label} at " +
              $"{anchor.transform.position}");

    if (label.Contains("LAMP") ||
        label.Contains("LIGHT") ||
        label.Contains("CEILING_LIGHT") ||
        label.Contains("CEILING")) {
        if (showLights)
            VisualiseLightAnchor(anchor, label);
    }
    else if (label.Contains("TABLE") ||
             label.Contains("COUCH") ||
             label.Contains("CHAIR") ||
             label.Contains("DESK") ||
             label.Contains("STORAGE") ||
             label.Contains("BED")) {
        if (showFurniture)
            VisualiseFurniture(anchor, label);
    }
}
    void VisualiseLightAnchor(MRUKAnchor anchor, string label) {
        // Sphere to mark light position
        GameObject lightMarker = GameObject.CreatePrimitive(
            PrimitiveType.Sphere);
        lightMarker.name = $"Light_{label}";
        lightMarker.transform.position = anchor.transform.position;
        lightMarker.transform.localScale = Vector3.one * 0.2f;

        SetTransparentColor(lightMarker, lightColor);
        Destroy(lightMarker.GetComponent<Collider>());

        // Add light gizmo
        Light lightComp = lightMarker.AddComponent<Light>();
        lightComp.type      = LightType.Point;
        lightComp.range     = 3f;
        lightComp.intensity = 0.5f;
        lightComp.color     = Color.yellow;

        lightVisuals.Add(lightMarker);
        Debug.Log($"Light detected: {label} at " +
                  $"{anchor.transform.position}");
    }

    void VisualiseFurniture(MRUKAnchor anchor, string label) {
        GameObject furniture = GameObject.CreatePrimitive(
            PrimitiveType.Cube);
        furniture.name = $"Furniture_{label}";
        furniture.transform.position   = anchor.transform.position;
        furniture.transform.rotation   = anchor.transform.rotation;
        furniture.transform.localScale = new Vector3(1f, 0.8f, 0.5f);

        SetTransparentColor(furniture, furnitureColor);
        Destroy(furniture.GetComponent<Collider>());

        furnitureVisuals.Add(furniture);
        Debug.Log($"Furniture detected: {label} at " +
                  $"{anchor.transform.position}");
    }

    GameObject CreateSurface(string name, Vector3 position,
                              Quaternion rotation, Vector3 scale,
                              Color color) {
        GameObject surface = GameObject.CreatePrimitive(
            PrimitiveType.Cube);
        surface.name                   = name;
        surface.transform.position     = position;
        surface.transform.rotation     = rotation;
        surface.transform.localScale   = scale;

        SetTransparentColor(surface, color);
        Destroy(surface.GetComponent<Collider>());
        return surface;
    }

    void SetTransparentColor(GameObject obj, Color color) {
        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
        Material mat    = new Material(
            Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;

        // Enable transparency
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mr.material = mat;
    }

    // Call this when furniture moves to refresh positions
    public void RefreshFurniture() {
        foreach (var obj in furnitureVisuals)
            Destroy(obj);
        furnitureVisuals.Clear();

        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null) return;

        foreach (var anchor in room.Anchors)
            VisualiseAnchor(anchor);

        Debug.Log("Furniture positions refreshed!");
    }
}