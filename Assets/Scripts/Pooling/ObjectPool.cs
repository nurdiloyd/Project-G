using System.Collections.Generic;
using Pooling.Interfaces;
using UnityEngine;
using Utilities;
using Object = UnityEngine.Object;

namespace Pooling
{
    public class ObjectPool
    {
        private readonly Stack<IPoolable> objects = new ();
        private readonly HashSet<IPoolable> activeObjects = new ();
        private readonly IPoolable poolObject;

        private int Count => objects.Count;
        private Transform PoolParent { get; set; }

        public ObjectPool(IPoolable poolObject, int numToSpawn = 0)
        {
            this.poolObject = poolObject;
            
            if (numToSpawn > 0)
            {
                SetParent();
                Spawn(numToSpawn, PoolParent);
            }

            void SetParent()
            {
                if (PoolParent == null)
                {
                    Transform parent = new GameObject(poolObject.Type.ToString()).transform;
                    parent.SetParent(PoolFactory.instance.transform);
                    PoolParent = parent;
                }
            }
        }

        private void Spawn(int number, Transform parent = null)
        {
            for (int i = 0; i < number; i++)
            {
                IPoolable poolableObj = CreateObject(parent);
                objects.Push(poolableObj);
                poolableObj.GameObject.SetActive(false);
            }
        }

        private IPoolable CreateObject(Transform parent = null)
        {
            IPoolable poolableObj = (IPoolable)Object.Instantiate((Object)poolObject, parent);
            return poolableObj;
        }

        #region Pull Functions
        
        public IPoolable Pull()
        {
            IPoolable poolableObj = Count > 0 ? objects.Pop() : CreateObject(PoolParent);
            poolableObj.GameObject.SetActive(true);
            poolableObj.OnSpawn();
            
            if (activeObjects.Add(poolableObj) == false)
            {
                Log.Error(("BUG: Trying to add the same object ->", poolableObj.Type), poolableObj.GameObject);
            }
            return poolableObj;
        }

        public IPoolable Pull(Transform parent)
        {
            IPoolable poolableObj = Pull();
            poolableObj.GameObject.transform.SetParent(parent);
            return poolableObj;
        }
        
        public IPoolable Pull(Vector3 position)
        {
            IPoolable poolableObj = Pull();
            poolableObj.GameObject.transform.position = position;
            return poolableObj;
        }
        
        public IPoolable Pull(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            IPoolable poolableObj = Pull();
            poolableObj.GameObject.transform.SetParent(parent);
            poolableObj.GameObject.transform.position = position;
            poolableObj.GameObject.transform.rotation = rotation;
            return poolableObj;
        }
        
        #endregion

        public void Push(IPoolable obj)
        {
            activeObjects.Remove(obj);

            if (objects.Contains(obj))
            {
                Log.Error(("BUG: Trying to add the same object ->", obj.Type), obj.GameObject);
                return;
            }
            objects.Push(obj);
            
            obj.OnReset();
            obj.GameObject.SetActive(false);
            obj.GameObject.transform.SetParent(PoolParent);
            obj.GameObject.transform.localPosition = Vector3.zero;
            obj.GameObject.transform.eulerAngles = Vector3.zero;
        }

        public void Reset()
        {
            foreach (IPoolable item in activeObjects)
            {
                PoolFactory.instance.ResetObject(item);
            }
            activeObjects.Clear();
        }
    }
}