using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
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
/// MULTIPLAYER UNLOCK CHANGE
/// ──────────────────────────
/// Previously WeaponRack subscribed directly to a single RageMeter.OnRageLevelUp.
/// This only worked for Player 1 and would not fire when Player 2 hit a new level.
///
/// Now WeaponRack subscribes to GameManager.OnSharedRageLevelUp — a static event
/// that GameManager fires after applying the unlock gate (either player, or both
/// players must reach the tier, depending on GameManager.unlockOnEitherPlayer).
///
/// The rageMeter Inspector field is kept for backwards compatibility but is no
/// longer used for unlock subscriptions. You can leave it assigned or clear it.
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
/// 1. Create child empty GameObjects on the rack as slot anchors: "Slot_0", "Slot_1"...
///    Position them where weapons should rest on the rack.
/// 2. Add a sphere trigger Collider to each slot anchor, tag it "WeaponRackSlot".
/// 3. Assign slotAnchors array in Inspector (drag each child in order).
/// 4. Assign levelConfig (RageLevelConfig ScriptableObject).
/// 5. Assign defaultBat (the Baseball Bat GameObject in the scene).
/// 6. In RageLevelConfig, set weaponPrefabToUnlock on level[2] = Paintball_Maker,
///    and level[4] = swagger/sledge prefab.
/// 7. rageMeter field is no longer used for unlocks — can be cleared or left assigned.
/// </summary>
public class WeaponRack : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("No longer used for weapon unlock subscriptions — GameManager.OnSharedRageLevelUp " +
             "now drives unlocks so both players can trigger them. " +
             "This field is kept for backwards compatibility only.")]
    [SerializeField] private RageMeter rageMeter;

    [Tooltip("ScriptableObject containing rage level thresholds and weapon unlock data. " +
             "Assign weaponPrefabToUnlock on level[2] and level[4] to unlock paintball and swagger.")]
    [SerializeField] private RageLevelConfig levelConfig;

    [Header("Slots")]
    [Tooltip("Child Transform anchors that define where each weapon rests on the rack. " +
             "Index 0 = default bat slot. Remaining indices match rage level indices in RageLevelConfig.")]
    [SerializeField] private Transform[] slotAnchors;

    [Header("Auto-Return Settings")]
    [Tooltip("If an ungripped weapon travels further than this (metres) from its slot, " +
             "it is immediately recalled. Set large (e.g. 10) for floor/table placement " +
             "so the weapon is not pulled back before the player reaches it.")]
    [SerializeField] private float autoReturnDistance = 10f;

    [Tooltip("Seconds after a weapon is released before it returns to its slot. " +
             "Give the player enough time to re-grab before the weapon flies back.")]
    [SerializeField] private float autoReturnDelay = 8f;

    [Tooltip("If true, auto-return is completely disabled. The weapon stays wherever " +
             "it lands. Correct when weapons are placed on floor or table for players " +
             "to walk to and pick up naturally.")]
    [SerializeField] private bool disableAutoReturn = false;

    [Header("Default Weapon")]
    [Tooltip("Drag the Baseball Bat GameObject here. It sits at scene root so cannot " +
             "be found via GetComponentInChildren. This explicit reference registers it " +
             "with Slot_0 on Start.")]
    [SerializeField] private GameObject defaultBat;

    [Header("Unlock FX")]
    [Tooltip("AudioSource used to play the weapon unlock sound.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Sound played when a new weapon slot is unlocked.")]
    [SerializeField] private AudioClip unlockClip;

    [Tooltip("Particle system that fires at the newly unlocked slot position.")]
    [SerializeField] private ParticleSystem unlockParticles;

    // ── Runtime State ─────────────────────────────────────────────────────────

    /// <summary>The weapon GameObject currently occupying each slot (null if empty).</summary>
    private List<GameObject> slotWeapons = new();

    /// <summary>Whether each slot has been unlocked. Slot 0 starts unlocked.</summary>
    private List<bool> slotUnlocked = new();

    /// <summary>
    /// Running coroutine handles for auto-return timers, one per slot.
    /// Kept so we can cancel a pending return if the player re-grabs the weapon.
    /// </summary>
    private List<Coroutine> returnCoroutines = new();

    /// <summary>Highest rage level index unlocked so far.</summary>
    private int highestUnlockedLevel = 0;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        // ── Subscribe to the SHARED unlock event from GameManager ─────────────
        // GameManager.OnSharedRageLevelUp fires when EITHER player (or BOTH,
        // depending on GameManager.unlockOnEitherPlayer) reaches a new rage tier.
        // This replaces the old direct rageMeter.OnRageLevelUp subscription which
        // only worked for Player 1.
        GameManager.OnSharedRageLevelUp += HandleRageLevelUp;

        // ── Initialise per-slot tracking lists ────────────────────────────────
        for (int i = 0; i < slotAnchors.Length; i++)
        {
            slotWeapons.Add(null);
            slotUnlocked.Add(i == 0); // Only slot 0 unlocked at start
            returnCoroutines.Add(null);
        }

        // ── Register the default bat in Slot 0 ───────────────────────────────
        // The bat sits at the scene root so GetComponentInChildren on Slot_0
        // cannot find it. We use the explicit defaultBat reference instead.
        if (slotAnchors.Length > 0)
        {
            if (defaultBat != null)
            {
                RegisterWeaponInSlot(defaultBat, 0);
                Debug.Log("[WeaponRack] Default bat registered in Slot_0.");
            }
            else
            {
                // Fallback — search Slot_0 children (bat parented under rack)
                var existingBat = slotAnchors[0].GetComponentInChildren<WeaponPickup>();
                if (existingBat != null)
                {
                    RegisterWeaponInSlot(existingBat.gameObject, 0);
                    Debug.Log("[WeaponRack] Default bat found via GetComponentInChildren.");
                }
                else
                {
                    Debug.LogWarning("[WeaponRack] No bat found for Slot_0. " +
                                     "Assign the bat to the 'Default Bat' field in the Inspector.");
                }
            }
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from both events to prevent callbacks on a destroyed object
        GameManager.OnSharedRageLevelUp -= HandleRageLevelUp;
    }

    /// <summary>
    /// Checks every frame whether any ungripped weapon has drifted too far.
    /// If so, triggers an immediate recall (no delay).
    /// </summary>
    private void Update()
    {
        CheckWeaponDistances();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by WeaponPickup when the player manually drops a weapon onto the rack
    /// (weapon entered the rack's trigger collider while not held).
    /// Cancels any pending auto-return timer and snaps the weapon to its slot.
    /// </summary>
    public void WeaponReturned(WeaponPickup weapon, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotAnchors.Length) return;

        if (returnCoroutines[slotIndex] != null)
        {
            StopCoroutine(returnCoroutines[slotIndex]);
            returnCoroutines[slotIndex] = null;
        }

        SnapToSlot(weapon.gameObject, slotIndex);
    }

    /// <summary>
    /// Called by WeaponPickup when the player grabs a weapon.
    /// Starts the auto-return countdown unless disableAutoReturn is true.
    /// </summary>
    public void WeaponPickedUp(WeaponPickup weapon, int slotIndex)
    {
        if (disableAutoReturn) return;

        if (returnCoroutines[slotIndex] != null)
            StopCoroutine(returnCoroutines[slotIndex]);

        returnCoroutines[slotIndex] = StartCoroutine(AutoReturnAfterDelay(slotIndex));
    }

    /// <summary>
    /// Associates a weapon GameObject with a specific rack slot.
    /// Initialises the WeaponPickup component and snaps the weapon to the slot.
    /// Called on Start for the default bat, and by HandleRageLevelUp for unlocked weapons.
    /// </summary>
    public void RegisterWeaponInSlot(GameObject weapon, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotAnchors.Length) return;

        slotWeapons[slotIndex] = weapon;

        var pickup = weapon.GetComponent<WeaponPickup>();
        if (pickup != null)
            pickup.Init(this, slotIndex);

        SnapToSlot(weapon, slotIndex);
    }

    /// <summary>
    /// Places the default bat near the player on the closest floor or table surface.
    /// Called by GameFlowManager after the player confirms their play area position.
    ///
    /// HOW IT WORKS
    /// ─────────────
    /// 1. Tries a TABLE surface near the player via MRUK.
    /// 2. Falls back to a floor position 0.8m in front of the player.
    /// 3. Moves Slot_0 anchor to match so auto-return (if enabled) brings it back
    ///    to this reachable position rather than the original rack location.
    /// </summary>
    public void PlaceBatNearPlayer()
    {
        if (defaultBat == null || slotAnchors.Length == 0)
        {
            Debug.LogWarning("[WeaponRack] Cannot place bat — defaultBat or " +
                             "slotAnchors not assigned.");
            return;
        }

        Vector3 targetPosition = Vector3.zero;
        Quaternion targetRotation = Quaternion.identity;
        bool positionFound = false;

        // ── Try table surface near player ─────────────────────────────────────
        MRUKRoom room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;
        if (room != null)
        {
            if (room.GenerateRandomPositionOnSurface(
                MRUK.SurfaceType.FACING_UP,
                0.1f,
                new LabelFilter(MRUKAnchor.SceneLabels.TABLE),
                out Vector3 tablePos,
                out Vector3 tableNormal))
            {
                targetPosition = tablePos + tableNormal * 0.05f;
                targetRotation = Quaternion.FromToRotation(Vector3.up, tableNormal);
                positionFound = true;
                Debug.Log("[WeaponRack] Bat placed on table surface.");
            }
        }

        // ── Fallback: floor 0.8m in front of player ───────────────────────────
        if (!positionFound && Camera.main != null)
        {
            Vector3 forward = Camera.main.transform.forward;
            forward.y = 0f;
            forward = forward.normalized;

            Vector3 floorPos = Camera.main.transform.position + forward * 0.8f;
            floorPos.y = 0f;

            Collider batCol = defaultBat.GetComponent<Collider>();
            float halfHeight = batCol != null ? batCol.bounds.extents.y : 0.1f;
            floorPos.y += halfHeight;

            targetPosition = floorPos;
            targetRotation = Quaternion.Euler(
                0f, Camera.main.transform.eulerAngles.y, 0f);
            positionFound = true;
            Debug.Log("[WeaponRack] No table found. Bat placed on floor in front of player.");
        }

        if (!positionFound)
        {
            Debug.LogWarning("[WeaponRack] Could not find a position for the bat.");
            return;
        }

        // Move Slot_0 anchor so auto-return targets the same reachable spot
        slotAnchors[0].position = targetPosition;
        slotAnchors[0].rotation = targetRotation;

        SnapToSlot(defaultBat, 0);
        Debug.Log($"[WeaponRack] Bat placed at {targetPosition}");
    }

    // ── Internal Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Responds to GameManager.OnSharedRageLevelUp.
    ///
    /// CALLED BY: GameManager — after applying the two-player unlock gate.
    ///            Fires when either player (or both, depending on settings)
    ///            crosses into a new rage tier.
    ///
    /// DUPLICATE GUARD: slotUnlocked[levelIndex] ensures a weapon is never
    /// spawned twice even if the event fires from both P1 and P2 in the same
    /// frame (e.g. both players break something simultaneously).
    ///
    /// IMPORTANT — DO NOT RENAME THIS METHOD without also updating the
    /// subscription in Start() and OnDestroy().
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

        // Already unlocked — duplicate-guard prevents double-spawning
        if (slotUnlocked[levelIndex]) return;

        // Mark the slot as unlocked before instantiating (prevents race conditions)
        slotUnlocked[levelIndex] = true;
        highestUnlockedLevel = Mathf.Max(highestUnlockedLevel, levelIndex);

        // Instantiate the weapon prefab at the slot anchor position
        var newWeapon = Instantiate(
            rageLevelData.weaponPrefabToUnlock,
            slotAnchors[levelIndex].position,
            slotAnchors[levelIndex].rotation);

        // Register it with the rack so WeaponPickup is initialised
        RegisterWeaponInSlot(newWeapon, levelIndex);

        // Play unlock audio and particles
        audioSource?.PlayOneShot(unlockClip);
        if (unlockParticles != null)
        {
            unlockParticles.transform.position = slotAnchors[levelIndex].position;
            unlockParticles.Play();
        }

        string msg = string.IsNullOrEmpty(rageLevelData.weaponUnlockMessage)
            ? rageLevelData.weaponPrefabToUnlock.name
            : rageLevelData.weaponUnlockMessage;

        Debug.Log($"[WeaponRack] Slot {levelIndex} unlocked: {msg}");
    }

    /// <summary>
    /// Checks every frame if any free weapon has drifted past autoReturnDistance.
    /// Triggers an immediate recall if so. Skipped when disableAutoReturn is true.
    /// </summary>
    private void CheckWeaponDistances()
    {
        if (disableAutoReturn) return;

        for (int i = 0; i < slotWeapons.Count; i++)
        {
            if (slotWeapons[i] == null) continue;

            var pickup = slotWeapons[i].GetComponent<WeaponPickup>();

            // Never pull a weapon the player is holding
            if (pickup == null || pickup.IsHeld) continue;

            float dist = Vector3.Distance(
                slotWeapons[i].transform.position,
                slotAnchors[i].position);

            if (dist > autoReturnDistance)
            {
                if (returnCoroutines[i] != null) StopCoroutine(returnCoroutines[i]);
                returnCoroutines[i] = StartCoroutine(RecallWeapon(slotWeapons[i], i, 0f));
            }
        }
    }

    /// <summary>
    /// Waits autoReturnDelay seconds then recalls the weapon if not re-grabbed.
    /// Skipped entirely when disableAutoReturn is true.
    /// </summary>
    private IEnumerator AutoReturnAfterDelay(int slotIndex)
    {
        if (disableAutoReturn) yield break;

        yield return new WaitForSeconds(autoReturnDelay);

        var weapon = slotWeapons[slotIndex];
        if (weapon == null) yield break;

        var pickup = weapon.GetComponent<WeaponPickup>();
        if (pickup != null && pickup.IsHeld) yield break;

        yield return RecallWeapon(weapon, slotIndex, 0f);
    }

    /// <summary>
    /// Smoothly lerps a weapon back to its rack slot over 0.5 seconds.
    /// Disables physics during the tween to prevent gravity interference.
    /// </summary>
    private IEnumerator RecallWeapon(GameObject weapon, int slotIndex, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (weapon == null) yield break;

        var rb = weapon.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        float elapsed = 0f;
        const float duration = 0.5f;
        Vector3 startPos = weapon.transform.position;
        Quaternion startRot = weapon.transform.rotation;
        Vector3 targetPos = slotAnchors[slotIndex].position;
        Quaternion targetRot = slotAnchors[slotIndex].rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            weapon.transform.position = Vector3.Lerp(startPos, targetPos, t);
            weapon.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        SnapToSlot(weapon, slotIndex);
        returnCoroutines[slotIndex] = null;
    }

    /// <summary>
    /// Instantly places a weapon at the exact slot position/rotation and
    /// zeroes its velocity so it rests cleanly on the rack.
    /// </summary>
    private void SnapToSlot(GameObject weapon, int slotIndex)
    {
        weapon.transform.position = slotAnchors[slotIndex].position;
        weapon.transform.rotation = slotAnchors[slotIndex].rotation;

        var rb = weapon.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}