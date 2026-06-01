using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoaderTwo : MonoBehaviour
{
    [Tooltip("DynamiteMulti prefab — instantiated at QR world origin on colocation.")]
    public GameObject explosionPrefab;
    public Transform  spawnPoint;      // kept for legacy Inspector refs; not used for spawning
    public GameObject explosionObject;

    [Tooltip("Height above QR world origin to spawn the dynamite. " +
             "Increase if dynamite appears in the floor.")]
    [SerializeField] private float spawnYOffset = 0f;

    private void OnEnable()
    {
        ColocationSetup.OnColocated += SpawnDynamite;
    }

    private void OnDisable()
    {
        ColocationSetup.OnColocated -= SpawnDynamite;
    }

    private void SpawnDynamite()
    {
        if (explosionPrefab == null)
        {
            Debug.LogWarning("[SceneLoaderTwo] explosionPrefab not assigned — dynamite won't spawn.");
            return;
        }

        // QR code = world origin (0,0,0) after colocation. spawnYOffset lifts the
        // dynamite above the QR plane if the model pivot sits below the desired height.
        Vector3 spawnPos = new Vector3(0f, spawnYOffset, 0f);
        GameObject spawned = Instantiate(explosionPrefab, spawnPos, Quaternion.identity);

        // Hand the DestructibleObject reference to WebSocketSceneClient so the
        // explosion event can hide it on all peers.
        var wsClient = FindFirstObjectByType<WebSocketSceneClient>();
        if (wsClient != null)
        {
            var destructible = spawned.GetComponent<DestructibleObject>();
            if (destructible != null)
                wsClient.dynamite = destructible;
        }

        Debug.Log($"[SceneLoaderTwo] Dynamite spawned at {spawnPos}.");
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void TriggerExplosion()
    {
        if (explosionObject != null)
            explosionObject.SetActive(true);
        Debug.Log("[SceneLoaderTwo] Explosion triggered!");
    }
}
