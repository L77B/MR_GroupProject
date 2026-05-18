using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoaderTwo : MonoBehaviour
{
    public GameObject explosionPrefab;
    public Transform spawnPoint;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
