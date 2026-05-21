using UnityEngine;
using UnityEngine.SceneManagement;

public class XRBootstrap : MonoBehaviour
{
    void Start()
    {
        SceneManager.LoadScene("Elin", LoadSceneMode.Additive);
    }
}
