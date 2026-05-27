using UnityEngine;

/// <summary>
/// Add to the bullet prefab used by Shooting.cs.
/// Shooting.cs sets playerIndex before the bullet is fired.
/// On first collision with a breakable, contributes rage for that player.
/// </summary>
public class BulletRageImpact : MonoBehaviour
{
    [HideInInspector] public int playerIndex = 0;

    private bool _hasHit = false;

    void OnCollisionEnter(Collision collision)
    {
        if (_hasHit) return;
        _hasHit = true;

        float impactForce = collision.impulse.magnitude;
        Vector3 hitPoint = collision.GetContact(0).point;
        Vector3 hitDir = -collision.relativeVelocity.normalized;

        var meshBreakable = collision.gameObject.GetComponent<MeshExploderBreakable>();
        if (meshBreakable != null)
        {
            meshBreakable.TakeHit(playerIndex, impactForce, 0f);
            return;
        }

        var destructible = collision.gameObject.GetComponent<DestructibleObject>();
        if (destructible != null)
        {
            destructible.TakeHit(impactForce, 0f, hitPoint, hitDir, playerIndex);
            return;
        }

        var breakable = collision.gameObject.GetComponent<BreakableObject>();
        if (breakable != null)
        {
            breakable.TakeHit(impactForce, hitPoint, hitDir);
            NetworkedRageState.Instance?.RPC_AddRage(playerIndex, 8f);
        }
    }
}
