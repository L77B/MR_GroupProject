using UnityEngine;
using System.Collections;
using Fusion;

public class Shooting : MonoBehaviour
{
    public GameObject shot;
    public GameObject hit;

    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;

    [HideInInspector] public int playerIndex = 0;

    void Start()
    {
        PreventAutoDestroyOnStop(hit);

        if (shot != null) shot.SetActive(false);
        if (hit  != null) hit.SetActive(false);

        StartCoroutine(ResolvePlayerIndex());
    }

    IEnumerator ResolvePlayerIndex()
    {
        NetworkRunner runner = null;
        yield return new WaitUntil(() => {
            runner = FindFirstObjectByType<NetworkRunner>();
            return runner != null && runner.IsRunning;
        });

        float waited = 0f;
        while ((NetworkedRageState.Instance == null ||
                !NetworkedRageState.Instance.IsSpawned) && waited < 15f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(1.5f);

        playerIndex = NetworkedRageState.Instance != null
            ? NetworkedRageState.Instance.GetPlayerIndex(runner.LocalPlayer)
            : (runner.IsSharedModeMasterClient ? 0 : 1);

        Debug.Log($"[Shooting] {gameObject.name} → PlayerIndex={playerIndex}");
    }

    public void TriggerShot()
    {
        if (shot != null) shot.SetActive(true);

        Invoke(nameof(DeactivateShot), 0.5f);
        Invoke(nameof(HitShot), 0.5f);

        if (bulletPrefab != null && bulletSpawnPoint != null)
        {
            GameObject bullet = Instantiate(
                bulletPrefab,
                bulletSpawnPoint.position,
                Quaternion.identity);

            // Tag with owner so BulletRageImpact knows which player fired it
            var bulletRage = bullet.GetComponent<BulletRageImpact>();
            if (bulletRage != null)
                bulletRage.playerIndex = playerIndex;

            Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
            if (bulletRb != null)
                bulletRb.AddForce(bulletSpawnPoint.right * 20f, ForceMode.Impulse);
        }
    }

    private void DeactivateShot()
    {
        if (shot != null) shot.SetActive(false);
    }

    private void HitShot()
    {
        if (hit != null)
        {
            hit.SetActive(true);
            Invoke(nameof(DeactivateHit), 1f);
        }
    }

    private void DeactivateHit()
    {
        if (hit != null) hit.SetActive(false);
    }

    private void PreventAutoDestroyOnStop(GameObject target)
    {
        if (target == null) return;

        ParticleSystem[] particleSystems =
            target.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in particleSystems)
        {
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
        }
    }
}
