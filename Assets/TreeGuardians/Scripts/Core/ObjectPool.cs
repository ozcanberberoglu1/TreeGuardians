using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreeGuardians.Core
{
    public abstract class PooledBehaviour : MonoBehaviour
    {
        internal Action releaseAction;
        public bool IsPooledActive { get; internal set; }

        public void ReleaseToPool()
        {
            if (!IsPooledActive) return;
            if (releaseAction != null) releaseAction();
            else gameObject.SetActive(false);
        }

        public virtual void OnSpawned() { }
        public virtual void OnDespawned() { }
    }

    public sealed class ObjectPool<T> where T : PooledBehaviour
    {
        readonly T prefab;
        readonly Transform root;
        readonly Stack<T> free;
        readonly List<T> all;
        readonly int maxSize;

        public int CountAll => all.Count;
        public int CountFree => free.Count;
        public int CountActive => all.Count - free.Count;
        public T Prefab => prefab;

        public ObjectPool(T prefab, Transform root, int prewarm, int maxSize = 512)
        {
            this.prefab = prefab;
            this.root = root;
            this.maxSize = Mathf.Max(1, maxSize);
            free = new Stack<T>(Mathf.Max(4, prewarm));
            all = new List<T>(Mathf.Max(4, prewarm));
            for (int i = 0; i < prewarm; i++)
            {
                var item = CreateNew();
                item.gameObject.SetActive(false);
                free.Push(item);
            }
        }

        T CreateNew()
        {
            var item = UnityEngine.Object.Instantiate(prefab, root);
            item.name = prefab.name;
            item.releaseAction = () => Release(item);
            all.Add(item);
            return item;
        }

        public T Get()
        {
            T item;
            if (free.Count > 0) item = free.Pop();
            else if (all.Count < maxSize) item = CreateNew();
            else
            {
                Debug.LogWarning($"[ObjectPool] {prefab.name} exceeded max size {maxSize}; reusing oldest active.");
                item = all[0];
                item.OnDespawned();
            }
            item.IsPooledActive = true;
            item.gameObject.SetActive(true);
            item.OnSpawned();
            return item;
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            var item = Get();
            item.transform.SetPositionAndRotation(position, rotation);
            return item;
        }

        public void Release(T item)
        {
            if (item == null || !item.IsPooledActive) return;
            item.IsPooledActive = false;
            item.OnDespawned();
            item.gameObject.SetActive(false);
            if (root != null) item.transform.SetParent(root, false);
            free.Push(item);
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].IsPooledActive) Release(all[i]);
        }

        public void DestroyAll()
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null) UnityEngine.Object.Destroy(all[i].gameObject);
            all.Clear();
            free.Clear();
        }
    }
}
