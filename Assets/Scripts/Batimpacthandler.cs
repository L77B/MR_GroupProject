private void OnCollisionEnter(Collision collision)
{
    if (!isEquipped) return;
    if (!IsSwinging) return;
    if (Time.time - lastHitTime < hitCooldown)
        return;

    float rawForce =
        collision.impulse.magnitude * forceMultiplier;
    float clampedForce =
        Mathf.Clamp(rawForce, 0f, maxForce);

    Vector3 hitPoint =
        collision.GetContact(0).point;
    Vector3 hitDir =
        (transform.position - prevPosition).normalized;

    // Check NetworkedBreakable
    NetworkedBreakable breakable =
        collision.gameObject
            .GetComponent<NetworkedBreakable>();

    if (breakable != null)
    {
        lastHitTime = Time.time;
        Debug.Log($"Hit breakable: " +
                  $"{collision.gameObject.name} " +
                  $"force: {clampedForce:F1}");
        breakable.TakeHit(clampedForce);
        return;
    }

    // Check DestructibleObject (fallback)
    DestructibleObject destructible =
        collision.gameObject
            .GetComponent<DestructibleObject>();

    if (destructible != null)
    {
        lastHitTime = Time.time;
        destructible.TakeHit(
            clampedForce,
            CurrentSwingSpeed,
            hitPoint,
            hitDir);
    }
}