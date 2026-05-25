using UnityEngine;
using UnityEngine.InputSystem;

public class OVRShooting : MonoBehaviour
{
    public Shooting shootingScript;
    public InputActionReference triggerShot;

    private bool wasPressedLastFrame = false;
    public float triggerThreshold = 0.5f;
    private Coroutine hapticRoutine;

    void OnEnable()
    {
        triggerShot.action.Enable();
        TriggerHaptics(10f);
    }

    void OnDisable()
    {
        triggerShot.action.Disable();
        StopHaptics();
    }

    void Update()
    {
        float triggerValue = triggerShot.action.ReadValue<float>();

        bool isPressed = triggerValue > triggerThreshold;

        if (isPressed && !wasPressedLastFrame)
        {
            shootingScript.TriggerShot();
            TriggerHaptics(10f);
        }

        wasPressedLastFrame = isPressed;
    }

    void TriggerHaptics(float force)
    {
        float intensity = Mathf.Clamp01(force / 20f);

        StopHaptics();
        hapticRoutine = StartCoroutine(TriggerHapticsForSeconds(intensity, 0.2f));
    }

    System.Collections.IEnumerator TriggerHapticsForSeconds(float intensity, float duration)
    {
        OVRInput.SetControllerVibration(intensity, intensity, OVRInput.Controller.RTouch);
        yield return new WaitForSeconds(duration);
        StopHaptics();
    }

    void StopHaptics()
    {
        if (hapticRoutine != null)
        {
            StopCoroutine(hapticRoutine);
            hapticRoutine = null;
        }

        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }
}