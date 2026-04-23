using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YueDestructibles
{
    public class YueDestructiblesGunDemo : MonoBehaviour
    {
        [Header("*** Important ***")]
        [Header("To see how to destruct objects by code look into this script.")]
        [Header("*** Important *** \n")]

        [Header("Gun Config")]
        public float impulseFactor = 10f;

        [Header("Prefabs")]
        public List<GameObject> prefabs;

        [Header("Dependencies")]
        public Camera fpsCamera;

        // variables
        private AudioSource source;
        private List<GameObject> spawnedPrefabs;
        private List<float> counters;
        private const float timeUntilSpawn = 5f;

        void Start()
        {// this is just about respawning the prefabs
            SetupPrefabs();
            source = GetComponent<AudioSource>();
        }
        void Update()
        {
            if (Input.GetButtonDown("Fire1"))
            {
                // play audioclip
                source.PlayOneShot(source.clip);

                // create raycast with mouse position
                Ray ray = fpsCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    // check if there is a Destructable component on the hit object
                    if(hit.transform.GetComponentInParent<YueDestructible>() != null)
                    {
                        // calculate direction of impulse of the "bullet"
                        Vector3 direction = (hit.point - fpsCamera.transform.position).normalized;

                        // called "DestructWithImpulse" method to destruct the object
                        // direction is multiplied with the impulse of the bullet
                        // the point of the raycast hit has to be passed into method
                        hit.transform.GetComponentInParent<YueDestructible>().DestructWithImpulse(direction * impulseFactor, hit.point);
                    }
                }
            }

            // respawn prefabs
            UpdatePrefabs();
        }


        // this below is just about respawning the prefabs
        private void SetupPrefabs()
        {
            spawnedPrefabs = new List<GameObject>();
            for (int i = 0; i < prefabs.Count; i++)
            {
                spawnedPrefabs.Add(Instantiate<GameObject>(prefabs[i], Vector3.zero + Vector3.forward * (0.25f * i) - Vector3.forward * 0.5f, Quaternion.identity));
            }

            counters = new List<float>();

            for(int i = 0; i < prefabs.Count; i++)
                counters.Add(0);
        }
        private void UpdatePrefabs()
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (!spawnedPrefabs[i])
                {
                    if (counters[i] > timeUntilSpawn)
                    {
                        spawnedPrefabs[i] = Instantiate<GameObject>(prefabs[i], Vector3.zero + Vector3.forward * (0.25f * i) - Vector3.forward * 0.5f, Quaternion.identity);
                        counters[i] = 0;
                    }

                    counters[i] += Time.deltaTime;
                }
            }
        }
    }
}