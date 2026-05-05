using UnityEngine;

public class BreakableObject : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth          = 100f;
    [SerializeField] private float currentHealth;

    [Header("Break Thresholds")]
    [SerializeField] private float lightHitThreshold  = 5f;
    [SerializeField] private float heavyHitThreshold  = 15f;
    [SerializeField] private float breakThreshold     = 25f;

    [Header("Break Settings")]
    [SerializeField] private GameObject[] fracturedPieces;
    [SerializeField] private float pieceExplosionForce = 300f;
    [SerializeField] private float pieceExplosionRadius = 0.5f;
    [SerializeField] private float pieceFadeTime      = 4f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem hitParticles;
    [SerializeField] private ParticleSystem breakParticles;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip lightHitSound;
    [SerializeField] private AudioClip heavyHitSound;
    [SerializeField] private AudioClip breakSound;

    [Header("Physics Response")]
    [SerializeField] private float knockbackMultiplier = 1f;

    private Rigidbody rb;
    private bool isBroken = false;

    void Awake() {
        rb              = GetComponent<Rigidbody>();
        currentHealth   = maxHealth;

        // Hide fractured pieces at start
        if (fracturedPieces != null)
            foreach (var piece in fracturedPieces)
                piece.SetActive(false);
    }

    public void TakeHit(float force, Vector3 hitPoint, Vector3 hitDirection) {
        if (isBroken) return;

        // Apply damage based on force
        float damage = CalculateDamage(force);
        currentHealth -= damage;

        // Apply physical knockback
        ApplyKnockback(force, hitDirection, hitPoint);

        // Determine response based on force level
        if (force >= breakThreshold || currentHealth <= 0) {
            Break(hitPoint, hitDirection, force);
        }
        else if (force >= heavyHitThreshold) {
            HeavyHitResponse(hitPoint, force);
        }
        else if (force >= lightHitThreshold) {
            LightHitResponse(hitPoint);
        }

        Debug.Log($"{gameObject.name} took {damage:F1} damage. " +
                  $"Health: {currentHealth:F1}/{maxHealth}");
    }

    float CalculateDamage(float force) {
        // Scale damage with force — light tap does little,
        // full swing does a lot
        return Mathf.Clamp(force * 3f, 0f, maxHealth);
    }

    void ApplyKnockback(float force, Vector3 direction, Vector3 hitPoint) {
        if (rb == null) return;
        rb.AddForceAtPosition(
            direction * force * knockbackMultiplier,
            hitPoint,
            ForceMode.Impulse);
    }

    void LightHitResponse(Vector3 hitPoint) {
        // Small wobble, light sound, small particle
        if (hitParticles)  hitParticles.Play();
        if (audioSource && lightHitSound)
            audioSource.PlayOneShot(lightHitSound, 0.5f);
    }

    void HeavyHitResponse(Vector3 hitPoint, float force) {
        // Bigger response, heavy sound
        if (hitParticles)  hitParticles.Play();
        if (audioSource && heavyHitSound)
            audioSource.PlayOneShot(heavyHitSound,
                Mathf.Clamp01(force / 20f));
    }

    void Break(Vector3 hitPoint, Vector3 hitDirection, float force) {
        isBroken = true;

        // Hide original mesh
        GetComponent<MeshRenderer>().enabled  = false;
        GetComponent<Collider>().enabled      = false;

        // Play break effects
        if (breakParticles) breakParticles.Play();
        if (audioSource && breakSound)
            audioSource.PlayOneShot(breakSound);

        // Activate and scatter fractured pieces
        if (fracturedPieces != null) {
            foreach (var piece in fracturedPieces) {
                piece.SetActive(true);
                Rigidbody pieceRb = piece.AddComponent<Rigidbody>();

                // Base explosion from hit point
                pieceRb.AddExplosionForce(
                    pieceExplosionForce * (force / 10f),
                    hitPoint,
                    pieceExplosionRadius);

                // Add directional force from swing direction
                pieceRb.AddForce(
                    hitDirection * force * 0.5f,
                    ForceMode.Impulse);

                // Random tumble
                pieceRb.AddTorque(
                    Random.insideUnitSphere * force,
                    ForceMode.Impulse);

                // Cleanup pieces after fade time
                Destroy(piece, pieceFadeTime);
            }
        }

        // Destroy the parent after a short delay
        Destroy(gameObject, pieceFadeTime + 0.1f);
    }

    // Optional — visualise health in editor
    void OnGUI() {
        if (!Application.isPlaying) return;
        // Remove this in production — debug only
        // Shows health above object in game view
    }
}