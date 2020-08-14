using UnityEngine;

namespace Utils.Pooling
{
    [System.Serializable]
    public class PrefabPool
    {
        public GameObject prefab;

        private ObjectPool<GameObject> pool = new ObjectPool<GameObject>();

        /// <summary>
        /// Returns the next available instance of the prefab, creating a new one
        /// if none exist in the pool. 
        /// Requires a Transform to be the parent of the new instance.
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        public GameObject Next(Transform parent)
        {
            GameObject nextObj;

            if (pool.Count > 0)
            {
                nextObj = pool.Next();
                nextObj.SetActive(true);
            }
            else
            {
                nextObj = Object.Instantiate(prefab, parent);
            }
            return nextObj;
        }

        public void ReturnToPool(GameObject obj)
        {
            obj.SetActive(false);
            pool.Add(obj);
        }

    }
}