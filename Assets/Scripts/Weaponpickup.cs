using UnityEngine;
using Oculus.Interaction;

/// <summary>
/// Attach to every weapon prefab alongside the OVR Interaction SDK's HandGrabInteractable.
///
/// RESPONSIBILITIES
/// ────────────────
/// 1. Detect when the player grabs or releases the weapon using the OVR grab system.
/// 2. Notify the WeaponRack when the weapon is picked up (so it can start the auto-return timer)
///    or returned (so it can cancel the timer and snap the weapon to its slot).
/// 3. Detect when an ungripped weapon enters a rack slot trigger zone and auto-return it.
///
/// HOW IT WORKS WITH HandGrabInteractable
/// ───────────────────────────────────────
/// HandGrabInteractable fires WhenStateChanged events as the player's hand
/// interacts with the object. This script subscribes to that event in OnEnable
/// and unsubscribes in OnDisable to avoid memory leaks or stale callbacks.
///
/// SETUP
/// ─────
/// 1. Add HandGrabInteractable to the weapon GameObject (OVR Interaction SDK).
/// 2. Add this WeaponPickup script to the same GameObject.
/// 3. On the WeaponRack's slot anchor, add a sphere trigger Collider and
///    set its tag to "WeaponRackSlot" (or change rackTriggerTag in the Inspector).
/// 4. WeaponRack.RegisterWeaponInSlot() calls Init() automatically —
///    no manual Init() call is needed.
/// </summary>
public class WeaponPickup : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("Rack Return")]
    [Tooltip("Tag that identifies a weapon rack slot trigger collider. " +
             "When this weapon enters a collider with this tag (while not held), " +
             "it snaps back to its slot. Must match the tag on each slot's trigger collider.")]
    [SerializeField] private string rackTriggerTag = "WeaponRackSlot";

    // ── Runtime State ─────────────────────────────────────────────────────────

    /// <summary>
    /// True while the player's hand is actively holding this weapon.
    /// Read by WeaponRack to decide whether auto-return should proceed.
    /// </summary>
    public bool IsHeld { get; private set; }

    private WeaponRack rack;              // The rack that owns this weapon
    private int slotIndex;         // Which slot on the rack this weapon belongs to
    private HandGrabInteractable grabInteractable;  // OVR SDK component on the same GameObject

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Cache the grab interactable — it must be on the same GameObject
        grabInteractable = GetComponent<HandGrabInteractable>();

        if (grabInteractable == null)
            Debug.LogWarning($"[WeaponPickup] {name} has no HandGrabInteractable. " +
                             "Grab detection will not work.");
    }

    private void OnEnable()
    {
        // Subscribe to grab state changes so we know when the player picks up or drops the weapon
        if (grabInteractable != null)
            grabInteractable.WhenStateChanged += OnGrabStateChanged;
    }

    private void OnDisable()
    {
        // Always unsubscribe when the component or GameObject is disabled
        // to avoid calling back into a destroyed or inactive object
        if (grabInteractable != null)
            grabInteractable.WhenStateChanged -= OnGrabStateChanged;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises this pickup with its owning rack and slot assignment.
    /// Called by WeaponRack.RegisterWeaponInSlot() immediately after the weapon
    /// is placed in a slot — no manual call is required.
    /// </summary>
    /// <param name="ownerRack">The WeaponRack this weapon belongs to.</param>
    /// <param name="ownSlotIndex">The slot index on that rack.</param>
    public void Init(WeaponRack ownerRack, int ownSlotIndex)
    {
        rack = ownerRack;
        slotIndex = ownSlotIndex;
    }

    // ── OVR Grab Callback ─────────────────────────────────────────────────────

    /// <summary>
    /// Fires whenever the HandGrabInteractable transitions between states
    /// (None → Hover → Select → None etc.).
    /// We only care about entering and leaving the Select (grabbed) state.
    /// </summary>
    private void OnGrabStateChanged(InteractableStateChangeArgs args)
    {
        // ── Weapon picked up ──────────────────────────────────────────────────
        if (args.NewState == InteractableState.Select)
        {
            IsHeld = true;

            // Notify the rack so it can start the auto-return countdown
            rack?.WeaponPickedUp(this, slotIndex);

            Debug.Log($"[WeaponPickup] {name} grabbed from slot {slotIndex}");
        }
        // ── Weapon released ───────────────────────────────────────────────────
        else if (args.PreviousState == InteractableState.Select)
        {
            IsHeld = false;

            // The rack's auto-return coroutine will handle bringing it back
            // if the player does not return it manually within autoReturnDelay seconds
            Debug.Log($"[WeaponPickup] {name} released from slot {slotIndex}");
        }
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────

    /// <summary>
    /// If the weapon enters a rack slot trigger zone while not being held,
    /// it has been dropped close enough to the rack to count as a manual return.
    /// Notify the rack so it can snap the weapon neatly into the slot.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Ignore trigger events while the player is still holding the weapon
        if (IsHeld) return;

        // Only respond to rack slot triggers — ignore all other trigger zones
        if (!other.CompareTag(rackTriggerTag)) return;

        rack?.WeaponReturned(this, slotIndex);
        Debug.Log($"[WeaponPickup] {name} returned to rack slot {slotIndex} via trigger drop");
    }
}