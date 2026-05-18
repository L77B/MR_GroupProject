using UnityEngine;
using Fusion;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

public class BatGrabAuthority : NetworkBehaviour
{
    private HandGrabInteractable _grab;
    private NetworkTransform     _netTransform;

    public override void Spawned()
    {
        _grab         = GetComponent<HandGrabInteractable>();
        _netTransform = GetComponent<NetworkTransform>();

        if (_grab != null)
        {
            _grab.WhenSelectingInteractorViewAdded +=
                OnGrabbed;
            _grab.WhenSelectingInteractorViewRemoved +=
                OnReleased;
        }
    }

    void OnGrabbed(IInteractorView view)
    {
        Debug.Log("Bat grabbed — requesting authority");
        Object.RequestStateAuthority();

        // Disable NetworkTransform while grabbed
        // so it doesn't fight hand grab system
        if (_netTransform != null)
            _netTransform.enabled = false;
    }

    void OnReleased(IInteractorView view)
    {
        Debug.Log("Bat released");

        // Re-enable NetworkTransform after release
        if (_netTransform != null)
            _netTransform.enabled = true;
    }

    void OnDestroy()
    {
        if (_grab != null)
        {
            _grab.WhenSelectingInteractorViewAdded -=
                OnGrabbed;
            _grab.WhenSelectingInteractorViewRemoved -=
                OnReleased;
        }
    }
}