using UnityEngine;
using UnityEngine.InputSystem;

public class OVRWeaponSwitch : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public InputActionReference SwitchBaseballBat;
    public InputActionReference SwitchLightSaber;

    public GameObject baseballbat;
    public GameObject lightSaber;
    public GameObject lightSaberParticles;

    public AudioClip audioClipLightSaber;
    public AudioClip audioClipBaseballBat;
    public AudioSource audioSource;

    public float triggerThreshold = 0.5f;

    void OnEnable()
    {
        SwitchBaseballBat.action.Enable();
        SwitchLightSaber.action.Enable();
    }

    void OnDisable()
    {
        SwitchBaseballBat.action.Disable();
        SwitchLightSaber.action.Disable();
    }

    void Update()
    {
        if (SwitchBaseballBat.action.WasPressedThisFrame())
        {
            ShowBaseballBat();
        }

        if (SwitchLightSaber.action.WasPressedThisFrame())
        {
            ShowLightSaber();
        }
    }

    void ShowBaseballBat()
    {

        baseballbat.GetComponent<Collider>().enabled = true;
        baseballbat.GetComponent<MeshRenderer>().enabled = true;

        audioSource.PlayOneShot(audioClipBaseballBat);

        lightSaber.GetComponent<Collider>().enabled = false;
        if (lightSaberParticles != null)
        {
            lightSaberParticles.SetActive(false);
        }

    }

    void ShowLightSaber()
    {
        lightSaber.GetComponent<Collider>().enabled = true;
        if (lightSaberParticles != null)
        {
            lightSaberParticles.SetActive(true);
        }

        audioSource.PlayOneShot(audioClipLightSaber);

        baseballbat.GetComponent<Collider>().enabled = false;
        baseballbat.GetComponent<MeshRenderer>().enabled = false;
    }
}
