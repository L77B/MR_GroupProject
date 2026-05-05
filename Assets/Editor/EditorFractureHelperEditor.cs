#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(EditorFractureHelper))]
public class EditorFractureHelperEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorFractureHelper helper = (EditorFractureHelper)target;

        if (GUILayout.Button("Fracture This Object"))
        {
            FractureObject(helper);
        }
    }

    void FractureObject(EditorFractureHelper helper)
    {
        GameObject go   = helper.gameObject;
        MeshFilter mf   = go.GetComponent<MeshFilter>();

        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("No MeshFilter found on object!");
            return;
        }

        Mesh mesh      = mf.sharedMesh;
        Bounds bounds  = mesh.bounds;
        Material mat   = go.GetComponent<MeshRenderer>().sharedMaterial;

        for (int i = 0; i < helper.fragmentCount; i++)
        {
            GameObject fragment = new GameObject($"Fragment_{i}");
            fragment.transform.SetParent(go.transform);
            fragment.transform.localPosition = Vector3.zero;
            fragment.transform.localRotation = Quaternion.identity;
            fragment.transform.localScale    = Vector3.one;
            fragment.SetActive(false);

            MeshFilter fragMF    = fragment.AddComponent<MeshFilter>();
            fragMF.mesh          = GenerateFragmentMesh(
                                    mesh, bounds, i, helper.fragmentCount);

            MeshRenderer fragMR  = fragment.AddComponent<MeshRenderer>();
            fragMR.sharedMaterial = mat;

            MeshCollider fragMC  = fragment.AddComponent<MeshCollider>();
            fragMC.convex        = true;
        }

        // Auto-assign fragments to BreakableObject if present
        BreakableObject breakable = go.GetComponent<BreakableObject>();
        if (breakable != null)
        {
            GameObject[] fragments = new GameObject[helper.fragmentCount];
            for (int i = 0; i < helper.fragmentCount; i++)
                fragments[i] = go.transform.Find($"Fragment_{i}").gameObject;

            SerializedObject so = new SerializedObject(breakable);
            SerializedProperty prop = so.FindProperty("fracturedPieces");
            prop.arraySize = fragments.Length;
            for (int i = 0; i < fragments.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = fragments[i];
            so.ApplyModifiedProperties();

            Debug.Log("Auto-assigned fragments to BreakableObject!");
        }

        Debug.Log($"Fractured {go.name} into " +
                  $"{helper.fragmentCount} pieces!");
    }

    Mesh GenerateFragmentMesh(Mesh originalMesh, Bounds bounds,
                               int seed, int totalFragments)
    {
        Random.InitState(seed * 1000);

        Vector3[] originalVerts = originalMesh.vertices;
        int[]     originalTris  = originalMesh.triangles;

        List<Vector3> newVerts     = new List<Vector3>();
        List<int>     selectedTris = new List<int>();
        Dictionary<int, int> indexMap = new Dictionary<int, int>();

        int trisPerFragment = Mathf.Max(3,
            originalTris.Length / (totalFragments * 3));
        int maxStart = Mathf.Max(1,
            originalTris.Length / 3 - trisPerFragment);
        int startTri = Random.Range(0, maxStart) * 3;

        for (int i = startTri;
             i < startTri + trisPerFragment * 3 &&
             i < originalTris.Length; i++)
        {
            int origIndex = originalTris[i];
            if (!indexMap.ContainsKey(origIndex))
            {
                indexMap[origIndex] = newVerts.Count;
                newVerts.Add(originalVerts[origIndex]);
            }
            selectedTris.Add(indexMap[origIndex]);
        }

        if (newVerts.Count < 3)
            return CreateCubeFragment();

        Mesh fragmentMesh      = new Mesh();
        fragmentMesh.vertices  = newVerts.ToArray();
        fragmentMesh.triangles = selectedTris.ToArray();
        fragmentMesh.RecalculateNormals();
        fragmentMesh.RecalculateBounds();

        return fragmentMesh;
    }

    Mesh CreateCubeFragment()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cubeMesh   = temp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(temp);
        return cubeMesh;
    }
}
#endif