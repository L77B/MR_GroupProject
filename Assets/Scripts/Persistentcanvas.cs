using UnityEngine;

/// <summary>
/// Attach to the Canvas root that contains DualRageBar.
///
/// PURPOSE
/// ────────
/// Fusion's NetworkSceneManagerDefault re-instantiates every root GameObject
/// when it takes over the scene, which creates duplicate Canvases and therefore
/// duplicate rage bars visible in the headset.
///
/// This script calls DontDestroyOnLoad() in Awake so the Canvas survives Fusion
/// scene takeovers without being destroyed and re-created. A static instance
/// guard ensures only one Canvas ever exists — any duplicate that Fusion tries
/// to create is immediately destroyed.
///
/// SETUP
/// ─────
/// 1. Select your Canvas in the Hierarchy (the one containing DualRageBar).
/// 2. Add Component → PersistentCanvas.
/// 3. That's it. No Inspector slots to fill.
///
/// The Canvas must be a ROOT GameObject (no parent) for DontDestroyOnLoad
/// to work. If your Canvas is a child of something else, move it to the root
/// of the Hierarchy first.
/// </summary>
public class PersistentCanvas : MonoBehaviour
{
    private static PersistentCanvas _instance;

    private void Awake()
    {
        // Singleton guard — destroy any duplicate Fusion creates
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[PersistentCanvas] Duplicate Canvas '{name}' detected " +
                             "— Fusion tried to create a second one. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        _instance = this;

        // Survive Fusion scene takeovers
        DontDestroyOnLoad(gameObject);

        Debug.Log($"[PersistentCanvas] '{name}' marked DontDestroyOnLoad. " +
                  "This Canvas will not be duplicated by Fusion.");
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}