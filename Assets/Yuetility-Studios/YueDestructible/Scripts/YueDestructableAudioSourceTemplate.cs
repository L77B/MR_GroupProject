using UnityEngine;

namespace YueDestructibles
{
    public class YueDestructableAudioSourceTemplate : MonoBehaviour
    {
        public AudioSource audioSource;
        [HideInInspector]
        public Transform[] debris;

        private float timeAlive = 0f;
        private const float maxTimeAlive = 10f;
        private Vector3 averagePosition;

        void Update()
        {
            if (debris.Length <= 0)
                Destroy(this.gameObject);

            if (!debris[0])
                Destroy(this.gameObject);

            // calculate position
            averagePosition = Vector3.zero;
            foreach (Transform t in debris)
            { 
                if(t)
                    averagePosition += t.position;
            }
            averagePosition /= debris.Length;

            // add position
            transform.position = averagePosition;

            // destroy, if overtime
            timeAlive += Time.deltaTime;

            if (timeAlive > maxTimeAlive)
                Destroy(this.gameObject);
        }
    }
}
