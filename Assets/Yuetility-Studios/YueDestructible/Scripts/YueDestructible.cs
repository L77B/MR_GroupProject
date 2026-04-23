using UnityEngine;
using UnityEngine.Events;

namespace YueDestructibles
{
    [RequireComponent(typeof(Rigidbody))]
    public class YueDestructible : MonoBehaviour
    {
        [Header("Destruction Properties")]
        public float shatterBounceMultiplier = 3f;
        public float maximumImpulse = 1.5f;
        public float impulseDamageMultiplier = 30f;
        public bool accumulateDamage = false;

        [Header("Health [%]")]
        [SerializeField]
        [Range(0f, 100f)]
        private float health = 100f;

        [Header("Debris Disappearance")]
        public bool isDisappearing = true;
        public float disappearingTime = 5f;

        [Header("Sound Effects")]
        public AudioClip[] destructionClips;
        public float impulseVolumeFactor = 0.5f;
        public YueDestructableAudioSourceTemplate audioSourceTemplate;

        [Header("Dependencies")]
        public GameObject debrisRoot;
        [Header("Events")]
        public UnityEvent onObjectDestruct;
        // variables
        #region 
        private Renderer mainObjectRenderer;
        private Rigidbody[] debris;
        private Rigidbody rigid;

        private bool isDestructed = false;
        private bool isSetup = false;
        #endregion

        private void Start()
        {
            // disable deris meshes
            debrisRoot.SetActive(false);
            // get list of debris rigidbodies
            debris = debrisRoot.GetComponentsInChildren<Rigidbody>();
            // get renderer to determine object size
            mainObjectRenderer = GetComponent<Renderer>();
            // get main rigidbody
            rigid = GetComponent<Rigidbody>();
            // check UnityEvent
            if (onObjectDestruct == null)
                onObjectDestruct = new UnityEvent();

            isSetup = true;
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (!isSetup)
                return;

            if (collision.impulse.magnitude > maximumImpulse)
            {
                DestructWithImpulse(collision.impulse, collision.GetContact(0).point);
            }

            if (!accumulateDamage)
                return;

            health -= collision.impulse.magnitude * impulseDamageMultiplier;

            if (health < 0f)
                DestructWithImpulse(collision.impulse, collision.GetContact(0).point);
        }

        public void DestructWithImpulse(Vector3 impulse, Vector3 point)
        {
            if (isDestructed)
                return;

            if (debris.Length <= 0)
                return;

            // active and unparent root
            debrisRoot.SetActive(true);
            debrisRoot.transform.parent = null;

            // inherit main rigid velocity
            foreach (Rigidbody rb in debris)
            {
                if (!rb)
                    break;

                // set active
                rb.gameObject.SetActive(true);

                // apply propertie
                rb.mass = rigid.mass / debris.Length;
                rb.linearDamping = rigid.linearDamping;
                rb.angularDamping = rigid.angularDamping;

                // add velocity
                rb.linearVelocity = rigid.linearVelocity;
                rb.angularVelocity = rigid.angularVelocity;

                // add impulse
                rb.AddExplosionForce(impulse.magnitude * shatterBounceMultiplier, point, mainObjectRenderer.bounds.max.magnitude);
            }

            // create sound effect
            CreateAudioEffect(impulse, point);

            // set flag
            isDestructed = true;

            // setup root, if disappearing
            if (isDisappearing)
            {
                debrisRoot.AddComponent<YueDestructiblesRoot>();
                debrisRoot.GetComponent<YueDestructiblesRoot>().SetDebris(debris);
                debrisRoot.GetComponent<YueDestructiblesRoot>().SetDisappearingTime(disappearingTime);
            }

            // invoke event
            onObjectDestruct.Invoke();

            // destroy intact object
            Destroy(this.gameObject);
        }
        public void DestructSimple()
        {
            DestructWithImpulse(Vector3.zero, transform.position);
        }
        public void AddDamage(float damageInPercent)
        {
            health -= damageInPercent;
        }

        private void CreateAudioEffect(Vector3 impulse, Vector3 point)
        {
            if (destructionClips.Length <= 0)
                return;

            // instatiate sound effect
            GameObject effect = Instantiate<GameObject>(audioSourceTemplate.gameObject, point, Quaternion.identity);

            // init debris
            YueDestructableAudioSourceTemplate templateClone = effect.GetComponent<YueDestructableAudioSourceTemplate>();
            templateClone.debris = debrisRoot.GetComponentsInChildren<Transform>();

            templateClone.audioSource.volume = impulseVolumeFactor * impulse.magnitude;

            // play random sound
            templateClone.audioSource.PlayOneShot(destructionClips[Random.Range(0, destructionClips.Length)]);
            Destroy(effect, 5f);
        }
    }
}