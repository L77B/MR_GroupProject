using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Lightsaberscript : MonoBehaviour
{
    public GameObject[] lights;
    public float delayBetweenLights = 0.1f;
    private bool isToggling = false;

    public GameObject saberCollider;
    public GameObject saberEffect;
    // Start is called once before the first execution of Upd   ate after the MonoBehaviour is created
    void Start()
    {
        saberCollider.SetActive(!saberCollider.activeSelf);
        saberEffect.SetActive(!saberEffect.activeSelf);
        StartCoroutine(ToggleLightsSequentially());
    }

    // Update is called once per frame
    void Update()
    {
        bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        if (spacePressed && !isToggling)
        {
            saberCollider.SetActive(!saberCollider.activeSelf);
            saberEffect.SetActive(!saberEffect.activeSelf);
            StartCoroutine(ToggleLightsSequentially());
        }

    }

    private IEnumerator ToggleLightsSequentially()
    {
        isToggling = true;
        foreach (GameObject light in lights)
        {
            light.SetActive(!light.activeSelf);
            yield return new WaitForSeconds(delayBetweenLights);
        }
        isToggling = false;
    }

}