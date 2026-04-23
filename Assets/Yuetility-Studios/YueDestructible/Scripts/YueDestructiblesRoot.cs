using UnityEngine;

namespace YueDestructibles
{
    public class YueDestructiblesRoot : MonoBehaviour
    {
        private Rigidbody[] debris;
        private float dissapearingSpeed = 0f;
        private float size = 1f;

        public void SetDisappearingTime(float time)
        {
            dissapearingSpeed = 1 / time;
        }
        public void SetDebris(Rigidbody[] rb)
        {
            debris = rb;
        }

        private void Update()
        {
            // Update Size until 0, then destroy
            size -= Time.deltaTime * dissapearingSpeed;
            foreach(Rigidbody r in debris)
            {
                r.transform.localScale = Vector3.one * size;
            }

            if(size <= 0.05f)
                Destroy(gameObject);
        }
    }
}
