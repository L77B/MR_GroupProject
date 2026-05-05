using UnityEngine;
using ZXing;

public class ZXingTest : MonoBehaviour
{
    void Start()
    {
        BarcodeReaderGeneric reader = new BarcodeReaderGeneric();
        Debug.Log("ZXing loaded successfully!");
    }
}