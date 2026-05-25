using UnityEngine;

public class FreezeRotation : MonoBehaviour
{
   Quaternion fixedRotation;

    void Start()
    {
        fixedRotation = transform.rotation;
    }

    void LateUpdate()
    {
        transform.rotation = fixedRotation;
    }
}
