using UnityEngine;
using UnityEngine.InputSystem;

public class OVRShooting : MonoBehaviour
{
    public Shooting shootingScript;
    public InputActionReference triggerShot;

    private bool wasPressedLastFrame = false;
    public float triggerThreshold = 0.5f;

    void OnEnable()
    {
        triggerShot.action.Enable();
    }

    void OnDisable()
    {
        triggerShot.action.Disable();
    }

    void Update()
    {
        float triggerValue = triggerShot.action.ReadValue<float>();

        bool isPressed = triggerValue > triggerThreshold;

        if (isPressed && !wasPressedLastFrame)
        {
            shootingScript.TriggerShot();
        }

        wasPressedLastFrame = isPressed;
    }
}