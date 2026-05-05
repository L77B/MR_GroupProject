namespace ExpObj
{
    using UnityEngine;

    public class BrokenObject : MonoBehaviour
    {
        [Header("Breaking settings")]
        [SerializeField] GameObject[] pieces;
        [SerializeField] float velMultiplier = 2f;
        [SerializeField] float timeBeforeDestroying = 60f;

        void OnEnable()
        {
            Destroy(gameObject, timeBeforeDestroying);
        }

        public void RandomVelocities()
        {
            for (int i = 0; i <= pieces.Length - 1; i++)
            {
                float xVel = Random.Range(-1f, 1f);
                float yVel = Random.Range(0, 1f);
                float zVel = Random.Range(-1f, 1f);
                Vector3 vel = new Vector3(velMultiplier * xVel, velMultiplier * yVel, velMultiplier * zVel);
                pieces[i].GetComponent<Rigidbody>().linearVelocity = vel;
            }
        }
    }
}