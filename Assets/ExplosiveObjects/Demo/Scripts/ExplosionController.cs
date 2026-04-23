namespace ExpObj
{
    using System.Collections.Generic;
    using UnityEngine;

    public class ExplosionController : MonoBehaviour
    {
        public List<ExplosiveObject> explosiveObjects = new List<ExplosiveObject>();

        public ExplosiveObject explosiveObject;

        public List<Transform> explosiveObjTransforms = new List<Transform>();

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.E)) // make explosive objects explode
            {
                for (int i = 0; i < explosiveObjects.Count; i++)
                {
                    if (explosiveObjects[i] != null)
                    {
                        explosiveObjects[i].Explode(); // calling function to make objects explode
                    }
                }
                explosiveObjects.Clear();
            }

            if (Input.GetKeyDown(KeyCode.R)) // respawning explosive objects
            {
                RespawnObjects();
            }
        }

        void RespawnObjects()
        {
            foreach (Transform t in explosiveObjTransforms)
            {
                GameObject newObj = Instantiate(explosiveObject.gameObject, t.position, Quaternion.identity);
                explosiveObjects.Add(newObj.GetComponent<ExplosiveObject>());
            }
        }
    }
}