using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace RuriKit
{
    /// <summary>
    ///     管理并复用预制体实例和具有无参数构造函数的纯 C# 对象。
    /// </summary>
    public class PoolManager : ManagerSingleton<PoolManager>
    {
        private const int DEFAULT_GO_CAPACITY = 16;
        private const int DEFAULT_CS_CAPACITY = 32;
        private const int GO_POOL_MAX_SIZE = 2048;

        private readonly Dictionary<Type, ICSPool> _csPools = new();
        private readonly Dictionary<int, Coroutine> _delayedReleases = new();
        private readonly HashSet<int> _goBorrowedInstances = new();
        private readonly Dictionary<int, GameObject> _goInstances = new();
        private readonly Dictionary<int, ObjectPool<GameObject>> _goPools = new();
        private readonly Dictionary<int, int> _instanceToPrefab = new();
        private readonly Dictionary<int, Transform> _poolRoots = new();

        protected override void OnSingletonDestroy()
        {
            Shutdown();
        }

        /// <summary>
        ///     为指定预制体预先创建并回收到对象池中。
        /// </summary>
        /// <param name="prefab">用于创建实例的预制体。为 <c>null</c> 时不执行操作并输出警告。</param>
        /// <param name="count">要预先创建的数量。小于或等于 0 时不执行操作。</param>
        public void Preload(GameObject prefab, int count)
        {
            if (!prefab)
            {
                Debug.LogWarning("Preload 失败：prefab 为 null。");
                return;
            }
            if (count <= 0) return;

            ObjectPool<GameObject> pool = GetOrCreateGOPool(prefab, Mathf.Max(count, DEFAULT_GO_CAPACITY));

            GameObject[] temp = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                temp[i] = GetPooledInstance(pool);
            }
            for (int i = 0; i < count; i++)
            {
                Release(temp[i]);
            }
        }

        /// <summary>
        ///     从指定预制体对应的对象池中获取一个活动实例，并保留实例当前的 Transform 状态。
        /// </summary>
        /// <param name="prefab">用于创建实例的预制体。为 <c>null</c> 时不创建实例并返回 <c>null</c>。</param>
        /// <returns>已激活并脱离对象池根节点的实例；获取失败时返回 <c>null</c>。</returns>
        public GameObject Get(GameObject prefab)
        {
            if (!prefab)
            {
                Debug.LogWarning("Get 失败：prefab 为 null。");
                return null;
            }

            ObjectPool<GameObject> pool = GetOrCreateGOPool(prefab, DEFAULT_GO_CAPACITY);
            return GetPooledInstance(pool);
        }

        /// <summary>
        ///     从对象池获取一个实例，设置其父级、世界坐标和世界旋转后再激活。
        /// </summary>
        /// <param name="prefab">用于创建实例的预制体。为 <c>null</c> 时不创建实例并返回 <c>null</c>。</param>
        /// <param name="position">实例激活前设置的世界坐标。</param>
        /// <param name="rotation">实例激活前设置的世界旋转。</param>
        /// <param name="parent">实例的新父级。可以为 <c>null</c>。</param>
        /// <returns>完成变换设置的实例；获取失败时返回 <c>null</c>。</returns>
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!prefab)
            {
                Debug.LogWarning("Get 失败：prefab 为 null。");
                return null;
            }

            ObjectPool<GameObject> pool = GetOrCreateGOPool(prefab, DEFAULT_GO_CAPACITY);
            GameObject instance = pool.Get();
            Transform t = instance.transform;
            t.SetParent(parent);
            t.SetPositionAndRotation(position, rotation);
            ActivateBorrowedInstance(instance);

            return instance;
        }

        /// <summary>
        ///     将实例立即归还到所属对象池；不属于任何对象池的实例会被销毁，已归还的实例会被忽略。
        /// </summary>
        /// <param name="instance">要归还的实例。为 <c>null</c> 时不执行任何操作。</param>
        public void Release(GameObject instance)
        {
            if (!instance) return;

            int instanceId = instance.GetInstanceID();
            if (!_instanceToPrefab.TryGetValue(instanceId, out int prefabId))
            {
                Debug.LogWarning($"Release 失败：实例 '{instance.name}' 不属于任何对象池，直接销毁。");
                CancelDelayedRelease(instanceId);
                _goInstances.Remove(instanceId);
                _goBorrowedInstances.Remove(instanceId);
                Destroy(instance);
                return;
            }

            CancelDelayedRelease(instanceId);

            if (!_goBorrowedInstances.Remove(instanceId))
            {
                Debug.LogWarning($"Release 失败：实例 '{instance.name}' 已归还到对象池，忽略重复归还。");
                return;
            }

            if (!_goPools.TryGetValue(prefabId, out ObjectPool<GameObject> pool))
            {
                Debug.LogWarning($"Release 失败：实例 '{instance.name}' 对应的对象池已不存在，直接销毁。");
                DestroyManagedInstance(instanceId, instance);
                return;
            }

            pool.Release(instance);
        }

        /// <summary>
        ///     在指定延迟后将实例归还到所属对象池，延迟时间受 <see cref="Time.timeScale" /> 影响。
        /// </summary>
        /// <param name="instance">要归还的实例。为 <c>null</c> 时不执行任何操作。</param>
        /// <param name="delaySeconds">归还前的延迟时间，单位为秒。小于或等于 0 时立即归还。</param>
        public void Release(GameObject instance, float delaySeconds)
        {
            if (!instance) return;
            if (delaySeconds <= 0f)
            {
                Release(instance);
                return;
            }

            int instanceId = instance.GetInstanceID();
            if (!_instanceToPrefab.ContainsKey(instanceId))
            {
                Debug.LogWarning($"Release 失败：实例 '{instance.name}' 不属于任何对象池，直接销毁。");
                CancelDelayedRelease(instanceId);
                _goInstances.Remove(instanceId);
                _goBorrowedInstances.Remove(instanceId);
                Destroy(instance);
                return;
            }

            if (!_goBorrowedInstances.Contains(instanceId))
            {
                Debug.LogWarning($"Release 失败：实例 '{instance.name}' 已归还到对象池，忽略重复延迟归还。");
                return;
            }

            CancelDelayedRelease(instanceId);

            Coroutine coroutine = StartCoroutine(DelayedReleaseRoutine(instance, instanceId, delaySeconds));
            _delayedReleases[instanceId] = coroutine;
        }

        /// <summary>
        ///     确定是否已经为指定预制体创建对象池。
        /// </summary>
        /// <param name="prefab">要检查的预制体。</param>
        /// <returns>存在对应对象池时为 <c>true</c>；否则为 <c>false</c>。</returns>
        public bool HasPool(GameObject prefab)
        {
            return prefab && _goPools.ContainsKey(prefab.GetInstanceID());
        }

        /// <summary>
        ///     释放并移除指定预制体对应的对象池，并销毁该对象池当前管理的全部实例。
        /// </summary>
        /// <param name="prefab">要清理对象池的预制体。为 <c>null</c> 或尚未创建对象池时不执行操作。</param>
        public void ClearPool(GameObject prefab)
        {
            if (!prefab) return;

            int prefabId = prefab.GetInstanceID();
            if (!_goPools.TryGetValue(prefabId, out ObjectPool<GameObject> pool)) return;

            List<int> instanceIds = new(16);
            foreach (KeyValuePair<int, int> kv in _instanceToPrefab)
            {
                if (kv.Value == prefabId)
                {
                    instanceIds.Add(kv.Key);
                }
            }

            pool.Dispose();
            _goPools.Remove(prefabId);

            foreach (int instanceId in instanceIds)
            {
                if (_goInstances.TryGetValue(instanceId, out GameObject instance))
                {
                    DestroyManagedInstance(instanceId, instance);
                }
            }

            if (_poolRoots.Remove(prefabId, out Transform poolRoot))
            {
                if (poolRoot)
                {
                    Destroy(poolRoot.gameObject);
                }
            }
        }

        /// <summary>
        ///     为指定纯 C# 类型预先创建并回收到对象池中。
        /// </summary>
        /// <typeparam name="T">具有无参数构造函数的引用类型。</typeparam>
        /// <param name="count">要预先创建的数量。小于或等于 0 时不执行操作。</param>
        public void Preload<T>(int count) where T : class, new()
        {
            if (count <= 0) return;

            CSPool<T> pool = GetOrCreateCSPool<T>(count);

            T[] temp = new T[count];
            for (int i = 0; i < count; i++)
            {
                temp[i] = pool.Get();
            }
            for (int i = 0; i < count; i++)
            {
                pool.Release(temp[i]);
            }
        }

        /// <summary>
        ///     从指定纯 C# 类型的对象池中获取一个对象，对象字段会保留上次归还时的状态。
        /// </summary>
        /// <typeparam name="T">具有无参数构造函数的引用类型。</typeparam>
        /// <returns>从对象池获取的对象。</returns>
        public T Get<T>() where T : class, new()
        {
            CSPool<T> pool = GetOrCreateCSPool<T>(DEFAULT_CS_CAPACITY);
            return pool.Get();
        }

        /// <summary>
        ///     将纯 C# 对象归还到其类型对应的对象池；重复归还或对象池不存在时不执行操作。
        /// </summary>
        /// <typeparam name="T">具有无参数构造函数的引用类型。</typeparam>
        /// <param name="obj">要归还的对象。为 <c>null</c> 时不执行操作。</param>
        public void Release<T>(T obj) where T : class, new()
        {
            if (obj == null) return;

            Type type = typeof(T);
            if (!_csPools.TryGetValue(type, out ICSPool csPool))
            {
                return;
            }

            if (csPool is CSPool<T> typedPool && !typedPool.Release(obj))
            {
                Debug.LogWarning($"Release<{typeof(T).Name}> 失败：对象不属于当前借出集合，忽略归还。");
            }
        }

        /// <summary>
        ///     释放并移除指定纯 C# 类型的对象池，未归还对象的借出记录会被清除。
        /// </summary>
        /// <typeparam name="T">要清理对象池的引用类型。</typeparam>
        public void ClearPool<T>() where T : class, new()
        {
            Type type = typeof(T);
            if (!_csPools.TryGetValue(type, out ICSPool csPool)) return;

            csPool.Dispose();
            _csPools.Remove(type);
        }

        /// <summary>
        ///     清理所有对象池中当前未借出的对象，同时保留对象池和已借出对象。
        /// </summary>
        public void ClearAllUnused()
        {
            foreach (ObjectPool<GameObject> pool in _goPools.Values)
            {
                pool.Clear();
            }

            foreach (ICSPool pool in _csPools.Values)
            {
                pool.Clear();
            }
        }

        /// <summary>
        ///     停止延迟归还任务，释放所有对象池，并销毁管理器追踪的游戏对象。
        /// </summary>
        public void Shutdown()
        {
            foreach (Coroutine c in _delayedReleases.Values)
            {
                if (c != null)
                {
                    StopCoroutine(c);
                }
            }
            _delayedReleases.Clear();

            foreach (ObjectPool<GameObject> pool in _goPools.Values)
            {
                pool.Dispose();
            }
            _goPools.Clear();

            foreach (GameObject instance in _goInstances.Values)
            {
                if (instance)
                {
                    Destroy(instance);
                }
            }
            _goInstances.Clear();

            _goBorrowedInstances.Clear();
            _instanceToPrefab.Clear();

            foreach (Transform root in _poolRoots.Values)
            {
                if (root)
                {
                    Destroy(root.gameObject);
                }
            }
            _poolRoots.Clear();

            foreach (ICSPool pool in _csPools.Values)
            {
                pool.Dispose();
            }
            _csPools.Clear();
        }

        private void GetOrCreatePoolRoot(int prefabId, string prefabName)
        {
            if (!_poolRoots.TryGetValue(prefabId, out Transform root))
            {
                GameObject rootGo = new($"Pool: {prefabName}");
                rootGo.transform.SetParent(transform);
                rootGo.transform.localPosition = Vector3.zero;
                root = rootGo.transform;
                _poolRoots[prefabId] = root;
            }
        }

        private ObjectPool<GameObject> GetOrCreateGOPool(GameObject prefab, int defaultCapacity)
        {
            int prefabId = prefab.GetInstanceID();

            if (_goPools.TryGetValue(prefabId, out ObjectPool<GameObject> existing))
            {
                return existing;
            }

            GetOrCreatePoolRoot(prefabId, prefab.name);

            ObjectPool<GameObject> pool = new(
                () =>
                {
                    GameObject go = Instantiate(prefab, _poolRoots[prefabId], true);
                    go.SetActive(false);
                    int instanceId = go.GetInstanceID();
                    _goInstances[instanceId] = go;
                    _instanceToPrefab[instanceId] = prefabId;
                    return go;
                },
                go =>
                {
                    int instanceId = go.GetInstanceID();
                    _goInstances[instanceId] = go;
                    _instanceToPrefab[instanceId] = prefabId;
                },
                go =>
                {
                    go.SetActive(false);
                    go.transform.SetParent(_poolRoots[prefabId]);
                },
                go =>
                {
                    int instanceId = go.GetInstanceID();
                    _goInstances.Remove(instanceId);
                    _instanceToPrefab.Remove(instanceId);
                    _goBorrowedInstances.Remove(instanceId);
                    CancelDelayedRelease(instanceId);
                    Destroy(go);
                },
                false,
                defaultCapacity,
                GO_POOL_MAX_SIZE
            );

            _goPools[prefabId] = pool;
            return pool;
        }

        private GameObject GetPooledInstance(ObjectPool<GameObject> pool)
        {
            GameObject instance = pool.Get();
            int instanceId = instance.GetInstanceID();
            Transform t = instance.transform;
            if (_instanceToPrefab.TryGetValue(instanceId, out int prefabId) &&
                _poolRoots.TryGetValue(prefabId, out Transform poolRoot) &&
                t.parent &&
                t.parent == poolRoot)
            {
                t.SetParent(null);
            }
            ActivateBorrowedInstance(instance);
            return instance;
        }

        private void ActivateBorrowedInstance(GameObject instance)
        {
            int instanceId = instance.GetInstanceID();
            _goBorrowedInstances.Add(instanceId);
            instance.SetActive(true);
        }

        private void DestroyManagedInstance(int instanceId, GameObject instance)
        {
            CancelDelayedRelease(instanceId);
            _goInstances.Remove(instanceId);
            _instanceToPrefab.Remove(instanceId);
            _goBorrowedInstances.Remove(instanceId);
            if (instance)
            {
                Destroy(instance);
            }
        }

        private CSPool<T> GetOrCreateCSPool<T>(int defaultCapacity) where T : class, new()
        {
            Type type = typeof(T);

            if (_csPools.TryGetValue(type, out ICSPool existing))
            {
                return (CSPool<T>)existing;
            }

            ObjectPool<T> pool = new(
                () => new T(),
                null,
                null,
                null,
                false,
                defaultCapacity
            );

            CSPool<T> csPool = new(pool);
            _csPools[type] = csPool;
            return csPool;
        }

        private void CancelDelayedRelease(int instanceId)
        {
            if (_delayedReleases.TryGetValue(instanceId, out Coroutine c))
            {
                if (c != null)
                {
                    StopCoroutine(c);
                }
                _delayedReleases.Remove(instanceId);
            }
        }

        private IEnumerator DelayedReleaseRoutine(GameObject instance, int instanceId, float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);

            _delayedReleases.Remove(instanceId);
            if (!instance)
            {
                _goInstances.Remove(instanceId);
                _instanceToPrefab.Remove(instanceId);
                _goBorrowedInstances.Remove(instanceId);
                yield break;
            }

            Release(instance);
        }
    }
}