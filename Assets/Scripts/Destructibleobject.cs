using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DestructibleObject : MonoBehaviour
{
    // ── Inspector Config ──────────────────────────────────────────────────────

    [Header("Health")]
    [SerializeField] private float maxHealth        = 100f;
    [SerializeField] private float lightHitThreshold = 5f;
    [SerializeField] private float heavyHitThreshold = 15f;
    [SerializeField] private float breakThreshold    = 25f;

    [Header("Fragments")]
    [SerializeField] private GameObject[] fracturedPieces;
    [SerializeField] private float explosionForce  = 300f;
    [SerializeField] private float explosionRadius = 1f;
    [SerializeField] private float cleanupDelay    = 4f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem hitParticles;
    [SerializeField] private ParticleSystem breakParticles;
    [SerializeField] private AudioSource    audioSource;
    [SerializeField] private AudioClip      lightHitSound;
    [SerializeField] private AudioClip      heavyHitSound;
    [SerializeField] private AudioClip      breakSound;

    [Header("Rage System")]
    [SerializeField] private RageMeter rageMeter;

    [Header("Health Label")]
    [SerializeField] private HealthLabel healthLabel;

    [Header("Physics")]
    [SerializeField] private float knockbackMultiplier = 1f;
    public GameObject explosionPrefab;
public Transform explosionSpawnPoint;

    // ── OnBroken Event ────────────────────────────────────────────────────────
    // ObjectSpawner subscribes to this to know when to respawn
    public event System.Action<GameObject> OnBroken;

    // ── Public Setters ────────────────────────────────────────────────────────

    public void SetRageMeter(RageMeter meter) { rageMeter = meter; }

    // ── Runtime State ─────────────────────────────────────────────────────────

    private float      currentHealth;
    private bool       isBroken;
    private Rigidbody  rb;
    private Renderer[] renderers;
    private Collider[] colliders;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        rb        = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();

        currentHealth = maxHealth;
        SetFragmentsActive(false);

        if (healthLabel == null)
            healthLabel = GetComponentInChildren<HealthLabel>();

        if (healthLabel != null)
            healthLabel.Initialise(maxHealth);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <param name="playerIndex">0 = P1 (host), 1 = P2 (client). Routes rage to the correct bar.</param>
    public void TakeHit(float force, float swingSpeed,
                        Vector3 hitPoint, Vector3 hitDirection,
                        int playerIndex = 0)
    {
        if (isBroken) return;

        float damage  = Mathf.Clamp(force * 3f, 0f, maxHealth);
        currentHealth -= damage;

        ApplyKnockback(force, hitPoint, hitDirection);

        if (healthLabel != null)
            healthLabel.UpdateHealth(currentHealth, maxHealth);

        bool willBreak = force >= breakThreshold ||
                         currentHealth <= 0f;

        // Only award rage when the object actually breaks (flat value per break).
        if (willBreak)
            NetworkedRageState.Instance?.AddRage(playerIndex, 10f);

        if (willBreak)
        {
            Break(hitPoint, hitDirection, force);
        }
        else if (force >= heavyHitThreshold)
        {
            PlayHeavyHitEffect(force);
        }
        else if (force >= lightHitThreshold)
        {
            PlayLightHitEffect();
        }

        Debug.Log($"[DestructibleObject] {name} — " +
                  $"P{playerIndex + 1} damage:{damage:F1}  " +
                  $"HP:{currentHealth:F1}/{maxHealth}  " +
                  $"willBreak:{willBreak}");
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private void ApplyKnockback(float force,
                                 Vector3 hitPoint,
                                 Vector3 hitDirection)
    {
        if (rb == null) return;
        rb.AddForceAtPosition(
            hitDirection.normalized * force * knockbackMultiplier,
            hitPoint,
            ForceMode.Impulse);
    }

    private void PlayLightHitEffect()
    {
        hitParticles?.Play();
        audioSource?.PlayOneShot(lightHitSound, 0.5f);
    }

    private void PlayHeavyHitEffect(float force)
    {
        hitParticles?.Play();
        if (audioSource != null && heavyHitSound != null)
        {
            float volume = Mathf.Clamp01(force / 20f);
            audioSource.PlayOneShot(heavyHitSound, volume);
        }
    }

    private void Break(Vector3 hitPoint,
                       Vector3 hitDirection,
                       float force)
    {
        isBroken = true;

        SetOriginalVisible(false);
        SetOriginalColliders(false);

        if (breakParticles != null) breakParticles.Play();
        audioSource?.PlayOneShot(breakSound);

        ActivateFragments(hitPoint, hitDirection, force);

        // Notify ObjectWaveManager
        ObjectWaveManager.Instance?.OnObjectBroken(this);

        // ── Fire OnBroken event so ObjectSpawner can respawn ──
        OnBroken?.Invoke(gameObject);

        Destroy(gameObject, cleanupDelay + 0.2f);
    }

    private void ActivateFragments(Vector3 hitPoint,
                                    Vector3 hitDirection,
                                    float force)
    {
        if (fracturedPieces == null) return;

        foreach (var piece in fracturedPieces)
        {
            if (piece == null) continue;

            piece.SetActive(true);

            var pieceRb = piece.GetComponent<Rigidbody>()
                          ?? piece.AddComponent<Rigidbody>();

            if (piece.GetComponent<Collider>() == null)
                piece.AddComponent<BoxCollider>();

            pieceRb.AddExplosionForce(
                explosionForce * Mathf.Clamp(force / 10f, 1f, 5f),
                hitPoint,
                explosionRadius,
                0.2f,
                ForceMode.Impulse);

            pieceRb.AddForce(
                hitDirection.normalized * force * 0.5f,
                ForceMode.Impulse);

            pieceRb.AddTorque(
                Random.insideUnitSphere * force,
                ForceMode.Impulse);

            Destroy(piece, cleanupDelay);
        }
    }

    // ── Visibility / Collider Helpers ─────────────────────────────────────────

    private void SetFragmentsActive(bool active)
    {
        if (fracturedPieces == null) return;
        foreach (var piece in fracturedPieces)
            if (piece != null) piece.SetActive(active);
    }

    private void SetOriginalVisible(bool visible)
    {
        foreach (var rend in renderers)
        {
            if (rend == null || IsFragment(rend.gameObject))
                continue;
            rend.enabled = visible;
        }
    }

    private void SetOriginalColliders(bool enabled)
    {
        foreach (var col in colliders)
        {
            if (col == null || IsFragment(col.gameObject))
                continue;
            col.enabled = enabled;
        }
    }

    private bool IsFragment(GameObject obj)
    {
        if (fracturedPieces == null) return false;
        foreach (var piece in fracturedPieces)
            if (piece == obj) return true;
        return false;
    }
    public void BreakNow()
{
    if (isBroken) return;

    Vector3 fakeHitPoint = transform.position;
    Vector3 fakeDirection = Vector3.up;
    float fakeForce = breakThreshold + 1f;

    Break(fakeHitPoint, fakeDirection, fakeForce);
    Instantiate(explosionPrefab, explosionSpawnPoint.position, explosionSpawnPoint.rotation);
    Debug.Log("Explosion triggered!");
}

}