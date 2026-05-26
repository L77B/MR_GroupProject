using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoaderTwo : MonoBehaviour
{
    public GameObject explosionPrefab;
    public Transform spawnPoint;
    public GameObject explosionObject;

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void TriggerExplosion()
    {

        explosionObject.SetActive(true);
        Debug.Log("Explosion triggered!");
    }
}
