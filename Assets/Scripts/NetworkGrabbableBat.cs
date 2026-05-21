using UnityEngine;
using Fusion;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using System.Collections.Generic;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Grabbable))]
public class NetworkGrabbableBat : NetworkBehaviour
{
    // ── Components ────────────────────────────────
    private Grabbable        _grabbable;
    private Rigidbody        _rb;
    private NetworkTransform _networkTransform;

    // ── Networked State ───────────────────────────
    [Networked] public PlayerRef CurrentHolder
        { get; private set; } = PlayerRef.None;

    // ── Local State ───────────────────────────────
    public bool IsBeingGrabbed { get; private set; }
    private bool _pendingGrab    = false;
    private bool _pendingRelease = false;
    private bool _networkTransformEnabled = true;

    // ── Anchor State ──────────────────────────────
    [Networked] private Vector3    AnchorPosition
        { get; set; }
    [Networked] private Quaternion AnchorRotation
        { get; set; }
    [Networked] private NetworkBool IsAnchored
        { get; set; }

    public override void Spawned()
    {
        _grabbable        = GetComponent<Grabbable>();
        _rb               = GetComponent<Rigidbody>();
        _networkTransform =
            GetComponent<NetworkTransform>();

        // Keep kinematic on wall until grabbed
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity  = false;
        }

        // Set anchor position
        if (Object.HasStateAuthority)
        {
            AnchorPosition = transform.position;
            AnchorRotation = transform.rotation;
            IsAnchored     = true;
            CurrentHolder  = PlayerRef.None;
        }

        // Subscribe to pointer events
        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised
                += OnPointerEvent;
        else
            Debug.LogError("Grabbable missing!");
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (!Object || !Object.IsValid) return;

        switch (evt.Type)
        {
            case PointerEventType.Select:
                OnGrabStarted();
                break;
            case PointerEventType.Unselect:
                OnGrabEnded();
                break;
        }
    }

    private void OnGrabStarted()
    {
        IsBeingGrabbed = true;
        _pendingGrab   = true;

        if (!Object.HasStateAuthority)
            Object.RequestStateAuthority();

        Debug.Log("Bat grabbed!");
    }

    private void OnGrabEnded()
    {
        IsBeingGrabbed = false;
        _pendingRelease = true;
        Debug.Log("Bat released!");
    }

    private void Update()
    {
        if (_networkTransform == null) return;

        // Disable NetworkTransform while being
        // grabbed locally so it doesn't fight
        // the grab system
        bool wantEnabled = !(IsBeingGrabbed &&
            !Object.HasStateAuthority);

        if (wantEnabled != _networkTransformEnabled)
        {
            _networkTransformEnabled = wantEnabled;
            _networkTransform.enabled = wantEnabled;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Process pending grab on authority
        if (_pendingGrab && Object.HasStateAuthority)
        {
            _pendingGrab  = false;
            IsAnchored    = false;
            CurrentHolder = Runner.LocalPlayer;

            // Release from wall
            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity  = true;
            }
        }

        // Process pending release on authority
        if (_pendingRelease && Object.HasStateAuthority)
        {
            _pendingRelease = false;
            CurrentHolder   = PlayerRef.None;
        }

        // Keep anchored to wall if not grabbed
        if (IsAnchored)
        {
            transform.position = AnchorPosition;
            transform.rotation = AnchorRotation;
            return;
        }

        // Non-authority: freeze RB while
        // someone else holds it
        if (CurrentHolder != PlayerRef.None &&
            !Object.HasStateAuthority)
        {
            if (_rb != null && !_rb.isKinematic)
            {
                _rb.linearVelocity  = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic     = true;
                _rb.useGravity      = false;
            }
        }
        else if (_rb != null &&
                 _rb.isKinematic &&
                 CurrentHolder == PlayerRef.None &&
                 !IsAnchored)
        {
            // Nobody holding and not anchored
            // re-enable physics
            _rb.isKinematic = false;
            _rb.useGravity  = true;
        }
    }

    public override void Despawned(
        NetworkRunner runner, bool hasState)
    {
        if (_grabbable != null)
            _grabbable.WhenPointerEventRaised
                -= OnPointerEvent;
    }
}