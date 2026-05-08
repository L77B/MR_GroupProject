using UnityEngine;

/// <summary>
/// ScriptableObject that defines all rage levels for the Rage Room game.
/// 
/// Each level entry specifies:
///   - The rage threshold needed to reach it (0–100 scale)
///   - A display name shown on the HUD ("Furious", "BERSERK" etc.)
///   - A colour for the rage bar fill
///   - Haptic intensity for controller rumble
///   - An optional weapon prefab that unlocks on the rack when this level is reached
///
/// HOW TO CREATE:
///   Right-click in the Project window → Create → RageRoom → Rage Level Config
///   Assign the created asset to RageMeter and WeaponRack in the Inspector.
///
/// IMPORTANT: Keep levels sorted by rageThreshold ascending (0, 20, 40, 60, 80...).
/// </summary>
[CreateAssetMenu(fileName = "RageLevelConfig", menuName = "RageRoom/Rage Level Config")]
public class RageLevelConfig : ScriptableObject
{
    // ── Nested Data Class ─────────────────────────────────────────────────────

    [System.Serializable]
    public class RageLevel
    {
        // ── Identity ──────────────────────────────────────────────────────────

        [Tooltip("Label shown on the HUD when this level is active. E.g. 'Furious', 'BERSERK'.")]
        public string levelName = "Calm";

        // ── Threshold ─────────────────────────────────────────────────────────

        [Tooltip("The rage value (0–100) the player must reach to enter this level.")]
        [Min(0)]
        public float rageThreshold = 0f;

        // ── Visuals ───────────────────────────────────────────────────────────

        [Tooltip("Colour the rage bar fill changes to when this level is active.")]
        public Color hudColor = Color.green;

        // ── Haptics ───────────────────────────────────────────────────────────

        [Tooltip("Controller vibration strength when levelling up to this tier. 0 = none, 1 = max.")]
        [Range(0f, 1f)]
        public float hapticIntensity = 0.3f;

        // ── Weapon Unlock ─────────────────────────────────────────────────────

        [Tooltip("If assigned, this weapon prefab will be spawned on the weapon rack " +
                 "the first time the player reaches this rage level. " +
                 "Leave null if no weapon unlocks at this level.")]
        public GameObject weaponPrefabToUnlock;

        [Tooltip("Optional message displayed when the weapon unlocks, " +
                 "e.g. 'Sledgehammer Unlocked!'. Leave blank if not needed.")]
        public string weaponUnlockMessage = "";
    }

    // ── Level Array ───────────────────────────────────────────────────────────

    [Header("Rage Levels — keep sorted by rageThreshold ascending")]
    [Tooltip("Define each rage tier here. The first entry should always have rageThreshold = 0 " +
             "so there is always a valid starting level.")]
    public RageLevel[] levels = new RageLevel[]
    {
        new RageLevel { levelName = "Calm",       rageThreshold =  0f, hudColor = Color.green,                    hapticIntensity = 0.1f },
        new RageLevel { levelName = "Warming Up", rageThreshold = 20f, hudColor = Color.yellow,                   hapticIntensity = 0.2f },
        new RageLevel { levelName = "Angry",      rageThreshold = 40f, hudColor = new Color(1f, 0.5f, 0f),        hapticIntensity = 0.4f },
        new RageLevel { levelName = "Furious",    rageThreshold = 60f, hudColor = Color.red,                      hapticIntensity = 0.6f },
        new RageLevel { levelName = "Rage Mode",  rageThreshold = 80f, hudColor = new Color(0.6f, 0f, 0f),        hapticIntensity = 1.0f },
    };

    // ── Helper Methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the RageLevel data that corresponds to the given rage value.
    /// Iterates through all levels and returns the last one whose threshold
    /// is less than or equal to the supplied rage value.
    /// </summary>
    /// <param name="rage">Current rage value (0–100).</param>
    public RageLevel GetLevelForRage(float rage)
    {
        // Start from the first (lowest) level as the safe default
        RageLevel current = levels[0];

        // Walk forward; each level that the rage value exceeds becomes the new current
        foreach (var level in levels)
        {
            if (rage >= level.rageThreshold)
                current = level;
        }

        return current;
    }

    /// <summary>
    /// Returns the array index of the level that corresponds to the given rage value.
    /// Used by RageMeter to detect when the player has crossed into a new tier.
    /// </summary>
    /// <param name="rage">Current rage value (0–100).</param>
    public int GetLevelIndexForRage(float rage)
    {
        int index = 0;

        for (int i = 0; i < levels.Length; i++)
        {
            // Every threshold the rage value meets or exceeds advances the index
            if (rage >= levels[i].rageThreshold)
                index = i;
        }

        return index;
    }
}