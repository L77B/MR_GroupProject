using UnityEngine;

public class GunControllerVisibility : MonoBehaviour
{
    public GameObject gun;

    public GameObject leftControllerVisual;
    public GameObject rightControllerVisual;

    void Update()
    {
        bool controllersActive =
            OVRInput.IsControllerConnected(OVRInput.Controller.LTouch) ||
            OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);

        gun.SetActive(controllersActive);

        leftControllerVisual.SetActive(false);
        rightControllerVisual.SetActive(false);
    }
}