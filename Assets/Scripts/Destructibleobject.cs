using UnityEngine;

/// <summary>
/// Attach to any breakable prop in the Rage Room (cube, vase, TV, etc.).
///
/// RESPONSIBILITIES
/// ────────────────
/// 1. Receive hit data from BatImpactHandler via TakeHit().
/// 2. Reduce health and apply physical knockback on each hit.
/// 3. Play light or heavy hit effects depending on force.
/// 4. When health reaches zero (or force exceeds breakThreshold in one blow),
///    destroy the object: hide the mesh, scatter fragment pieces, play break FX.
/// 5. Report each hit — including whether it was fatal — to RageMeter.
/// 6. Notify ObjectWaveManager when destroyed so the wave system can track progress.
///
/// SETUP
/// ─────
/// - Add a Rigidbody (not kinematic) so the object can be knocked around.
/// - Assign fracturedPieces: child GameObjects that are hidden at start and
///   activated with explosion force when the object breaks.
/// - Assign rageMeter in the Inspector OR leave empty and let ObjectWaveManager
///   inject it at runtime via SetRageMeter().
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DestructibleObject : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("Health")]
    [Tooltip("Starting health points. Object breaks when this reaches zero.")]
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Minimum force required to trigger the light hit reaction (particles + quiet sound).")]
    [SerializeField] private float lightHitThreshold = 5f;

    [Tooltip("Minimum force required to trigger the heavy hit reaction (louder sound, more particles).")]
    [SerializeField] private float heavyHitThreshold = 15f;

    [Tooltip("If a single hit's force meets or exceeds this value, the object breaks instantly " +
             "regardless of remaining health.")]
    [SerializeField] private float breakThreshold = 25f;

    [Header("Fragments")]
    [Tooltip("Pre-fractured child GameObjects. Hidden at start; activated and scattered on break.")]
    [SerializeField] private GameObject[] fracturedPieces;

    [Tooltip("How strongly the fragments are blasted outward from the hit point.")]
    [SerializeField] private float explosionForce = 300f;

    [Tooltip("Radius of the explosion that scatters fragments.")]
    [SerializeField] private float explosionRadius = 1f;

    [Tooltip("Seconds before fragment GameObjects are automatically destroyed (for performance).")]
    [SerializeField] private float cleanupDelay = 4f;

    [Header("Effects")]
    [Tooltip("Particle system played on light and heavy hits.")]
    [SerializeField] private ParticleSystem hitParticles;

    [Tooltip("Particle system played when the object fully breaks.")]
    [SerializeField] private ParticleSystem breakParticles;

    [Tooltip("AudioSource on this GameObject used to play all hit and break sounds.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Sound played on a light hit.")]
    [SerializeField] private AudioClip lightHitSound;

    [Tooltip("Sound played on a heavy hit; volume scales with force.")]
    [SerializeField] private AudioClip heavyHitSound;

    [Tooltip("Sound played when the object breaks apart.")]
    [SerializeField] private AudioClip breakSound;

    [Header("Rage System")]
    [Tooltip("The RageMeter that receives hit data. " +
             "Can be left empty in a prefab — ObjectWaveManager injects this at spawn time.")]
    [SerializeField] private RageMeter rageMeter;

    [Header("Health Label")]
    [Tooltip("Optional HealthLabel component on a child World Space Canvas. " +
             "If assigned, the label updates automatically on every hit. " +
             "Leave empty if you do not want a health display on this object.")]
    [SerializeField] private HealthLabel healthLabel;

    [Header("Physics")]
    [Tooltip("Multiplier applied to the knockback impulse. Increase to make the object fly further.")]
    [SerializeField] private float knockbackMultiplier = 1f;

    // ── Public Setters ────────────────────────────────────────────────────────

    /// <summary>
    /// Injects the RageMeter reference at runtime.
    /// Called by ObjectWaveManager immediately after spawning this object
    /// so it does not need to be pre-assigned inside the prefab.
    /// </summary>
    public void SetRageMeter(RageMeter meter) { rageMeter = meter; }

    // ── Runtime State ─────────────────────────────────────────────────────────

    private float currentHealth; // Remaining health; counts down to 0
    private bool isBroken;      // Guard flag — prevents Break() running twice
    private Rigidbody rb;
    private Renderer[] renderers;    // Cached for fast mesh visibility toggling
    private Collider[] colliders;    // Cached for fast collider toggling on break

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Cache component references once to avoid repeated GetComponent calls
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();

        // Start at full health
        currentHealth = maxHealth;

        // Fragment pieces must be invisible and inactive at the start;
        // they are only revealed when the object breaks
        SetFragmentsActive(false);

        // Auto-find HealthLabel on a child Canvas if not assigned in Inspector.
        // This means you do not need to manually wire it up on every prefab instance.
        if (healthLabel == null)
            healthLabel = GetComponentInChildren<HealthLabel>();

        // Initialise the label with the starting health value so it shows
        // full health when the object first appears in the scene
        if (healthLabel != null)
            healthLabel.Initialise(maxHealth);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Entry point called by BatImpactHandler when a valid swing collision occurs.
    /// Processes damage, physics response, effects, and rage reporting.
    /// </summary>
    /// <param name="force">
    ///   Scaled collision impulse from BatImpactHandler.
    ///   Used for damage calculation, knockback, and choosing the hit reaction tier.
    /// </param>
    /// <param name="swingSpeed">
    ///   Bat tip speed in m/s. Passed through to RageMeter for combo scoring.
    /// </param>
    /// <param name="hitPoint">
    ///   World-space position of the first contact point. Used to apply directional knockback.
    /// </param>
    /// <param name="hitDirection">
    ///   Normalised direction the bat was travelling. Used for knockback and fragment scatter.
    /// </param>
    public void TakeHit(float force, float swingSpeed, Vector3 hitPoint, Vector3 hitDirection)
    {
        // Do nothing if the object has already been destroyed this frame
        if (isBroken) return;

        // Convert force into a health damage amount (capped at full health)
        float damage = Mathf.Clamp(force * 3f, 0f, maxHealth);
        currentHealth -= damage;

        // Push the object physically in the swing direction
        ApplyKnockback(force, hitPoint, hitDirection);

        // Update the health label so the player can see damage feedback
        if (healthLabel != null)
            healthLabel.UpdateHealth(currentHealth, maxHealth);

        // Decide whether this hit is fatal before reporting to the rage system
        bool willBreak = force >= breakThreshold || currentHealth <= 0f;

        // Inform the rage meter — it scores both regular hits and break bonuses
        rageMeter?.RegisterHit(force, swingSpeed, willBreak);

        // Branch into the appropriate reaction
        if (willBreak)
        {
            Break(hitPoint, hitDirection, force);
        }
        else if (force >= heavyHitThreshold)
        {
            // Strong hit but object survives — louder audio, volume scales with force
            PlayHeavyHitEffect(force);
        }
        else if (force >= lightHitThreshold)
        {
            // Weak but registered hit — minimal feedback
            PlayLightHitEffect();
        }
        // Hits below lightHitThreshold produce no feedback (accidental grazes)

        Debug.Log($"[DestructibleObject] {name} — damage:{damage:F1}  " +
                  $"HP:{currentHealth:F1}/{maxHealth}  willBreak:{willBreak}");
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Applies a physics impulse at the exact hit contact point,
    /// pushing the object in the bat's swing direction.
    /// </summary>
    private void ApplyKnockback(float force, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (rb == null) return;

        rb.AddForceAtPosition(
            hitDirection.normalized * force * knockbackMultiplier,
            hitPoint,
            ForceMode.Impulse);
    }

    /// <summary>
    /// Visual and audio feedback for hits below the heavy threshold.
    /// Plays particles at half volume so it feels like a glancing blow.
    /// </summary>
    private void PlayLightHitEffect()
    {
        hitParticles?.Play();
        audioSource?.PlayOneShot(lightHitSound, 0.5f);
    }

    /// <summary>
    /// Visual and audio feedback for hard hits that do not break the object.
    /// Volume is proportional to force so big hits sound correspondingly louder.
    /// </summary>
    private void PlayHeavyHitEffect(float force)
    {
        hitParticles?.Play();

        if (audioSource != null && heavyHitSound != null)
        {
            // Normalise force against a reference of 20 units; cap at full volume
            float volume = Mathf.Clamp01(force / 20f);
            audioSource.PlayOneShot(heavyHitSound, volume);
        }
    }

    /// <summary>
    /// Destroys the object visually and physically:
    ///   1. Sets the broken flag to prevent re-entry.
    ///   2. Hides the original mesh and disables its colliders.
    ///   3. Plays break particles and audio.
    ///   4. Activates and scatters the fragment pieces with explosion physics.
    ///   5. Notifies ObjectWaveManager so it can count the broken object.
    ///   6. Schedules destruction of the parent GameObject.
    /// </summary>
    private void Break(Vector3 hitPoint, Vector3 hitDirection, float force)
    {
        isBroken = true;

        // Hide the intact mesh so only fragments are visible
        SetOriginalVisible(false);

        // Disable the original collider so nothing collides with the now-invisible mesh
        SetOriginalColliders(false);

        // Break particle burst at object position
        if (breakParticles != null) breakParticles.Play();
        audioSource?.PlayOneShot(breakSound);

        // Scatter all fragment pieces outward from the hit point
        ActivateFragments(hitPoint, hitDirection, force);

        // Tell the wave manager one object has been destroyed
        ObjectWaveManager.Instance?.OnObjectBroken(this);

        // Delay destruction slightly beyond cleanupDelay so fragment Destroy()
        // calls (which use cleanupDelay) complete first
        Destroy(gameObject, cleanupDelay + 0.2f);
    }

    /// <summary>
    /// Activates each fragment piece and applies:
    ///   - An outward explosion force centred on the hit point.
    ///   - A directional push along the swing direction.
    ///   - A random spin torque for visual variety.
    /// Each piece is scheduled for destruction after cleanupDelay seconds
    /// to avoid cluttering the scene with physics objects indefinitely.
    /// </summary>
    private void ActivateFragments(Vector3 hitPoint, Vector3 hitDirection, float force)
    {
        if (fracturedPieces == null) return;

        foreach (var piece in fracturedPieces)
        {
            if (piece == null) continue;

            piece.SetActive(true);

            // Add a Rigidbody if the piece does not already have one
            var pieceRb = piece.GetComponent<Rigidbody>() ?? piece.AddComponent<Rigidbody>();

            // Add a box collider if none exists (minimum needed for physics)
            if (piece.GetComponent<Collider>() == null)
                piece.AddComponent<BoxCollider>();

            // Scale explosion force by how hard the hit was (1× at force=10, capped at 5×)
            pieceRb.AddExplosionForce(
                explosionForce * Mathf.Clamp(force / 10f, 1f, 5f),
                hitPoint,
                explosionRadius,
                0.2f,           // Upward modifier — slight vertical lift
                ForceMode.Impulse);

            // Extra push in the swing direction so fragments fly the way the bat swings
            pieceRb.AddForce(hitDirection.normalized * force * 0.5f, ForceMode.Impulse);

            // Random spin to make each fragment tumble differently
            pieceRb.AddTorque(Random.insideUnitSphere * force, ForceMode.Impulse);

            // Clean up the fragment after it has had time to settle
            Destroy(piece, cleanupDelay);
        }
    }

    // ── Visibility / Collider Helpers ─────────────────────────────────────────

    /// <summary>
    /// Enables or disables all Renderer components on this object,
    /// skipping any that belong to fragment pieces (those are controlled separately).
    /// </summary>
    private void SetFragmentsActive(bool active)
    {
        if (fracturedPieces == null) return;
        foreach (var piece in fracturedPieces)
            if (piece != null) piece.SetActive(active);
    }

    /// <summary>
    /// Shows or hides the original intact mesh renderers.
    /// Fragment renderers are skipped — they are managed by ActivateFragments().
    /// </summary>
    private void SetOriginalVisible(bool visible)
    {
        foreach (var rend in renderers)
        {
            if (rend == null || IsFragment(rend.gameObject)) continue;
            rend.enabled = visible;
        }
    }

    /// <summary>
    /// Enables or disables the original colliders on this object.
    /// Fragment colliders are skipped so they can still interact with the floor.
    /// </summary>
    private void SetOriginalColliders(bool enabled)
    {
        foreach (var col in colliders)
        {
            if (col == null || IsFragment(col.gameObject)) continue;
            col.enabled = enabled;
        }
    }

    /// <summary>
    /// Returns true if the given GameObject is one of the registered fragment pieces.
    /// Used to avoid accidentally toggling fragment renderers/colliders as part of the original mesh.
    /// </summary>
    private bool IsFragment(GameObject obj)
    {
        if (fracturedPieces == null) return false;
        foreach (var piece in fracturedPieces)
            if (piece == obj) return true;
        return false;
    }
}