using UnityEngine;

public class BreakParticleEffect : MonoBehaviour
{
    [Header("Particle Settings")]
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private float      particleScale = 1f;

    public void PlayEffect()
    {
        if (particlePrefab == null)
        {
            Debug.LogWarning(
                "No particle prefab assigned!");
            return;
        }

        // Spawn particle at object position
        GameObject effect = Instantiate(
            particlePrefab,
            transform.position,
            Quaternion.identity);

        // Scale effect to match object size
        effect.transform.localScale =
            Vector3.one * particleScale;

        // Get particle system duration then destroy
        ParticleSystem ps =
            effect.GetComponent<ParticleSystem>();

        if (ps != null)
        {
            float duration = ps.main.duration +
                             ps.main.startLifetime
                                .constantMax;
            Destroy(effect, duration);
        }
        else
        {
            // Fallback destroy after 2 seconds
            Destroy(effect, 2f);
        }

        Debug.Log($"Break effect played at " +
                  $"{transform.position}");
    }
}