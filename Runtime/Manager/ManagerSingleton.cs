using UnityEngine;

// ReSharper disable StaticMemberInGenericType

namespace RuriKit
{
    /// <summary>
    ///     为全局管理器提供按需创建、重复实例处理和退出保护。
    /// </summary>
    /// <typeparam name="T">具体的管理器类型。</typeparam>
    /// <remarks>
    ///     派生类通过单例生命周期钩子执行初始化和清理，不应另行声明 Awake、OnApplicationQuit 或 OnDestroy。
    /// </remarks>
    public abstract class ManagerSingleton<T> : MonoBehaviour where T : ManagerSingleton<T>
    {
        private static T _instance;
        private static int _runtimeGeneration = -1;

        private bool _isInitialized;

        /// <summary>
        ///     获取当前管理器实例；场景中不存在时自动创建一个持久化实例。
        /// </summary>
        public static T Instance
        {
            get
            {
                ResetStaticStateIfNeeded();

                if (!_instance && ManagerSingletonRuntime.IsApplicationQuitting)
                {
                    Debug.LogWarning($"应用程序正在退出时，调用了 {typeof(T).Name} 单例实例。");
                    return null;
                }

                if (!_instance)
                {
                    _instance = FindObjectOfType<T>();
                    if (!_instance)
                    {
                        GameObject go = new($"[{typeof(T).Name}]");
                        DontDestroyOnLoad(go);
                        _instance = go.AddComponent<T>();
                    }
                }

                _instance.EnsureInitialized();
                return _instance;
            }
        }

        /// <summary>
        ///     获取是否已经存在可用实例。
        /// </summary>
        public static bool HasInstance
        {
            get
            {
                ResetStaticStateIfNeeded();
                return _instance;
            }
        }

        /// <summary>
        ///     尝试获取当前可用实例，不会自动创建新对象。
        /// </summary>
        public static bool TryGetInstance(out T manager)
        {
            ResetStaticStateIfNeeded();
            manager = _instance;
            return manager;
        }

        private void Awake()
        {
            ResetStaticStateIfNeeded();

            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            EnsureInitialized();
        }

        private void OnApplicationQuit()
        {
            ManagerSingletonRuntime.MarkApplicationQuitting();
            OnSingletonApplicationQuit();
        }

        private void OnDestroy()
        {
            try
            {
                OnSingletonDestroy();
            }
            finally
            {
                ResetStaticStateIfNeeded();
                if (_instance == this)
                {
                    _instance = null;
                }
            }
        }

        protected virtual void OnSingletonAwake()
        {
            // PASS
        }

        protected virtual void OnSingletonApplicationQuit()
        {
            // PASS
        }

        protected virtual void OnSingletonDestroy()
        {
            // PASS
        }

        private void EnsureInitialized()
        {
            if (_isInitialized) return;

            _isInitialized = true;
            OnSingletonAwake();
        }

        private static void ResetStaticStateIfNeeded()
        {
            if (_runtimeGeneration == ManagerSingletonRuntime.Generation) return;

            _runtimeGeneration = ManagerSingletonRuntime.Generation;
            _instance = null;
        }
    }

    internal static class ManagerSingletonRuntime
    {
        internal static int Generation { get; private set; }
        internal static bool IsApplicationQuitting { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Generation++;
            IsApplicationQuitting = false;
        }

        internal static void MarkApplicationQuitting()
        {
            IsApplicationQuitting = true;
        }
    }
}