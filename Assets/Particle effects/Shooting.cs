using UnityEngine;
public class Shooting : MonoBehaviour
{
    public GameObject shot;
    public GameObject hit;

    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PreventAutoDestroyOnStop(hit);

        if (shot != null)
        {
            shot.SetActive(false);
        }
        if (hit != null)
        {
            hit.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void TriggerShot()
    {
        shot.SetActive(true);

        Invoke(nameof(DeactivateShot), 0.5f);
        Invoke(nameof(HitShot), 0.5f);

        GameObject bullet = Instantiate(bulletPrefab, bulletSpawnPoint.position, Quaternion.identity);
        Rigidbody bulletRigidbody = bullet.GetComponent<Rigidbody>();
        bulletRigidbody.AddForce(bulletSpawnPoint.right * 20f, ForceMode.Impulse);
    }


    private void DeactivateShot()
    {
        if (shot != null)
        {
            shot.SetActive(false);
        }
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
        if (hit != null)
        {
            hit.SetActive(false);
        }
    }

    private void PreventAutoDestroyOnStop(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        ParticleSystem[] particleSystems = target.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            var main = particleSystem.main;
            main.stopAction = ParticleSystemStopAction.None;
        }
    }

}
