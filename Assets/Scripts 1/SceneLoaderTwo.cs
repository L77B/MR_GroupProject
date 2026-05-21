using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoaderTwo : MonoBehaviour
{
    public GameObject explosionPrefab;
    public Transform spawnPoint;
 
    public void RestartGame()
    {
        StartCoroutine(RestartRoutine());
    }

    IEnumerator RestartRoutine()
    {
        // Unload the game scene
        yield return SceneManager.UnloadSceneAsync("GameScene");

        // Reload the game scene
        yield return SceneManager.LoadSceneAsync("GameScene", LoadSceneMode.Additive);
    }


    

    public void TriggerExplosion()
    {
        gameObject.SetActive(false);
        Instantiate(explosionPrefab, spawnPoint.position, spawnPoint.rotation);
    }
}
