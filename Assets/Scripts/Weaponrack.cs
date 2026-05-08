using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the weapon rack — a fixed prop in the Rage Room that holds all available weapons.
///
/// SLOT SYSTEM
/// ───────────
/// Each slot is a child Transform on the rack GameObject (e.g. "Slot_0", "Slot_1").
/// Slot 0 always holds the default baseball bat (already placed in the scene).
/// Higher slots unlock when the player reaches the corresponding rage level defined
/// in RageLevelConfig — a new weapon prefab is instantiated and placed in the slot.
///
/// WEAPON RETURN
/// ─────────────
/// Two mechanisms ensure weapons find their way back to the rack:
///   1. TIMER-based: when a weapon is released (not held), a coroutine waits
///      autoReturnDelay seconds and then smoothly lerps it back to its slot.
///   2. DISTANCE-based: checked every Update — if a free weapon has drifted
///      further than autoReturnDistance from its slot anchor, it is recalled
///      immediately (e.g. it rolled off a table or was thrown).
///
/// SETUP
/// ─────
/// 1. Create child empty GameObjects on the rack as slot anchors: "Slot_0", "Slot_1"…
///    Position them where weapons should rest on the rack.
/// 2. Also add a sphere trigger Collider to each slot anchor and tag it "WeaponRackSlot"
///    (WeaponPickup uses this tag for manual drop-return).
/// 3. Assign slotAnchors array in the Inspector (drag each child in order).
/// 4. Assign rageMeter and levelConfig.
/// 5. Make sure the default bat in Slot_0 has a WeaponPickup component —
///    RegisterWeaponInSlot() is called automatically on Start.
/// </summary>
public class WeaponRack : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The player's RageMeter. OnRageLevelUp drives weapon unlocks.")]
    [SerializeField] private RageMeter rageMeter;

    [Tooltip("ScriptableObject containing rage level thresholds and weapon unlock data.")]
    [SerializeField] private RageLevelConfig levelConfig;

    [Header("Slots")]
    [Tooltip("Child Transform anchors that define where each weapon rests on the rack. " +
             "Index 0 = default bat slot. Remaining indices match rage level indices.")]
    [SerializeField] private Transform[] slotAnchors;

    [Header("Auto-Return Settings")]
    [Tooltip("If an ungripped weapon travels further than this (metres) from its slot, " +
             "it is immediately recalled to the rack.")]
    [SerializeField] private float autoReturnDistance = 3f;

    [Tooltip("Seconds after a weapon is released before it automatically returns to the rack. " +
             "Gives the player a chance to pick it up again before it flies back.")]
    [SerializeField] private float autoReturnDelay = 3f;

    [Header("Unlock FX")]
    [Tooltip("AudioSource used to play the weapon unlock sound.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Sound played when a new weapon slot is unlocked.")]
    [SerializeField] private AudioClip unlockClip;

    [Tooltip("Particle system that fires at the newly unlocked slot position.")]
    [SerializeField] private ParticleSystem unlockParticles;

    // ── Runtime State ─────────────────────────────────────────────────────────

    /// <summary>The weapon GameObject currently occupying each slot (null if slot is empty).</summary>
    private List<GameObject> slotWeapons = new();

    /// <summary>Whether each slot has been unlocked (Slot 0 starts unlocked).</summary>
    private List<bool> slotUnlocked = new();

    /// <summary>
    /// Running coroutine handles for auto-return timers, one per slot.
    /// Kept so we can cancel a pending return if the player re-grabs the weapon.
    /// </summary>
    private List<Coroutine> returnCoroutines = new();

    /// <summary>Highest rage level index that has been unlocked so far.</summary>
    private int highestUnlockedLevel = 0;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        // Subscribe to the rage level-up event so we can unlock weapons at the right time
        if (rageMeter != null)
            rageMeter.OnRageLevelUp += HandleRageLevelUp;

        // Initialise per-slot tracking lists to match the number of slot anchors
        for (int i = 0; i < slotAnchors.Length; i++)
        {
            slotWeapons.Add(null);
            slotUnlocked.Add(i == 0); // Only slot 0 is open at the start
            returnCoroutines.Add(null);
        }

        // Register the default bat that is already placed in the scene under Slot 0.
        // WeaponPickup on the bat needs to know which rack and slot it belongs to.
        if (slotAnchors.Length > 0)
        {
            var existingBat = slotAnchors[0].GetComponentInChildren<WeaponPickup>();
            if (existingBat != null)
                RegisterWeaponInSlot(existingBat.gameObject, 0);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent calls on a destroyed object
        if (rageMeter != null)
            rageMeter.OnRageLevelUp -= HandleRageLevelUp;
    }

    /// <summary>
    /// Checks every frame whether any ungripped weapon has drifted too far from its slot.
    /// If so, triggers an immediate recall (no delay).
    /// </summary>
    private void Update()
    {
        CheckWeaponDistances();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by WeaponPickup when the player manually drops a weapon back onto the rack
    /// (the weapon entered the rack's trigger collider while not being held).
    /// Cancels any pending auto-return timer and snaps the weapon to its slot.
    /// </summary>
    public void WeaponReturned(WeaponPickup weapon, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotAnchors.Length) return;

        // A manual return means the auto-return timer is no longer needed
        if (returnCoroutines[slotIndex] != null)
        {
            StopCoroutine(returnCoroutines[slotIndex]);
            returnCoroutines[slotIndex] = null;
        }

        SnapToSlot(weapon.gameObject, slotIndex);
    }

    /// <summary>
    /// Called by WeaponPickup when the player grabs a weapon off the rack.
    /// Starts the auto-return countdown — if the player does not return the weapon
    /// within autoReturnDelay seconds, it will fly back on its own.
    /// </summary>
    public void WeaponPickedUp(WeaponPickup weapon, int slotIndex)
    {
        // Cancel any previously running return coroutine for this slot
        if (returnCoroutines[slotIndex] != null)
            StopCoroutine(returnCoroutines[slotIndex]);

        // Start a fresh countdown
        returnCoroutines[slotIndex] = StartCoroutine(AutoReturnAfterDelay(slotIndex));
    }

    /// <summary>
    /// Associates a weapon GameObject with a specific rack slot.
    /// Initialises the WeaponPickup component on the weapon and snaps it to the slot.
    /// Called on Start for the default bat, and by HandleRageLevelUp for newly spawned weapons.
    /// </summary>
    public void RegisterWeaponInSlot(GameObject weapon, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotAnchors.Length) return;

        slotWeapons[slotIndex] = weapon;

        // Give the weapon's pickup script a reference back to this rack and its slot index
        var pickup = weapon.GetComponent<WeaponPickup>();
        if (pickup != null)
            pickup.Init(this, slotIndex);

        SnapToSlot(weapon, slotIndex);
    }

    // ── Internal Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Responds to a rage level-up event.
    /// If the new level unlocks a weapon (weaponPrefabToUnlock is assigned in the config),
    /// instantiates that weapon at the appropriate slot and plays unlock FX.
    /// </summary>
    private void HandleRageLevelUp(RageLevelConfig.RageLevel level, int levelIndex)
    {
        if (levelConfig == null) return;
        if (levelIndex >= levelConfig.levels.Length) return;

        var rageLevelData = levelConfig.levels[levelIndex];

        // Nothing to unlock at this level
        if (rageLevelData.weaponPrefabToUnlock == null) return;

        // No slot anchor exists for this level index
        if (levelIndex >= slotAnchors.Length) return;

        // Already unlocked — do not spawn a duplicate
        if (slotUnlocked[levelIndex]) return;

        // Mark the slot as open
        slotUnlocked[levelIndex] = true;
        highestUnlockedLevel = Mathf.Max(highestUnlockedLevel, levelIndex);

        // Instantiate the new weapon at the slot's current world position
        var newWeapon = Instantiate(
            rageLevelData.weaponPrefabToUnlock,
            slotAnchors[levelIndex].position,
            slotAnchors[levelIndex].rotation);

        // Register it so the rack owns it and WeaponPickup is initialised
        RegisterWeaponInSlot(newWeapon, levelIndex);

        // Play unlock audio and particles at the slot position
        audioSource?.PlayOneShot(unlockClip);
        if (unlockParticles != null)
        {
            unlockParticles.transform.position = slotAnchors[levelIndex].position;
            unlockParticles.Play();
        }

        Debug.Log($"[WeaponRack] Unlocked slot {levelIndex}: {rageLevelData.weaponUnlockMessage}");
    }

    /// <summary>
    /// Runs every Update to catch weapons that have drifted out of range.
    /// Only acts on weapons that are not currently held by the player.
    /// Recalls immediately (no delay) because the object has left the play area.
    /// </summary>
    private void CheckWeaponDistances()
    {
        for (int i = 0; i < slotWeapons.Count; i++)
        {
            if (slotWeapons[i] == null) continue;

            var pickup = slotWeapons[i].GetComponent<WeaponPickup>();

            // Never pull a weapon out of the player's hand
            if (pickup == null || pickup.IsHeld) continue;

            float dist = Vector3.Distance(
                slotWeapons[i].transform.position,
                slotAnchors[i].position);

            if (dist > autoReturnDistance)
            {
                // Stop any existing timer and start an immediate recall
                if (returnCoroutines[i] != null) StopCoroutine(returnCoroutines[i]);
                returnCoroutines[i] = StartCoroutine(RecallWeapon(slotWeapons[i], i, 0f));
            }
        }
    }

    /// <summary>
    /// Waits for autoReturnDelay seconds, then recalls the weapon if it is still
    /// not being held. The delay gives the player a window to pick it back up.
    /// </summary>
    private IEnumerator AutoReturnAfterDelay(int slotIndex)
    {
        yield return new WaitForSeconds(autoReturnDelay);

        var weapon = slotWeapons[slotIndex];
        if (weapon == null) yield break;

        var pickup = weapon.GetComponent<WeaponPickup>();

        // Player grabbed it again within the window — do not recall
        if (pickup != null && pickup.IsHeld) yield break;

        yield return RecallWeapon(weapon, slotIndex, 0f);
    }

    /// <summary>
    /// Smoothly moves the weapon back to its rack slot over a short duration.
    /// Disables physics during the tween so the weapon travels cleanly without
    /// being deflected by gravity or collisions.
    /// </summary>
    /// <param name="weapon">The weapon GameObject to recall.</param>
    /// <param name="slotIndex">Which slot it belongs to.</param>
    /// <param name="delay">Optional additional wait before the lerp begins.</param>
    private IEnumerator RecallWeapon(GameObject weapon, int slotIndex, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (weapon == null) yield break;

        // Freeze physics so the weapon does not fight the tween
        var rb = weapon.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // Capture start and target pose for the lerp
        float elapsed = 0f;
        float duration = 0.5f;
        Vector3 startPos = weapon.transform.position;
        Quaternion startRot = weapon.transform.rotation;
        Vector3 targetPos = slotAnchors[slotIndex].position;
        Quaternion targetRot = slotAnchors[slotIndex].rotation;

        // Lerp position and rotation simultaneously each frame
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            weapon.transform.position = Vector3.Lerp(startPos, targetPos, t);
            weapon.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        // Final snap to exact slot position and re-enable physics
        SnapToSlot(weapon, slotIndex);
        returnCoroutines[slotIndex] = null;
    }

    /// <summary>
    /// Instantly places a weapon at the exact slot position/rotation and
    /// zeroes out its velocity so it rests cleanly on the rack.
    /// </summary>
    private void SnapToSlot(GameObject weapon, int slotIndex)
    {
        weapon.transform.position = slotAnchors[slotIndex].position;
        weapon.transform.rotation = slotAnchors[slotIndex].rotation;

        var rb = weapon.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;      // Re-enable physics after the tween
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}