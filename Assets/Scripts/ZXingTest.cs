using UnityEngine;
using ZXing;

public class ZXingVersionTest : MonoBehaviour
{
    void Start()
    {
        // Test basic reader creation
        BarcodeReaderGeneric reader = new BarcodeReaderGeneric();
        Debug.Log("BarcodeReaderGeneric created successfully");

        // Log available methods
        var methods = typeof(BarcodeReaderGeneric).GetMethods();
        foreach (var method in methods)
        {
            if (method.Name == "Decode")
            {
                string parameters = "";
                foreach (var param in method.GetParameters())
                {
                    parameters += $"{param.ParameterType.Name} {param.Name}, ";
                }
                Debug.Log($"Decode method found: Decode({parameters})");
            }
        }
    }
}