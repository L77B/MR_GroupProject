
using UnityEngine;

public class OVRShoot : MonoBehaviour
{
    public Shooting shootingScript;
    public string triggerShot = "TriggerShot";
    private bool wasPressedLastFrame = false;
    public float triggerThreshold = 0.5f;

    void Update()
    {
        float triggerValue = 0f;

        if (OVRPlugin.GetActionStateFloat(triggerShot, out triggerValue))
        {
            bool isPressed = triggerValue > triggerThreshold;

            if (isPressed && !wasPressedLastFrame)
            {
                shootingScript.TriggerShot();
            }

            wasPressedLastFrame = isPressed;
        }
        else
        {
            Debug.LogWarning("Trigger action not found or failed");
        }
    }
}
