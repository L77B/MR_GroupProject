using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;



public class SceneLoaderTwo : MonoBehaviour
{
    public GameObject explosionPrefab;
    public Transform spawnPoint;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    

IEnumerator RestartSceneSafely()
{
    yield return null;
    yield return null;
    yield return null;
    yield return null; // wait one frame so MR shuts down cleanly
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
}

    public void RestartGame()
    {
        StartCoroutine(RestartSceneSafely());
        //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadSceneByName(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
    public void TriggerExplosion()
    {
        gameObject.SetActive(false);
        Instantiate(explosionPrefab, spawnPoint.position, spawnPoint.rotation);
    }
}
