// ── Required OVR Interaction SDK namespaces ───────────────────────────────────
using UnityEngine;
using Oculus.Interaction;           // PointableElement, IInteractableView, PointerEvent, PointerEventType
using Oculus.Interaction.HandGrab;  // HandGrabInteractable (Oculus.Interaction.HandGrab namespace)

/// <summary>
/// Attach to every weapon prefab alongside OVR Interaction SDK's HandGrabInteractable.
///
/// HOW GRAB DETECTION WORKS IN THIS SDK
/// ──────────────────────────────────────
/// HandGrabInteractable inherits from:
///   PointerInteractable<HandGrabInteractor, HandGrabInteractable>
///     └── Interactable<HandGrabInteractor, HandGrabInteractable>
///
/// The reliable way to detect grab/release without subclassing the SDK is to use
/// the PointableElement that HandGrabInteractable exposes. PointableElement fires
/// WhenPointerEventRaised for every pointer interaction event including:
///   PointerEventType.Select   — a hand confirmed a grab
///   PointerEventType.Unselect — the hand released
///   PointerEventType.Hover    — hand is near but not grabbing
///   PointerEventType.Unhover  — hand moved away
///
/// We also keep a count of active selectors (selectCount) so we handle the edge
/// case where two hands could theoretically grab the same weapon — IsHeld stays
/// true until ALL hands have released.
///
/// REQUIRED COMPONENTS ON THE SAME GAMEOBJECT
/// ───────────────────────────────────────────
///   - HandGrabInteractable   (OVR Interaction SDK)
///   - Grabbable              (required by HandGrabInteractable)
///   - Rigidbody              (required by HandGrabInteractable)
///   - WeaponPickup           (this script)
///
/// RACK SLOT SETUP
/// ───────────────
/// On each WeaponRack slot anchor child GO:
///   - Add a sphere trigger Collider
///   - Set its Tag to "WeaponRackSlot" (matches rackTriggerTag below)
/// This lets a player drop a weapon near the rack to manually return it.
/// </summary>
public class WeaponPickup : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("Rack Return")]
    [Tooltip("Tag on the rack slot trigger colliders. " +
             "When this weapon enters a trigger with this tag while not held, " +
             "it snaps back to its slot. Must match the tag set on each slot anchor's collider.")]
    [SerializeField] private string rackTriggerTag = "WeaponRackSlot";

    // ── Runtime State ─────────────────────────────────────────────────────────

    /// <summary>
    /// True while at least one hand is actively holding this weapon.
    /// Stays true until ALL grabs are released (handles two-hand edge case).
    /// Read by WeaponRack to gate auto-return logic.
    /// </summary>
    public bool IsHeld => selectCount > 0;

    [HideInInspector]
    public WeaponRack rack;             // The rack that owns this weapon — set by WeaponRack.RegisterWeaponInSlot() at runtime
    private int slotIndex;        // Index of this weapon's slot on the rack
    private HandGrabInteractable grabInteractable; // SDK component — source of grab events

    // Counts how many hands are currently selecting this weapon.
    // Incremented on Select, decremented on Unselect.
    // Keeps IsHeld accurate when two hands interact with the same object.
    private int selectCount = 0;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Cache the HandGrabInteractable. Full type is:
        //   Oculus.Interaction.HandGrab.HandGrabInteractable
        // It must live on the same GameObject as this script.
        grabInteractable = GetComponent<HandGrabInteractable>();

        if (grabInteractable == null)
            Debug.LogWarning($"[WeaponPickup] {name}: no HandGrabInteractable found. " +
                             "Grab events will not fire. " +
                             "Add HandGrabInteractable to this GameObject.");
    }

    private void OnEnable()
    {
        // PointableElement is the event bus on every PointerInteractable.
        // WhenPointerEventRaised fires for Select, Unselect, Hover, Unhover.
        // We use OnEnable/OnDisable so the subscription tracks the component's
        // active state — avoids stale callbacks when the weapon is disabled.
        if (grabInteractable != null && grabInteractable.PointableElement != null)
            grabInteractable.PointableElement.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDisable()
    {
        // Always unsubscribe to prevent callbacks on an inactive or destroyed object
        if (grabInteractable != null && grabInteractable.PointableElement != null)
            grabInteractable.PointableElement.WhenPointerEventRaised -= OnPointerEvent;

        // Reset select count if this weapon is forcibly disabled mid-grab
        selectCount = 0;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by WeaponRack.RegisterWeaponInSlot() after placing this weapon in a slot.
    /// Stores the rack reference and slot index so this script can report back.
    /// No manual call needed — WeaponRack handles it automatically.
    /// </summary>
    public void Init(WeaponRack ownerRack, int ownSlotIndex)
    {
        rack = ownerRack;
        slotIndex = ownSlotIndex;
    }

    // ── Pointer Event Handler ─────────────────────────────────────────────────

    /// <summary>
    /// Receives all pointer events from the HandGrabInteractable's PointableElement.
    ///
    /// PointerEvent contains:
    ///   Identifier — which interactor fired this (unique per hand)
    ///   Type       — Select, Unselect, Hover, Unhover, Move, Cancel
    ///   Pose       — world-space pose at the time of the event
    ///
    /// We only act on Select and Unselect.
    /// </summary>
    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            // ── Grab confirmed ────────────────────────────────────────────────
            // PointerEventType.Select fires once when the hand's grab gesture
            // crosses the threshold defined by HandGrabInteractable's GrabbingRule.
            case PointerEventType.Select:

                selectCount++;

                // Only notify the rack on the FIRST grab (count goes 0 → 1)
                // so we don't start multiple return timers if two hands grab at once
                if (selectCount == 1)
                {
                    rack?.WeaponPickedUp(this, slotIndex);
                    Debug.Log($"[WeaponPickup] {name} grabbed from slot {slotIndex}");
                }
                break;

            // ── Release confirmed ─────────────────────────────────────────────
            // PointerEventType.Unselect fires once when the hand's grab gesture
            // falls below the release threshold in HandGrabInteractable's GrabbingRule.
            case PointerEventType.Unselect:

                // Guard against spurious Unselect calls going below zero
                selectCount = Mathf.Max(0, selectCount - 1);

                // Only mark as released when ALL hands have let go
                if (selectCount == 0)
                {
                    Debug.Log($"[WeaponPickup] {name} released from slot {slotIndex}");
                    // WeaponRack's auto-return coroutine (started on pickup) handles
                    // bringing the weapon back after autoReturnDelay seconds if needed
                }
                break;

                // ── All other event types (Hover, Unhover, Move, Cancel) ──────────
                // We do not need to act on these for rack logic, so they are ignored.
        }
    }

    // ── Trigger Detection (manual rack drop) ─────────────────────────────────

    /// <summary>
    /// If the weapon enters a rack slot trigger zone while not being held, the player
    /// has deliberately dropped it close enough to the rack to count as a return.
    /// Notifies WeaponRack which cancels the auto-return timer and snaps it to the slot.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Never recall a weapon the player is still actively holding
        if (IsHeld) return;

        // Only respond to rack slot triggers — ignore all other trigger volumes
        if (!other.CompareTag(rackTriggerTag)) return;

        rack?.WeaponReturned(this, slotIndex);
        Debug.Log($"[WeaponPickup] {name} manually returned to rack slot {slotIndex}");
    }
}